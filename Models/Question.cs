using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TINWeb.Models
{
    [Table("Question")]
    public class Question
    {
        [Key]
        public int Id { get; set; }

        [StringLength(255)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? GroupTitle { get; set; }

        public string? GroupDescription { get; set; }

        public int? GroupId { get; set; }

        public string? QuestionText { get; set; }

        [StringLength(255)]
        public string? ImportColumnName { get; set; }

        [StringLength(255)]
        public string? ImportColumnNameAlt { get; set; }

        public int? OrderNumber { get; set; }

        [StringLength(255)]
        public string? Multi1 { get; set; }

        [StringLength(255)]
        public string? Multi2 { get; set; }

        [StringLength(255)]
        public string? Multi3 { get; set; }

        [StringLength(255)]
        public string? Multi4 { get; set; }

        [StringLength(255)]
        public string? Multi5 { get; set; }

        [StringLength(255)]
        public string? Multi6 { get; set; }

        [StringLength(255)]
        public string? Multi7 { get; set; }

        [StringLength(255)]
        public string? Multi8 { get; set; }

        [StringLength(255)]
        public string? Multi9 { get; set; }

        [StringLength(255)]
        public string? Multi10 { get; set; }

        [StringLength(50)]
        public string? AnswerType { get; set; }
    }
}
