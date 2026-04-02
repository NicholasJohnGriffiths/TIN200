using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("QuestionSubgroup")]
    public class QuestionSubgroup
    {
        [Key]
        public int Id { get; set; }

        public int QuestionGroupId { get; set; }

        [StringLength(255)]
        public string? Title { get; set; }
    }
}
