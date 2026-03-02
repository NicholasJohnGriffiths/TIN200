using Microsoft.EntityFrameworkCore;
using TINWorkspaceTemp.Data;
using TINWorkspaceTemp.Models;
using System.Data;

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
            try
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
            catch
            {
                try
                {
                    return await GetAllTin200FallbackAsync(financialYear);
                }
                catch
                {
                    return new List<Tin200>();
                }
            }
        }

        public async Task<List<int>> GetAvailableFinancialYearsAsync()
        {
            try
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
            catch
            {
                try
                {
                    var rows = await GetAllTin200FallbackAsync();
                    var years = new List<int>();
                    if (rows.Any(r => r.Fye2025 != null)) years.Add(2025);
                    if (rows.Any(r => r.Fye2024 != null)) years.Add(2024);
                    if (rows.Any(r => r.Fye2023 != null)) years.Add(2023);
                    return years;
                }
                catch
                {
                    return new List<int>();
                }
            }
        }

        public async Task<Tin200?> GetTin200ByIdAsync(int id)
        {
            try
            {
                return await _context.Tin200.FindAsync(id);
            }
            catch
            {
                try
                {
                    var all = await GetAllTin200FallbackAsync();
                    return all.FirstOrDefault(x => x.Id == id);
                }
                catch
                {
                    return null;
                }
            }
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

        private async Task<List<Tin200>> GetAllTin200FallbackAsync(int? financialYear = null)
        {
            var map = await GetTin200ColumnMapAsync();
            var rows = new List<Tin200>();

            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();

                var sql = $@"
SELECT
    [{map["Id"]}] AS Id,
    [{map["CeoFirstName"]}] AS CeoFirstName,
    [{map["CeoLastName"]}] AS CeoLastName,
    [{map["Email"]}] AS Email,
    [{map["ExternalId"]}] AS ExternalId,
    [{map["CompanyName"]}] AS CompanyName,
    [{map["CompanyDescription"]}] AS CompanyDescription,
    [{map["Fye2025"]}] AS Fye2025,
    [{map["Fye2024"]}] AS Fye2024,
    [{map["Fye2023"]}] AS Fye2023
FROM [TIN200]";

                if (financialYear.HasValue)
                {
                    var yearColumn = financialYear.Value switch
                    {
                        2025 => map["Fye2025"],
                        2024 => map["Fye2024"],
                        2023 => map["Fye2023"],
                        _ => string.Empty
                    };

                    if (!string.IsNullOrWhiteSpace(yearColumn))
                    {
                        sql += $" WHERE [{yearColumn}] IS NOT NULL";
                    }
                }

                sql += $" ORDER BY [{map["Id"]}] DESC";
                cmd.CommandText = sql;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new Tin200
                    {
                        Id = GetInt32(reader, "Id"),
                        CeoFirstName = GetString(reader, "CeoFirstName"),
                        CeoLastName = GetString(reader, "CeoLastName"),
                        Email = GetString(reader, "Email"),
                        ExternalId = GetString(reader, "ExternalId"),
                        CompanyName = GetString(reader, "CompanyName"),
                        CompanyDescription = GetString(reader, "CompanyDescription"),
                        Fye2025 = GetDecimal(reader, "Fye2025"),
                        Fye2024 = GetDecimal(reader, "Fye2024"),
                        Fye2023 = GetDecimal(reader, "Fye2023")
                    });
                }

                return rows;
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        private async Task<Dictionary<string, string>> GetTin200ColumnMapAsync()
        {
            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'TIN200'";

                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existing.Add(reader.GetString(0));
                }

                string Pick(params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (existing.Contains(name))
                        {
                            return name;
                        }
                    }
                    throw new InvalidOperationException($"None of the expected columns were found: {string.Join(", ", names)}");
                }

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Id"] = Pick("Id"),
                    ["CeoFirstName"] = Pick("CEOFirstName", "CeoFirstName", "CEO First Name", "CEO First Name "),
                    ["CeoLastName"] = Pick("CEOLastName", "CeoLastName", "CEO Last Name", "CEO Last Name "),
                    ["Email"] = Pick("Email", "Email "),
                    ["ExternalId"] = Pick("ExternalID", "ExternalId", "External ID"),
                    ["CompanyName"] = Pick("CompanyName", "Company Name"),
                    ["CompanyDescription"] = Pick("CompanyDescription", "Company Description"),
                    ["Fye2025"] = Pick("FYE2025", "Fye2025", "FYE 2025"),
                    ["Fye2024"] = Pick("FYE2024", "Fye2024", "FYE 2024"),
                    ["Fye2023"] = Pick("FYE2023", "Fye2023", "FYE 2023")
                };
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        private static string? GetString(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
        }

        private static int GetInt32(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.GetInt32(ordinal);
        }

        private static decimal? GetDecimal(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            if (record.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToDecimal(record.GetValue(ordinal));
        }
    }
}
