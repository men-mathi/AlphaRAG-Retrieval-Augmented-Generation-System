namespace RagService.Core.Domain
{
    public class Chunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int ChunkIndex { get; set; }
    }
}
