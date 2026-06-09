using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RagService.Core.Domain;
using RagService.Core.Interfaces;
using RagService.Infrastructure.Configuration;

namespace RagService.Infrastructure.Services
{
    public class MarkdownIngestionService : IDocumentIngestionService
    {
        private readonly IVectorStore _vectorStore;
        private readonly IEmbeddingService _embeddingService;
        private readonly RagSettings _settings;

        public MarkdownIngestionService(
            IVectorStore vectorStore,
            IEmbeddingService embeddingService,
            IOptions<RagSettings> settings)
        {
            _vectorStore = vectorStore;
            _embeddingService = embeddingService;
            _settings = settings.Value;
        }

        public async Task IngestFolderAsync(string folderPath)
        {
            var targetPath = folderPath;
            if (!Path.IsPathRooted(targetPath))
            {
                var currentDir = Directory.GetCurrentDirectory();
                var candidate = Path.GetFullPath(Path.Combine(currentDir, folderPath));
                if (Directory.Exists(candidate))
                {
                    targetPath = candidate;
                }
                else
                {
                    // Try parent folder
                    candidate = Path.GetFullPath(Path.Combine(currentDir, "..", folderPath));
                    if (Directory.Exists(candidate))
                    {
                        targetPath = candidate;
                    }
                    else
                    {
                        // Try grandparent folder
                        candidate = Path.GetFullPath(Path.Combine(currentDir, "..", "..", folderPath));
                        if (Directory.Exists(candidate))
                        {
                            targetPath = candidate;
                        }
                    }
                }
            }

            Console.WriteLine($"[Info] Ingesting folder: {targetPath}");

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
                Console.WriteLine($"Created directory: {targetPath}");
                return;
            }

            // Clear vector store before indexing
            await _vectorStore.ClearAsync();

            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                ".md", ".txt", ".pdf", ".docx", ".pptx", ".xlsx", ".csv", ".html", ".htm" 
            };

            var files = Directory.GetFiles(targetPath, "*.*", SearchOption.AllDirectories)
                .Where(file => extensions.Contains(Path.GetExtension(file)))
                .ToList();

            Console.WriteLine($"Found {files.Count} documents (PDF/Word/PowerPoint/Excel/CSV/HTML/Text) in {targetPath}");

            foreach (var file in files)
            {
                var content = await DocumentParser.ExtractTextAsync(file);
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                var fileName = Path.GetFileName(file);
                
                var chunks = ChunkText(content, fileName, _settings.ChunkSize, _settings.ChunkOverlap);
                Console.WriteLine($"Chunked {fileName} into {chunks.Count} chunks.");

                foreach (var chunk in chunks)
                {
                    chunk.Embedding = await _embeddingService.GetEmbeddingAsync(chunk.Content);
                }

                await _vectorStore.AddChunksAsync(chunks);
                Console.WriteLine($"Indexed {chunks.Count} chunks for {fileName}");
            }
        }

        private List<Chunk> ChunkText(string content, string fileName, int chunkSize, int chunkOverlap)
        {
            var chunks = new List<Chunk>();
            var normalizedContent = content.Replace("\r\n", "\n");

            // Split content by paragraphs (double newlines)
            var paragraphs = Regex.Split(normalizedContent, @"\n{2,}");

            var currentChunkBuilder = new StringBuilder();
            int chunkIndex = 0;
            var overlapParagraphs = new Queue<string>();

            foreach (var para in paragraphs)
            {
                var trimmedPara = para.Trim();
                if (string.IsNullOrEmpty(trimmedPara))
                    continue;

                // Force split extremely long paragraphs
                if (trimmedPara.Length > chunkSize && currentChunkBuilder.Length == 0)
                {
                    var parts = SplitLongParagraph(trimmedPara, chunkSize);
                    foreach (var part in parts)
                    {
                        chunks.Add(new Chunk
                        {
                            Content = part,
                            SourceFile = fileName,
                            ChunkIndex = chunkIndex++
                        });
                    }
                    continue;
                }

                if (currentChunkBuilder.Length + trimmedPara.Length > chunkSize)
                {
                    // Flush current chunk
                    chunks.Add(new Chunk
                    {
                        Content = currentChunkBuilder.ToString().Trim(),
                        SourceFile = fileName,
                        ChunkIndex = chunkIndex++
                    });

                    currentChunkBuilder.Clear();

                    // Add overlap content from the queue
                    foreach (var oPara in overlapParagraphs)
                    {
                        currentChunkBuilder.AppendLine(oPara);
                        currentChunkBuilder.AppendLine();
                    }
                }

                currentChunkBuilder.AppendLine(trimmedPara);
                currentChunkBuilder.AppendLine();

                // Track paragraphs for overlap
                overlapParagraphs.Enqueue(trimmedPara);
                if (overlapParagraphs.Count > 1)
                {
                    overlapParagraphs.Dequeue();
                }
            }

            // Flush remaining chunk
            if (currentChunkBuilder.Length > 0)
            {
                chunks.Add(new Chunk
                {
                    Content = currentChunkBuilder.ToString().Trim(),
                    SourceFile = fileName,
                    ChunkIndex = chunkIndex++
                });
            }

            return chunks;
        }

        private List<string> SplitLongParagraph(string para, int size)
        {
            var list = new List<string>();
            int start = 0;
            while (start < para.Length)
            {
                int len = Math.Min(size, para.Length - start);
                list.Add(para.Substring(start, len));
                start += len;
            }
            return list;
        }
    }
}
