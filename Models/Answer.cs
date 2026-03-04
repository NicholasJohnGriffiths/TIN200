using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWorkspaceTemp.Models
{
    [Table("Answer")]
    public class Answer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClientSurveyId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        public string? AnswerText { get; set; }

        [Column(TypeName = "money")]
        public decimal? AnswerCurrency { get; set; }

        public int? AnswerNumber { get; set; }
    }
}
