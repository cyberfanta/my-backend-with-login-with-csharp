using System.ComponentModel.DataAnnotations;

namespace MyBackendApi.Models
{
    public class RefreshTokenRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
} 