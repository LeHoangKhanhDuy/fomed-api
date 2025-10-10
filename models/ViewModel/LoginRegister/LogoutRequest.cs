using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.ViewModel
{
    public sealed class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
