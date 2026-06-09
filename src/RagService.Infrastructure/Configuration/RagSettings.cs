namespace RagService.Infrastructure.Configuration
{
    public class RagSettings
    {
        public string LlmProvider { get; set; } = "Ollama"; // "Ollama" or "OpenAI"
        public string EmbeddingProvider { get; set; } = "Ollama"; // "Ollama" or "OpenAI"

        public OpenAiSettings OpenAi { get; set; } = new();
        public OllamaSettings Ollama { get; set; } = new();

        public string DocsFolder { get; set; } = "docs";
        public int ChunkSize { get; set; } = 500;
        public int ChunkOverlap { get; set; } = 100;
    }

    public class OpenAiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string LlmModel { get; set; } = "gpt-4o-mini";
        public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    }

    public class OllamaSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string LlmModel { get; set; } = "llama3";
        public string EmbeddingModel { get; set; } = "nomic-embed-text";
    }
}
