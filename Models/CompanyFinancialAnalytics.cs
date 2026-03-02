using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWorkspaceTemp.Models
{
    [Table("vw_CompanyFinancialAnalytics")]
    public class CompanyFinancialAnalytics
    {
        [Key]
        public int Id { get; set; }

        public string? CompanyName { get; set; }
        public string? CompanyDescription { get; set; }
        public string? CeoFullName { get; set; }
        public string? CeoFirstName { get; set; }
        public string? CeoLastName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ExternalId { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Revenue2025 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Revenue2024 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Revenue2023 { get; set; }

        public string? TINNumber { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? GrowthAmount_2025vs2024 { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? GrowthPercent_2025vs2024 { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? GrowthAmount_2024vs2023 { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? GrowthPercent_2024vs2023 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? AverageRevenue_2Year { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? AverageRevenue_3Year { get; set; }

        public string? RevenueTrend { get; set; }
        public string? RevenueSize { get; set; }
        public DateTime? LastRefreshTime { get; set; }
    }
}
