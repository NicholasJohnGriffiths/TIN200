using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class TaskService
    {
        private readonly ApplicationDbContext _context;

        public TaskService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<TaskItem>> GetAllAsync(TaskItemStatus? statusFilter = null)
        {
            var query = _context.TaskItems.AsQueryable();

            if (statusFilter.HasValue)
            {
                query = query.Where(t => (t.Status ?? TaskItemStatus.Active) == statusFilter.Value);
            }

            return await query
                .OrderByDescending(t => t.CreatedDatetime ?? DateTime.MinValue)
                .ThenByDescending(t => t.Id)
                .ToListAsync();
        }

        public async Task<TaskItem?> GetByIdAsync(int id)
        {
            return await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TaskItem> CreateAsync(TaskItem record)
        {
            NormalizeRecord(record);
            _context.TaskItems.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<TaskItem> UpdateAsync(TaskItem record)
        {
            var existing = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == record.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Task with ID {record.Id} was not found.");
            }

            existing.CreatedBy = record.CreatedBy;
            existing.CreatedDatetime = record.CreatedDatetime;
            existing.Status = record.Status ?? TaskItemStatus.Active;
            existing.Title = record.Title;
            existing.Description = record.Description;
            existing.CompletedBy = record.CompletedBy;
            existing.CompletedDatetime = record.CompletedDatetime;

            NormalizeRecord(existing);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task ArchiveAsync(int id)
        {
            var record = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (record == null)
            {
                return;
            }

            record.Status = TaskItemStatus.Archived;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await ArchiveAsync(id);
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.TaskItems.AnyAsync(t => t.Id == id);
        }

        public async Task<TaskItem> CreateSurveyLinkRequestedTaskAsync(int companyId, string companyName, int financialYear, string reason, string? createdBy = null)
        {
            return await CreateAsync(new TaskItem
            {
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "System" : createdBy,
                CreatedDatetime = DateTime.Now,
                Status = TaskItemStatus.Active,
                Title = $"Survey link requested - {companyName}",
                Description = $"A new survey link was requested for {companyName} (Company ID {companyId}) for survey year {financialYear}. Reason: {reason}."
            });
        }

        public async Task<TaskItem> CreateSurveySubmittedTaskAsync(int companyId, string companyName, int financialYear, string? createdBy = null)
        {
            return await CreateAsync(new TaskItem
            {
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "System" : createdBy,
                CreatedDatetime = DateTime.Now,
                Status = TaskItemStatus.Active,
                Title = $"Survey submitted - {companyName}",
                Description = $"The survey for {companyName} (Company ID {companyId}) was submitted for survey year {financialYear}. Review and follow up if needed."
            });
        }

        private static void NormalizeRecord(TaskItem record)
        {
            record.Status ??= TaskItemStatus.Active;
            record.CreatedDatetime ??= DateTime.Now;

            if (record.Status == TaskItemStatus.Completed && !record.CompletedDatetime.HasValue)
            {
                record.CompletedDatetime = DateTime.Now;
            }
        }
    }
}
