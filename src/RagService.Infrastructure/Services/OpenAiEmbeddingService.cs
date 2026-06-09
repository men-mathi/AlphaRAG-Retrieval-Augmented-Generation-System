using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RagService.Core.Interfaces;
using RagService.Infrastructure.Configuration;

namespace RagService.Infrastructure.Services
{
    public class OpenAiEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly RagSettings _settings;

        public OpenAiEmbeddingService(HttpClient httpClient, IOptions<RagSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(_settings.OpenAi.ApiKey) || _settings.OpenAi.ApiKey == "YOUR_OPENAI_API_KEY")
            {
                Console.WriteLine("[Warning] OpenAI API key is missing or default. Using fallback mock embedding.");
                return GetMockEmbedding(text);
            }

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenAi.ApiKey);

            var payload = new OpenAiEmbeddingRequest
            {
                Input = text,
                Model = _settings.OpenAi.EmbeddingModel
            };

            requestMessage.Content = JsonContent.Create(payload);

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>();
                if (result?.Data != null && result.Data.Length > 0)
                {
                    return result.Data[0].Embedding;
                }
                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to fetch OpenAI embeddings: {ex.Message}. Using fallback mock embedding.");
                return GetMockEmbedding(text);
            }
        }

        private float[] GetMockEmbedding(string text)
        {
            var mockEmbedding = new float[1536]; // OpenAI default dimension
            int hash = text.GetHashCode();
            var rand = new Random(hash);
            for (int i = 0; i < mockEmbedding.Length; i++)
            {
                mockEmbedding[i] = (float)rand.NextDouble();
            }
            return mockEmbedding;
        }

        private class OpenAiEmbeddingRequest
        {
            [JsonPropertyName("input")]
            public string Input { get; set; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;
        }

        private class OpenAiEmbeddingResponse
        {
            [JsonPropertyName("data")]
            public OpenAiEmbeddingData[] Data { get; set; } = Array.Empty<OpenAiEmbeddingData>();
        }

        private class OpenAiEmbeddingData
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}
