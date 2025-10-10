using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoMed.Api.Models
{
    [Table("EmailVerificationTokens")]
    public class EmailVerificationTokens
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long TokenId { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        public byte[] TokenHash { get; set; } = Array.Empty<byte>();

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime? UsedAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }
}
