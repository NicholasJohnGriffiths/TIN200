namespace TINWeb.Services
{
    public interface IImageStorageService
    {
        Task<string> SaveImageAsync(IFormFile file, string entityType, int entityId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default);
        Task<Stream?> OpenReadAsync(string filePath, CancellationToken cancellationToken = default);
        Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
