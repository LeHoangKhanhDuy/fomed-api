namespace FoMed.Api.Models
{
    public class Service
    {
        public int ServiceId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal? BasePrice { get; set; }
        public short? DurationMin { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }


        public int? CategoryId { get; set; }
        public ServiceCategory? Category { get; set; }
    }
}
