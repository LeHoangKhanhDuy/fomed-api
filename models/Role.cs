using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class Role
{
    [Key]
    public int RoleId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
