namespace RagService.Core.DTOs
{
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public int Limit { get; set; } = 3;
    }
}
