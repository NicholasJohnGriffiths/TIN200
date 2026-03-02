using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Data;
using TINWorkspaceTemp.Models;

namespace TINWorkspaceTemp.Services
{
    public class Tin200Service
    {
        private readonly ApplicationDbContext _context;

        public Tin200Service(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tin200>> GetAllTin200Async(int? financialYear = null)
        {
            var query = _context.Tin200.AsQueryable();
            if (financialYear.HasValue)
            {
                query = financialYear.Value switch
                {
                    2025 => query.Where(t => t.Fye2025 != null),
                    2024 => query.Where(t => t.Fye2024 != null),
                    2023 => query.Where(t => t.Fye2023 != null),
                    _ => query
                };
            }
            return await query.OrderByDescending(t => t.Id).ToListAsync();
        }

        public async Task<List<int>> GetAvailableFinancialYearsAsync()
        {
            var years = new List<int>();

            if (await _context.Tin200.AnyAsync(t => t.Fye2025 != null))
            {
                years.Add(2025);
            }

            if (await _context.Tin200.AnyAsync(t => t.Fye2024 != null))
            {
                years.Add(2024);
            }

            if (await _context.Tin200.AnyAsync(t => t.Fye2023 != null))
            {
                years.Add(2023);
            }

            return years;
        }

        public async Task<Tin200?> GetTin200ByIdAsync(int id)
        {
            return await _context.Tin200.FindAsync(id);
        }

        public async Task<Tin200> CreateTin200Async(Tin200 tin200)
        {
            _context.Tin200.Add(tin200);
            await _context.SaveChangesAsync();
            return tin200;
        }

        public async Task<Tin200> UpdateTin200Async(Tin200 tin200)
        {
            _context.Tin200.Update(tin200);
            await _context.SaveChangesAsync();
            return tin200;
        }

        public async Task DeleteTin200Async(int id)
        {
            var tin200 = await GetTin200ByIdAsync(id);
            if (tin200 != null)
            {
                _context.Tin200.Remove(tin200);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> Tin200ExistsAsync(int id)
        {
            return await _context.Tin200.AnyAsync(t => t.Id == id);
        }
    }
}
