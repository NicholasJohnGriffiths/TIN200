using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWorkspaceTemp.Models
{
    [Table("vw_RevenueSummaryBySize")]
    public class RevenueSummaryBySize
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string? RevenueSize { get; set; }

        public int CompanyCount { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? TotalRevenue2025 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? AverageRevenue2025 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? MinRevenue2025 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? MaxRevenue2025 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? TotalRevenue2024 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? AverageRevenue2024 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? TotalRevenue2023 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? AverageRevenue2023 { get; set; }
    }
}
