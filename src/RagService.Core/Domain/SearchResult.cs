namespace RagService.Core.Domain
{
    public class SearchResult
    {
        public Chunk Chunk { get; set; } = null!;
        public float Similarity { get; set; }
    }
}
