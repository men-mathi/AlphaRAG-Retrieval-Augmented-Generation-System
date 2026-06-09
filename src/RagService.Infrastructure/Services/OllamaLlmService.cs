using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RagService.Core.Interfaces;
using RagService.Infrastructure.Configuration;

namespace RagService.Infrastructure.Services
{
    public class OllamaLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly RagSettings _settings;

        public OllamaLlmService(HttpClient httpClient, IOptions<RagSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<string> GenerateCompletionAsync(string prompt, string systemInstruction = "")
        {
            var baseUrl = _settings.Ollama.BaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/api/chat";

            var messages = new List<OllamaChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                messages.Add(new OllamaChatMessage { Role = "system", Content = systemInstruction });
            }
            messages.Add(new OllamaChatMessage { Role = "user", Content = prompt });

            var request = new OllamaChatRequest
            {
                Model = _settings.Ollama.LlmModel,
                Messages = messages,
                Stream = false
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
                return result?.Message?.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to fetch Ollama completion: {ex.Message}. Returning fallback message.");
                return SmartFallbackGenerator.GenerateSmartFallback(prompt, "Ollama", _settings.Ollama.LlmModel, _settings.Ollama.BaseUrl);
            }
        }

        private class OllamaChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<OllamaChatMessage> Messages { get; set; } = new();

            [JsonPropertyName("stream")]
            public bool Stream { get; set; } = false;
        }

        private class OllamaChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class OllamaChatResponse
        {
            [JsonPropertyName("message")]
            public OllamaChatMessage? Message { get; set; }
        }
    }
}
