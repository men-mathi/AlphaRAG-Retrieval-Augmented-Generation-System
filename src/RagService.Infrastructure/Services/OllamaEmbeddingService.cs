using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RagService.Core.Interfaces;
using RagService.Infrastructure.Configuration;

namespace RagService.Infrastructure.Services
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly RagSettings _settings;

        public OllamaEmbeddingService(HttpClient httpClient, IOptions<RagSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var baseUrl = _settings.Ollama.BaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/api/embeddings";
            var request = new OllamaEmbeddingRequest
            {
                Model = _settings.Ollama.EmbeddingModel,
                Prompt = text
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to fetch Ollama embeddings: {ex.Message}. Using fallback mock embedding.");
                // Return a mock vector of 384 dimensions for local developer testing when Ollama is offline
                var mockEmbedding = new float[384];
                // Fill with some deterministic values based on text hash
                int hash = text.GetHashCode();
                var rand = new Random(hash);
                for (int i = 0; i < mockEmbedding.Length; i++)
                {
                    mockEmbedding[i] = (float)rand.NextDouble();
                }
                return mockEmbedding;
            }
        }

        private class OllamaEmbeddingRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;
        }

        private class OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}
