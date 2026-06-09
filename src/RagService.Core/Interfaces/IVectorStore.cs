using System.Collections.Generic;
using System.Threading.Tasks;
using RagService.Core.Domain;

namespace RagService.Core.Interfaces
{
    public interface IVectorStore
    {
        Task AddChunksAsync(IEnumerable<Chunk> chunks);
        Task<List<SearchResult>> SearchSimilarChunksAsync(float[] queryEmbedding, int limit);
        Task ClearAsync();
    }
}
