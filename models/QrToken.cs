using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class QrToken
{
    [Key]
    public Guid TokenId { get; set; }
    public string RefType { get; set; } = default!;
    public long RefId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
