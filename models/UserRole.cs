using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class UserRole
{
    [Key]
    public long UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; }

    public virtual User User { get; set; } = default!;
    public virtual Role Role { get; set; } = default!;
}
