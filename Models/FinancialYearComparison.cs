using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("vw_FinancialYearComparison")]
    public class FinancialYearComparison
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? CompanyName { get; set; }
        public string? CeoFullName { get; set; }
        public int FiscalYear { get; set; }

        [Column(TypeName = "decimal(18, 0)")]
        public decimal? Revenue { get; set; }

        public string? ExternalId { get; set; }
        public string? Email { get; set; }
    }
}
