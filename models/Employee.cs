
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoMed.Api.Models
{
    [Table("Employees")]
    public sealed class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        public long UserId { get; set; }

        [MaxLength(150)]
        public string? Department { get; set; }

        [MaxLength(150)]
        public string? Position { get; set; }

        public DateOnly? HireDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}
