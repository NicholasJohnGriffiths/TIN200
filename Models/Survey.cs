using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWorkspaceTemp.Models
{
    [Table("Survey")]
    public class Survey
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Financial Year")]
        [Required]
        public int FinancialYear { get; set; }
    }
}