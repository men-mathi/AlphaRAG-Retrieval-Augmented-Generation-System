using System.Threading.Tasks;

namespace RagService.Core.Interfaces
{
    public interface ILlmService
    {
        Task<string> GenerateCompletionAsync(string prompt, string systemInstruction = "");
    }
}
