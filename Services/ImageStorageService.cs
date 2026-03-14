using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace TINWeb.Services
{
    public class ImageStorageService : IImageStorageService
    {
        private const string BlobPrefix = "azblob://";
        private readonly IWebHostEnvironment _environment;
        private readonly string? _blobConnectionString;
        private readonly string _blobContainerName;

        public ImageStorageService(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _blobConnectionString = configuration["ImageStorage:BlobConnectionString"];
            _blobContainerName = configuration["ImageStorage:BlobContainerName"] ?? "images";
        }

        public async Task<string> SaveImageAsync(IFormFile file, string entityType, int entityId, CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            var generatedFileName = $"{entityType}_{entityId}_{Guid.NewGuid():N}{extension}";

            if (IsBlobEnabled())
            {
                var blobName = $"{entityType}/{entityId}/{generatedFileName}";
                var containerClient = await GetContainerClientAsync(cancellationToken);
                var blobClient = containerClient.GetBlobClient(blobName);

                await using var uploadStream = file.OpenReadStream();
                await blobClient.UploadAsync(
                    uploadStream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType }
                    },
                    cancellationToken);

                return $"{BlobPrefix}{_blobContainerName}/{blobName}";
            }

            var imagesFolderPath = Path.Combine(_environment.ContentRootPath, "Images");
            Directory.CreateDirectory(imagesFolderPath);

            var physicalFilePath = Path.Combine(imagesFolderPath, generatedFileName);
            await using (var stream = System.IO.File.Create(physicalFilePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            return Path.Combine("Images", generatedFileName).Replace("\\", "/");
        }

        public async Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            if (TryParseBlobPath(filePath, out var containerName, out var blobName))
            {
                if (!IsBlobEnabled())
                {
                    return false;
                }

                var blobClient = GetBlobClient(containerName, blobName);
                var exists = await blobClient.ExistsAsync(cancellationToken);
                return exists.Value;
            }

            var fullPath = ResolveLocalPath(filePath);
            return System.IO.File.Exists(fullPath);
        }

        public async Task<Stream?> OpenReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!await ExistsAsync(filePath, cancellationToken))
            {
                return null;
            }

            if (TryParseBlobPath(filePath, out var containerName, out var blobName))
            {
                var blobClient = GetBlobClient(containerName, blobName);
                return await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
            }

            var fullPath = ResolveLocalPath(filePath);
            return System.IO.File.OpenRead(fullPath);
        }

        public async Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (TryParseBlobPath(filePath, out var containerName, out var blobName))
            {
                if (!IsBlobEnabled())
                {
                    return;
                }

                var blobClient = GetBlobClient(containerName, blobName);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                return;
            }

            var fullPath = ResolveLocalPath(filePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private bool IsBlobEnabled()
        {
            return !string.IsNullOrWhiteSpace(_blobConnectionString);
        }

        private async Task<BlobContainerClient> GetContainerClientAsync(CancellationToken cancellationToken)
        {
            var serviceClient = new BlobServiceClient(_blobConnectionString);
            var containerClient = serviceClient.GetBlobContainerClient(_blobContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            return containerClient;
        }

        private BlobClient GetBlobClient(string containerName, string blobName)
        {
            var serviceClient = new BlobServiceClient(_blobConnectionString);
            return serviceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        }

        private static bool TryParseBlobPath(string filePath, out string containerName, out string blobName)
        {
            containerName = string.Empty;
            blobName = string.Empty;

            if (!filePath.StartsWith(BlobPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var withoutPrefix = filePath.Substring(BlobPrefix.Length);
            var slashIndex = withoutPrefix.IndexOf('/');
            if (slashIndex <= 0 || slashIndex >= withoutPrefix.Length - 1)
            {
                return false;
            }

            containerName = withoutPrefix.Substring(0, slashIndex);
            blobName = withoutPrefix.Substring(slashIndex + 1);
            return true;
        }

        private string ResolveLocalPath(string filePath)
        {
            var normalizedRelativePath = filePath
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            return Path.Combine(_environment.ContentRootPath, normalizedRelativePath);
        }
    }
}
