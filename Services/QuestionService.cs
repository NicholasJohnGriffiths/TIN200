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
            _context.Question.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<Question> UpdateAsync(Question record)
        {
            _context.Question.Update(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task DeleteAsync(int id)
        {
            var record = await _context.Question.FindAsync(id);
            if (record != null)
            {
                _context.Question.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Question.AnyAsync(q => q.Id == id);
        }
    }
}
