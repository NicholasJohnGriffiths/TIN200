using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;
using System.Threading;

namespace TINWeb.Services
{
    public class QuestionGroupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IImageStorageService _imageStorageService;
        private const string EntityTypeName = "questiongroup";
        private static readonly SemaphoreSlim SchemaLock = new(1, 1);
        private static bool _schemaEnsured;

        public QuestionGroupService(ApplicationDbContext context, IImageStorageService imageStorageService)
        {
            _context = context;
            _imageStorageService = imageStorageService;
        }

        public async Task<List<QuestionGroup>> GetAllAsync()
        {
            await EnsureSchemaAsync();

            return await _context.QuestionGroup
                .OrderBy(x => x.OrderNumber ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<QuestionGroup?> GetByIdAsync(int id)
        {
            await EnsureSchemaAsync();
            return await _context.QuestionGroup.FindAsync(id);
        }

        public async Task<QuestionGroup> CreateAsync(QuestionGroup record, IFormFile? image1File, IFormFile? image2File, IFormFile? image3File)
        {
            await EnsureSchemaAsync();

            _context.QuestionGroup.Add(record);
            await _context.SaveChangesAsync();

            record.ImageId1 = await SaveImageAsync(record.Id, image1File);
            record.ImageId2 = await SaveImageAsync(record.Id, image2File);
            record.ImageId3 = await SaveImageAsync(record.Id, image3File);

            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<QuestionGroup> UpdateAsync(
            QuestionGroup record,
            IFormFile? image1File,
            IFormFile? image2File,
            IFormFile? image3File,
            bool clearImage1,
            bool clearImage2,
            bool clearImage3)
        {
            await EnsureSchemaAsync();

            var existing = await _context.QuestionGroup.FirstOrDefaultAsync(x => x.Id == record.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"QuestionGroup with ID {record.Id} was not found.");
            }

            existing.OrderNumber = record.OrderNumber;
            existing.Title = record.Title;
            existing.Description = record.Description;

            if (clearImage1)
            {
                await RemoveImageAsync(existing.ImageId1);
                existing.ImageId1 = null;
            }
            else if (image1File != null && image1File.Length > 0)
            {
                await RemoveImageAsync(existing.ImageId1);
                existing.ImageId1 = await SaveImageAsync(existing.Id, image1File);
            }

            if (clearImage2)
            {
                await RemoveImageAsync(existing.ImageId2);
                existing.ImageId2 = null;
            }
            else if (image2File != null && image2File.Length > 0)
            {
                await RemoveImageAsync(existing.ImageId2);
                existing.ImageId2 = await SaveImageAsync(existing.Id, image2File);
            }

            if (clearImage3)
            {
                await RemoveImageAsync(existing.ImageId3);
                existing.ImageId3 = null;
            }
            else if (image3File != null && image3File.Length > 0)
            {
                await RemoveImageAsync(existing.ImageId3);
                existing.ImageId3 = await SaveImageAsync(existing.Id, image3File);
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task DeleteAsync(int id)
        {
            await EnsureSchemaAsync();

            var record = await GetByIdAsync(id);
            if (record != null)
            {
                await RemoveImageAsync(record.ImageId1);
                await RemoveImageAsync(record.ImageId2);
                await RemoveImageAsync(record.ImageId3);
                _context.QuestionGroup.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            await EnsureSchemaAsync();
            return await _context.QuestionGroup.AnyAsync(x => x.Id == id);
        }

        public async Task<Image?> GetImageByIdAsync(int imageId)
        {
            await EnsureSchemaAsync();
            return await _context.Image.FirstOrDefaultAsync(x => x.Id == imageId);
        }

        public async Task<Dictionary<int, Image>> GetImagesByIdsAsync(IEnumerable<int> imageIds)
        {
            await EnsureSchemaAsync();

            var ids = imageIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, Image>();
            }

            return await _context.Image
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);
        }

        private async Task EnsureSchemaAsync()
        {
            if (_schemaEnsured)
            {
                return;
            }

            await SchemaLock.WaitAsync();
            try
            {
                if (_schemaEnsured)
                {
                    return;
                }

                await _context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[Image]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Image]
    (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EntityType] VARCHAR(50) NOT NULL,
        [EntityId] INT NOT NULL,
        [FileName] VARCHAR(255) NOT NULL,
        [FilePath] VARCHAR(500) NOT NULL,
        [FileType] VARCHAR(50) NOT NULL,
        [FileSize] INT NULL,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END;

IF COL_LENGTH('dbo.QuestionGroup', 'ImageId1') IS NULL
BEGIN
    ALTER TABLE [dbo].[QuestionGroup] ADD [ImageId1] [int] NULL;
END;

IF COL_LENGTH('dbo.QuestionGroup', 'ImageId2') IS NULL
BEGIN
    ALTER TABLE [dbo].[QuestionGroup] ADD [ImageId2] [int] NULL;
END;

IF COL_LENGTH('dbo.QuestionGroup', 'ImageId3') IS NULL
BEGIN
    ALTER TABLE [dbo].[QuestionGroup] ADD [ImageId3] [int] NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QuestionGroup_Image1')
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD CONSTRAINT [FK_QuestionGroup_Image1] FOREIGN KEY ([ImageId1]) REFERENCES [dbo].[Image]([Id]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QuestionGroup_Image2')
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD CONSTRAINT [FK_QuestionGroup_Image2] FOREIGN KEY ([ImageId2]) REFERENCES [dbo].[Image]([Id]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_QuestionGroup_Image3')
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD CONSTRAINT [FK_QuestionGroup_Image3] FOREIGN KEY ([ImageId3]) REFERENCES [dbo].[Image]([Id]);
END;
");

                _schemaEnsured = true;
            }
            finally
            {
                SchemaLock.Release();
            }
        }

        private async Task<int?> SaveImageAsync(int entityId, IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var extension = Path.GetExtension(file.FileName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            var storagePath = await _imageStorageService.SaveImageAsync(file, EntityTypeName, entityId);

            var image = new Image
            {
                EntityType = EntityTypeName,
                EntityId = entityId,
                FileName = file.FileName,
                FilePath = storagePath,
                FileType = extension.TrimStart('.').ToLowerInvariant(),
                FileSize = file.Length > int.MaxValue ? int.MaxValue : (int)file.Length,
                CreatedDate = DateTime.UtcNow
            };

            _context.Image.Add(image);
            await _context.SaveChangesAsync();

            return image.Id;
        }

        private async Task RemoveImageAsync(int? imageId)
        {
            if (!imageId.HasValue)
            {
                return;
            }

            var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == imageId.Value);
            if (image == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(image.FilePath))
            {
                await _imageStorageService.DeleteIfExistsAsync(image.FilePath);
            }

            _context.Image.Remove(image);
        }
    }
}
