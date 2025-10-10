using System.ComponentModel.DataAnnotations;
using FoMed.Api.Models;

public class UserProfile
{
    [Key]
    public long UserId { get; set; }            // PK = FK
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}