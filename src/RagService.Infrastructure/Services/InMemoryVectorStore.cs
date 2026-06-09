using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RagService.Core.Domain;
using RagService.Core.Interfaces;

namespace RagService.Infrastructure.Services
{
    public class InMemoryVectorStore : IVectorStore
    {
        private readonly ConcurrentBag<Chunk> _chunks = new();

        public Task AddChunksAsync(IEnumerable<Chunk> chunks)
        {
            foreach (var chunk in chunks)
            {
                _chunks.Add(chunk);
            }
            return Task.CompletedTask;
        }

        public Task<List<SearchResult>> SearchSimilarChunksAsync(float[] queryEmbedding, int limit)
        {
            var results = new List<SearchResult>();

            if (queryEmbedding == null || queryEmbedding.Length == 0)
                return Task.FromResult(results);

            foreach (var chunk in _chunks)
            {
                if (chunk.Embedding == null || chunk.Embedding.Length == 0)
                    continue;

                float score = CalculateCosineSimilarity(queryEmbedding, chunk.Embedding);
                results.Add(new SearchResult
                {
                    Chunk = chunk,
                    Similarity = score
                });
            }

            var topResults = results
                .OrderByDescending(r => r.Similarity)
                .Take(limit)
                .ToList();

            return Task.FromResult(topResults);
        }

        public Task ClearAsync()
        {
            _chunks.Clear();
            return Task.CompletedTask;
        }

        private float CalculateCosineSimilarity(float[] vecA, float[] vecB)
        {
            if (vecA.Length != vecB.Length)
                return 0.0f;

            float dotProduct = 0.0f;
            float normA = 0.0f;
            float normB = 0.0f;

            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }

            if (normA == 0.0f || normB == 0.0f)
                return 0.0f;

            return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }
    }
}
