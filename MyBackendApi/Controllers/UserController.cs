using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.Annotations;
using MyBackendApi.Services;
using MyBackendApi.Models;

namespace MyBackendApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [SwaggerTag("User management operations")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [SwaggerOperation(
            Summary = "List users", 
            Description = "Gets a paginated list of all users (requires authentication)",
            OperationId = "GetUsers",
            Tags = new[] { "User" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Users list retrieved successfully", typeof(PaginatedResponse<User>))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Unauthorized")]
        public async Task<IActionResult> GetUsers(
            [FromQuery, SwaggerParameter("Page number (starting from 1)", Required = false)] int pageNumber = 1, 
            [FromQuery, SwaggerParameter("Items per page (maximum 100)", Required = false)] int pageSize = 10)
        {
            var paginatedUsers = await _userService.GetUsers(pageNumber, pageSize);
            return Ok(paginatedUsers);
        }

        [HttpGet("{id}")]
        [SwaggerOperation(
            Summary = "Get user by ID", 
            Description = "Retrieves details for a specific user by their ID (requires authentication)",
            OperationId = "GetUserById",
            Tags = new[] { "User" }
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "User found", typeof(User))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "Unauthorized")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "User not found")]
        public async Task<IActionResult> GetUserById(
            [SwaggerParameter("Unique ID of the user", Required = true)] Guid id)
        {
            var user = await _userService.GetUserById(id);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(user);
        }
    }
} 