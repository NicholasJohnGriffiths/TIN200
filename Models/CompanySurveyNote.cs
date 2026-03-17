using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("CompanySurveyNotes")]
    public class CompanySurveyNote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CompanySurveyId { get; set; }

        [Required]
        public DateTime NoteDateTime { get; set; }

        [Required]
        [StringLength(255)]
        public string User { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
}
