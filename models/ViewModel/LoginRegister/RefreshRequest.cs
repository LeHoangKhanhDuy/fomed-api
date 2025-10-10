using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.ViewModel;

public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
