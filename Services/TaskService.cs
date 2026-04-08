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
            await ApplyPendingTasksDueForReactivationAsync();

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
            await ApplyPendingTasksDueForReactivationAsync();
            return await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TaskItem> CreateAsync(TaskItem record, string? actingUser = null)
        {
            NormalizeRecord(record, actingUser, statusChanged: true);
            _context.TaskItems.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<TaskItem> UpdateAsync(TaskItem record, string? actingUser = null)
        {
            var existing = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == record.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Task with ID {record.Id} was not found.");
            }

            var previousStatus = existing.Status ?? TaskItemStatus.Active;
            var newStatus = record.Status ?? TaskItemStatus.Active;

            existing.CreatedBy = record.CreatedBy;
            existing.CreatedDatetime = record.CreatedDatetime;
            existing.Status = newStatus;
            existing.Title = record.Title;
            existing.Description = record.Description;
            existing.SetBackToActiveDate = record.SetBackToActiveDate?.Date;
            existing.CompletedBy = record.CompletedBy;
            existing.CompletedDatetime = record.CompletedDatetime;

            NormalizeRecord(existing, actingUser, statusChanged: previousStatus != newStatus);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task ArchiveAsync(int id, string? actingUser = null)
        {
            var record = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (record == null)
            {
                return;
            }

            record.Status = TaskItemStatus.Archived;
            NormalizeRecord(record, actingUser, statusChanged: true);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateStatusAsync(int id, TaskItemStatus newStatus, string? actingUser = null)
        {
            var record = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (record == null)
            {
                return;
            }

            if ((record.Status ?? TaskItemStatus.Active) == newStatus)
            {
                return;
            }

            record.Status = newStatus;
            NormalizeRecord(record, actingUser, statusChanged: true);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, string? actingUser = null)
        {
            await ArchiveAsync(id, actingUser);
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

        public async Task<TaskItem> CreateSurveyEmailBounceTaskAsync(
            int companyId,
            string companyName,
            int financialYear,
            string recipientEmail,
            string status,
            string? reason,
            string? eventId = null,
            string? createdBy = null)
        {
            var title = $"Survey email bounced - {companyName}";
            TaskItem? existingTask;

            if (!string.IsNullOrWhiteSpace(eventId))
            {
                existingTask = await _context.TaskItems.FirstOrDefaultAsync(t =>
                    (t.Status ?? TaskItemStatus.Active) == TaskItemStatus.Active
                    && t.Title == title
                    && t.Description != null
                    && t.Description.Contains($"Event ID {eventId}"));
            }
            else
            {
                existingTask = await _context.TaskItems.FirstOrDefaultAsync(t =>
                    (t.Status ?? TaskItemStatus.Active) == TaskItemStatus.Active
                    && t.Title == title
                    && t.Description != null
                    && t.Description.Contains(recipientEmail));
            }

            if (existingTask != null)
            {
                return existingTask;
            }

            var safeReason = string.IsNullOrWhiteSpace(reason)
                ? "No additional delivery details were provided."
                : reason.Trim();

            var description = $"The survey email to {recipientEmail} for {companyName} (Company ID {companyId}) bounced back for survey year {financialYear}. Status: {status}. Reason: {safeReason}.";
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                description += $" Event ID {eventId}.";
            }

            return await CreateAsync(new TaskItem
            {
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "System" : createdBy,
                CreatedDatetime = DateTime.Now,
                Status = TaskItemStatus.Active,
                Title = title,
                Description = description
            });
        }

        private async Task ApplyPendingTasksDueForReactivationAsync()
        {
            var today = DateTime.Today;
            var dueTasks = await _context.TaskItems
                .Where(t => (t.Status ?? TaskItemStatus.Active) == TaskItemStatus.Pending
                    && t.SetBackToActiveDate.HasValue
                    && t.SetBackToActiveDate.Value.Date <= today)
                .ToListAsync();

            if (!dueTasks.Any())
            {
                return;
            }

            foreach (var task in dueTasks)
            {
                task.Status = TaskItemStatus.Active;
                NormalizeRecord(task, statusChanged: true);
            }

            await _context.SaveChangesAsync();
        }

        private static void NormalizeRecord(TaskItem record, string? actingUser = null, bool statusChanged = false)
        {
            record.Status ??= TaskItemStatus.Active;
            record.CreatedDatetime ??= DateTime.Now;
            record.SetBackToActiveDate = record.SetBackToActiveDate?.Date;

            if (string.IsNullOrWhiteSpace(record.CreatedBy))
            {
                record.CreatedBy = string.IsNullOrWhiteSpace(actingUser) ? "Admin" : actingUser;
            }

            if (statusChanged || !record.StatusChangeDatetime.HasValue)
            {
                record.StatusChangeDatetime = DateTime.Now;
            }

            if (record.Status == TaskItemStatus.Completed)
            {
                if (!record.CompletedDatetime.HasValue)
                {
                    record.CompletedDatetime = DateTime.Now;
                }

                if (string.IsNullOrWhiteSpace(record.CompletedBy) && !string.IsNullOrWhiteSpace(actingUser))
                {
                    record.CompletedBy = actingUser;
                }

                record.SetBackToActiveDate = null;
                return;
            }

            record.CompletedBy = null;
            record.CompletedDatetime = null;

            if (record.Status != TaskItemStatus.Pending)
            {
                record.SetBackToActiveDate = null;
            }
        }
    }
}
