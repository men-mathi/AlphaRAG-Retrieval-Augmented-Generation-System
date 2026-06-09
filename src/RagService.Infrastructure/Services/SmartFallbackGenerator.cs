using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RagService.Infrastructure.Services
{
    public static class SmartFallbackGenerator
    {
        public static string GenerateSmartFallback(string prompt, string providerName, string modelName, string connectionDetails)
        {
            // 1. Extract User Question from prompt template
            string question = "";
            int questionIndex = prompt.IndexOf("User Question:");
            if (questionIndex != -1)
            {
                int endIndex = prompt.IndexOf("Formulate your answer based on context:", questionIndex);
                if (endIndex != -1)
                {
                    question = prompt.Substring(questionIndex + 14, endIndex - (questionIndex + 14)).Trim();
                }
                else
                {
                    question = prompt.Substring(questionIndex + 14).Trim();
                }
            }

            // 2. Extract Context details from prompt template
            string context = "";
            int contextIndex = prompt.IndexOf("Context details:");
            if (contextIndex != -1 && questionIndex != -1 && questionIndex > contextIndex)
            {
                context = prompt.Substring(contextIndex + 16, questionIndex - (contextIndex + 16)).Trim();
            }

            // 3. Fallback for standard greetings
            string lowerQuestion = question.ToLowerInvariant().Trim('?', '.', '!', ' ');
            if (lowerQuestion == "hi" || lowerQuestion == "hello" || lowerQuestion == "hey" || lowerQuestion == "greetings" || lowerQuestion == "welcome")
            {
                return $"Hello! I am Team Alpha's AI assistant representing Team Alpha. I'm here to answer questions based on our local documentation. How can I help you today?\n\n*(Note: Running in offline fallback mode; {providerName} client could not reach model '{modelName}' at {connectionDetails})*";
            }

            // 4. Fallback search inside context using keyword matching
            if (!string.IsNullOrEmpty(context) && !string.IsNullOrEmpty(question))
            {
                var lines = context.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var paragraphs = new List<string>();
                var currentPara = new StringBuilder();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (currentPara.Length > 0)
                        {
                            paragraphs.Add(currentPara.ToString().Trim());
                            currentPara.Clear();
                        }
                    }
                    else
                    {
                        currentPara.AppendLine(line);
                    }
                }
                if (currentPara.Length > 0)
                {
                    paragraphs.Add(currentPara.ToString().Trim());
                }

                // Tokenize query into searchable keywords (filtering out short grammar words)
                var keywords = question.Split(new[] { ' ', '?', '.', ',', '!', ';', ':', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 3)
                    .Select(w => w.ToLowerInvariant())
                    .ToList();

                string? bestMatch = null;
                int maxMatches = 0;

                foreach (var para in paragraphs)
                {
                    if (para.StartsWith("[Source:") && para.Length < 30)
                        continue; // Skip source tag headings that have no body

                    int matchCount = 0;
                    string lowerPara = para.ToLowerInvariant();
                    foreach (var kw in keywords)
                    {
                        if (lowerPara.Contains(kw))
                        {
                            matchCount++;
                        }
                    }

                    if (matchCount > maxMatches)
                    {
                        maxMatches = matchCount;
                        bestMatch = para;
                    }
                }

                if (bestMatch != null && maxMatches > 0)
                {
                    // Found a relevant paragraph chunk
                    return $"{bestMatch}\n\n*(Note: This response was generated using keyword matching on local document context in offline fallback mode because {providerName} ({modelName}) at {connectionDetails} was unreachable)*";
                }
            }

            // 5. Default Response if no keywords match context
            string query = string.IsNullOrWhiteSpace(question) ? "your question" : $"\"{question}\"";
            return $"I searched the documents for {query} but could not generate a direct match in offline mode.\n\nTo use full generative AI, please make sure your local {providerName} service is active (model '{modelName}' at {connectionDetails}) or configure the cloud service settings in `appsettings.json`.";
        }
    }
}
