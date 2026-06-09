using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text;
using RagService.Core.Domain;
using RagService.Core.DTOs;
using RagService.Core.Interfaces;
using RagService.Infrastructure.Configuration;
using RagService.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.Configure<RagSettings>(builder.Configuration.GetSection("RagSettings"));


builder.Services.AddHttpClient();


builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();


builder.Services.AddTransient<IDocumentIngestionService, MarkdownIngestionService>();


builder.Services.AddTransient<OllamaEmbeddingService>();
builder.Services.AddTransient<OpenAiEmbeddingService>();
builder.Services.AddTransient<OllamaLlmService>();
builder.Services.AddTransient<OpenAiLlmService>();

// Dynamic injection of services based on configuration provider values
builder.Services.AddTransient<IEmbeddingService>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<RagSettings>>().Value;
    return settings.EmbeddingProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<OpenAiEmbeddingService>()
        : sp.GetRequiredService<OllamaEmbeddingService>();
});

builder.Services.AddTransient<ILlmService>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<RagSettings>>().Value;
    return settings.LlmProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<OpenAiLlmService>()
        : sp.GetRequiredService<OllamaLlmService>();
});

// Configure Swagger OpenApi services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable static files to serve the chatbot UI
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable Swagger UI and place it at /swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ASP.NET Core RAG Web API v1");
    c.RoutePrefix = "swagger"; // Serves Swagger UI at /swagger
});

// GET /health - Check status of the RAG system
app.MapGet("/health", (IOptions<RagSettings> settings) =>
{
    var s = settings.Value;
    return Results.Ok(new
    {
        Status = "Healthy",
        LlmProvider = s.LlmProvider,
        EmbeddingProvider = s.EmbeddingProvider,
        DocsFolder = s.DocsFolder,
        ChunkSize = s.ChunkSize,
        ChunkOverlap = s.ChunkOverlap
    });
})
.WithName("HealthCheck");

// POST /ingest - Manually re-index the markdown document files
app.MapPost("/ingest", async (IDocumentIngestionService ingestionService, IOptions<RagSettings> settings) =>
{
    try
    {
        await ingestionService.IngestFolderAsync(settings.Value.DocsFolder);
        return Results.Ok(new { message = "Ingestion completed successfully." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Ingestion failed: {ex.Message}");
    }
})
.WithName("IngestDocuments");

// POST /chat - Query RAG service with user message
app.MapPost("/chat", async (ChatRequest request, IEmbeddingService embeddingService, IVectorStore vectorStore, ILlmService llmService) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Message cannot be empty.");
    }

    // 1. Calculate embedding for query message
    var queryEmbedding = await embeddingService.GetEmbeddingAsync(request.Message);

    // 2. Search for relevant context text chunks in the Vector Store
    var matches = await vectorStore.SearchSimilarChunksAsync(queryEmbedding, request.Limit);

    // 3. Filter matches by a similarity threshold and assemble context
    var contextBuilder = new StringBuilder();
    var potentialSources = new List<string>();

    // Loose threshold to avoid feeding completely irrelevant chunks
    var relevantMatches = matches.Where(m => m.Similarity >= 0.3f).ToList();

    foreach (var match in relevantMatches)
    {
        contextBuilder.AppendLine($"[Source: {match.Chunk.SourceFile}]");
        contextBuilder.AppendLine(match.Chunk.Content);
        contextBuilder.AppendLine();

        if (!potentialSources.Contains(match.Chunk.SourceFile))
        {
            potentialSources.Add(match.Chunk.SourceFile);
        }
    }

    // 4. Formulate instructions and final prompt
    var systemPrompt = "You are a helpful AI assistant representing Team Alpha. You will answer questions by referring to the provided Markdown document chunks. Cite your sources specifically if you find relevant facts in them. If the context does not contain enough information to answer, state that you are answering from general knowledge.";

    var prompt = $"Context details:\n\n{contextBuilder}\nUser Question: {request.Message}\n\nFormulate your answer based on context:";

    // 5. Query the configured LLM provider for completion
    var answer = await llmService.GenerateCompletionAsync(prompt, systemPrompt);

    // 6. Post-verification of sources: only attribute to files actually referenced in the answer
    var finalSources = new List<string>();
    foreach (var src in potentialSources)
    {
        var filenameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(src);
        if (answer.Contains(src, StringComparison.OrdinalIgnoreCase) ||
            answer.Contains(filenameWithoutExt, StringComparison.OrdinalIgnoreCase))
        {
            finalSources.Add(src);
        }
    }

    // Fallback citation: if no explicit citation exists but the answer is context-backed, include the top match if score is high
    if (finalSources.Count == 0 && relevantMatches.Count > 0 &&
        !answer.Contains("general knowledge", StringComparison.OrdinalIgnoreCase) &&
        relevantMatches[0].Similarity >= 0.45f)
    {
        finalSources.Add(relevantMatches[0].Chunk.SourceFile);
    }

    return Results.Ok(new ChatResponse
    {
        Answer = answer,
        Sources = finalSources
    });
})
.WithName("Chat");

// Trigger document ingestion in the background on startup
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var ingestion = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();
    var settings = scope.ServiceProvider.GetRequiredService<IOptions<RagSettings>>();
    try
    {
        Console.WriteLine("[Startup] Triggering initial document ingestion...");
        await ingestion.IngestFolderAsync(settings.Value.DocsFolder);
        Console.WriteLine("[Startup] Initial document ingestion completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Initial document ingestion failed: {ex.Message}");
    }
});

app.Run();
