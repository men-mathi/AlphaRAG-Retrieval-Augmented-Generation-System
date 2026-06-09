using System.Threading.Tasks;

namespace RagService.Core.Interfaces
{
    public interface IDocumentIngestionService
    {
        Task IngestFolderAsync(string folderPath);
    }
}
