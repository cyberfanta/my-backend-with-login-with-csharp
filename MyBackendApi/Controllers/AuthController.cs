using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.Annotations;
using MyBackendApi.Models;
using MyBackendApi.Services;

namespace MyBackendApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [SwaggerTag("Authentication operations")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("signup")]
        [SwaggerOperation(
            Summary = "Register new user",
            Description = "Creates a new user account with username and password",
            OperationId = "SignUp",
            Tags = new[] { "Auth" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Registration successful", typeof(AuthResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Error in submitted data", typeof(AuthResponse))]
        public async Task<IActionResult> SignUp([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.Register(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("login")]
        [SwaggerOperation(
            Summary = "User login", 
            Description = "Authenticates an existing user and returns a JWT token",
            OperationId = "Login",
            Tags = new[] { "Auth" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Login successful", typeof(AuthResponse))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Invalid credentials", typeof(AuthResponse))]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.Login(request);

            if (!response.Success)
            {
                return Unauthorized(response);
            }

            return Ok(response);
        }

        [HttpPost("refresh-token")]
        [SwaggerOperation(
            Summary = "Refresh token", 
            Description = "Updates an existing JWT token to extend the session",
            OperationId = "RefreshToken",
            Tags = new[] { "Auth" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Token updated successfully", typeof(AuthResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid token", typeof(AuthResponse))]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.RefreshToken(request.Token);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [SwaggerOperation(
            Summary = "Logout", 
            Description = "Ends the current user session (requires authentication)",
            OperationId = "Logout",
            Tags = new[] { "Auth" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Logout successful", typeof(AuthResponse))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Unauthorized")]
        public IActionResult Logout()
        {
            var response = _authService.Logout();
            return Ok(response);
        }

        [HttpDelete("delete-account")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [SwaggerOperation(
            Summary = "Delete account", 
            Description = "Permanently deletes the current user's account (requires authentication)",
            OperationId = "DeleteAccount",
            Tags = new[] { "Auth" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Account deleted successfully", typeof(AuthResponse))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Unauthorized")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "User not found", typeof(AuthResponse))]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Unauthorized(new { message = "User not properly identified" });
            }

            var response = await _authService.DeleteAccount(userGuid);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }
    }
} 