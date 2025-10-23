using System.ComponentModel.DataAnnotations;
using FoMed.Api.Models;

public class UserProfile
{
    [Key]
    public long UserId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}