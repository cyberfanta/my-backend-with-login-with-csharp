namespace MyBackendApi.Models
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
} 