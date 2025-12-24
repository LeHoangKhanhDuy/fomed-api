
using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models
{
    public class ServiceCategory
    {
        [Key]
        public int CategoryId { get; set; }

        [StringLength(50)]
        public string? Code { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        // visit | lab | vaccine
        [Required, StringLength(20)]
        public string CategoryType { get; set; } = "visit";
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Service> Services { get; set; } = new List<Service>();
    }
}
