using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWorkspaceTemp.Models
{
    [Table("CompanySurvey")]
    public class CompanySurvey
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Company ID")]
        [Required]
        public int CompanyId { get; set; }

        [Display(Name = "Survey ID")]
        [Required]
        public int SurveyId { get; set; }

        [Required]
        public bool Saved { get; set; }

        [Required]
        public bool Submitted { get; set; }

        [Required]
        public bool Requested { get; set; }
    }
}
