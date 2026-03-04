using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Data;
using TINWorkspaceTemp.Models;

namespace TINWorkspaceTemp.Services
{
    public class QuestionService
    {
        private readonly ApplicationDbContext _context;

        public QuestionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Question>> GetAllAsync()
        {
            return await _context.Question
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id)
                .ToListAsync();
        }

        public async Task<Question?> GetByIdAsync(int id)
        {
            return await _context.Question.FindAsync(id);
        }

        public async Task<Question> CreateAsync(Question record)
        {
            var orderedQuestions = await _context.Question
                .OrderBy(q => q.OrderNumber ?? int.MaxValue)
                .ThenBy(q => q.Id)
                .ToListAsync();

            for (var index = 0; index < orderedQuestions.Count; index++)
            {
                orderedQuestions[index].OrderNumber = index + 1;
            }

            var maxInsertOrder = orderedQuestions.Count + 1;
            var requestedOrder = record.OrderNumber ?? maxInsertOrder;
            var insertOrder = Math.Max(1, Math.Min(requestedOrder, maxInsertOrder));

            foreach (var question in orderedQuestions.Where(q => (q.OrderNumber ?? 0) >= insertOrder))
            {
                question.OrderNumber = (question.OrderNumber ?? 0) + 1;
            }

            record.OrderNumber = insertOrder;
            _context.Question.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<Question> UpdateAsync(Question record)
        {
            var orderedQuestions = await _context.Question
                .OrderBy(q => q.OrderNumber ?? int.MaxValue)
                .ThenBy(q => q.Id)
                .ToListAsync();

            for (var index = 0; index < orderedQuestions.Count; index++)
            {
                orderedQuestions[index].OrderNumber = index + 1;
            }

            var existingRecord = orderedQuestions.FirstOrDefault(q => q.Id == record.Id);
            if (existingRecord == null)
            {
                throw new InvalidOperationException($"Question with ID {record.Id} was not found.");
            }

            var currentOrder = existingRecord.OrderNumber ?? 1;
            var maxOrder = orderedQuestions.Count;
            var requestedOrder = record.OrderNumber ?? currentOrder;
            var targetOrder = Math.Max(1, Math.Min(requestedOrder, maxOrder));

            if (targetOrder < currentOrder)
            {
                foreach (var question in orderedQuestions.Where(q => q.Id != existingRecord.Id && (q.OrderNumber ?? 0) >= targetOrder && (q.OrderNumber ?? 0) < currentOrder))
                {
                    question.OrderNumber = (question.OrderNumber ?? 0) + 1;
                }
            }
            else if (targetOrder > currentOrder)
            {
                foreach (var question in orderedQuestions.Where(q => q.Id != existingRecord.Id && (q.OrderNumber ?? 0) > currentOrder && (q.OrderNumber ?? 0) <= targetOrder))
                {
                    question.OrderNumber = (question.OrderNumber ?? 0) - 1;
                }
            }

            existingRecord.OrderNumber = targetOrder;
            existingRecord.Title = record.Title;
            existingRecord.Description = record.Description;
            existingRecord.QuestionText = record.QuestionText;
            existingRecord.AnswerType = record.AnswerType;
            existingRecord.Multi1 = record.Multi1;
            existingRecord.Multi2 = record.Multi2;
            existingRecord.Multi3 = record.Multi3;
            existingRecord.Multi4 = record.Multi4;
            existingRecord.Multi5 = record.Multi5;
            existingRecord.Multi6 = record.Multi6;
            existingRecord.Multi7 = record.Multi7;
            existingRecord.Multi8 = record.Multi8;

            await _context.SaveChangesAsync();
            return existingRecord;
        }

        public async Task DeleteAsync(int id)
        {
            var orderedQuestions = await _context.Question
                .OrderBy(q => q.OrderNumber ?? int.MaxValue)
                .ThenBy(q => q.Id)
                .ToListAsync();

            for (var index = 0; index < orderedQuestions.Count; index++)
            {
                orderedQuestions[index].OrderNumber = index + 1;
            }

            var record = orderedQuestions.FirstOrDefault(q => q.Id == id);
            if (record == null)
            {
                return;
            }

            var deletedOrder = record.OrderNumber ?? orderedQuestions.Count;
            _context.Question.Remove(record);

            foreach (var question in orderedQuestions.Where(q => q.Id != id && (q.OrderNumber ?? 0) > deletedOrder))
            {
                question.OrderNumber = (question.OrderNumber ?? 0) - 1;
            }

            await _context.SaveChangesAsync();
        }

        public async Task MoveUpAsync(int id)
        {
            var question = await _context.Question.FindAsync(id);
            if (question == null)
            {
                return;
            }

            var currentOrder = question.OrderNumber ?? 1;
            if (currentOrder <= 1)
            {
                return;
            }

            question.OrderNumber = currentOrder - 1;
            await UpdateAsync(question);
        }

        public async Task MoveDownAsync(int id)
        {
            var question = await _context.Question.FindAsync(id);
            if (question == null)
            {
                return;
            }

            var maxOrder = await _context.Question.CountAsync();
            var currentOrder = question.OrderNumber ?? maxOrder;
            if (currentOrder >= maxOrder)
            {
                return;
            }

            question.OrderNumber = currentOrder + 1;
            await UpdateAsync(question);
        }

        public async Task NormalizeOrderNumbersAsync()
        {
            var orderedQuestions = await _context.Question
                .OrderBy(q => q.OrderNumber ?? int.MaxValue)
                .ThenBy(q => q.Id)
                .ToListAsync();

            for (var index = 0; index < orderedQuestions.Count; index++)
            {
                orderedQuestions[index].OrderNumber = index + 1;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Question.AnyAsync(q => q.Id == id);
        }
    }
}
