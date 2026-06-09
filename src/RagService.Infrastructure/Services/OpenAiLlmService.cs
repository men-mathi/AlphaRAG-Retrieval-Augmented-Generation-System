using System;
using System.Collections.Generic;
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
    public class OpenAiLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly RagSettings _settings;

        public OpenAiLlmService(HttpClient httpClient, IOptions<RagSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<string> GenerateCompletionAsync(string prompt, string systemInstruction = "")
        {
            if (string.IsNullOrWhiteSpace(_settings.OpenAi.ApiKey) || _settings.OpenAi.ApiKey == "YOUR_OPENAI_API_KEY")
            {
                Console.WriteLine("[Warning] OpenAI API key is missing or default. Returning fallback response.");
                return SmartFallbackGenerator.GenerateSmartFallback(prompt, "OpenAI", _settings.OpenAi.LlmModel, "https://api.openai.com/v1");
            }

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenAi.ApiKey);

            var messages = new List<OpenAiChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                messages.Add(new OpenAiChatMessage { Role = "system", Content = systemInstruction });
            }
            messages.Add(new OpenAiChatMessage { Role = "user", Content = prompt });

            var payload = new OpenAiChatRequest
            {
                Model = _settings.OpenAi.LlmModel,
                Messages = messages,
                Stream = false
            };

            requestMessage.Content = JsonContent.Create(payload);

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>();
                if (result?.Choices != null && result.Choices.Length > 0)
                {
                    return result.Choices[0].Message?.Content ?? string.Empty;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to fetch OpenAI completion: {ex.Message}. Returning fallback message.");
                return SmartFallbackGenerator.GenerateSmartFallback(prompt, "OpenAI", _settings.OpenAi.LlmModel, "https://api.openai.com/v1");
            }
        }

        private class OpenAiChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<OpenAiChatMessage> Messages { get; set; } = new();

            [JsonPropertyName("stream")]
            public bool Stream { get; set; } = false;
        }

        private class OpenAiChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class OpenAiChatResponse
        {
            [JsonPropertyName("choices")]
            public OpenAiChatChoice[] Choices { get; set; } = Array.Empty<OpenAiChatChoice>();
        }

        private class OpenAiChatChoice
        {
            [JsonPropertyName("message")]
            public OpenAiChatMessage? Message { get; set; }
        }
    }
}
