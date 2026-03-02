using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWorkspaceTemp.Models
{
    [Table("vw_TopPerformersAnalytics")]
    public class TopPerformersAnalytics
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int RankByRevenue2025 { get; set; }

        public string? CompanyName { get; set; }
        public string? CeoFullName { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Revenue2025 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Revenue2024 { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Revenue2023 { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? YoYGrowth2025 { get; set; }

        public string? PerformanceTrend { get; set; }
        public string? ContactEmail { get; set; }
    }
}
