using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyBackendApi.Data;
using MyBackendApi.Models;

namespace MyBackendApi.Services
{
    public class AuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponse> Register(RegisterRequest request)
        {
            // Check if the user already exists
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Username is already in use"
                };
            }

            // Create a new user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                Name = request.Name,
                LastName = request.LastName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            // Generate JWT token
            var token = GenerateJwtToken(user);

            return new AuthResponse
            {
                Token = token,
                Success = true,
                Message = "Registration successful",
                UserId = user.Id
            };
        }

        public async Task<AuthResponse> Login(LoginRequest request)
        {
            // Find user by username
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

            // Verify if user exists and password is correct
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid username or password"
                };
            }

            // Generate JWT token
            var token = GenerateJwtToken(user);

            return new AuthResponse
            {
                Token = token,
                Success = true,
                Message = "Login successful",
                UserId = user.Id
            };
        }

        public async Task<AuthResponse> RefreshToken(string token)
        {
            try
            {
                // Validate the current token
                var principal = GetPrincipalFromToken(token);
                
                if (principal == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid token"
                    };
                }

                // Extract user identification
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Token does not contain valid user information"
                    };
                }

                // Find the user in the database
                var user = await _context.Users.FindAsync(userGuid);
                
                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                // Generate a new token
                var newToken = GenerateJwtToken(user);

                return new AuthResponse
                {
                    Token = newToken,
                    Success = true,
                    Message = "Token updated successfully",
                    UserId = user.Id
                };
            }
            catch (Exception)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Error processing request"
                };
            }
        }

        public async Task<AuthResponse> DeleteAccount(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            
            if (user == null)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                Success = true,
                Message = "Account deleted successfully"
            };
        }

        // This method doesn't need to do anything in the backend, as JWT token management
        // is handled by the client simply discarding the token
        public AuthResponse Logout()
        {
            return new AuthResponse
            {
                Success = true,
                Message = "Logged out successfully"
            };
        }

        public ClaimsPrincipal? GetPrincipalFromToken(string token)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? "defaultsecretkey12345678901234567890");
            
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false  // We don't validate expiration to allow refreshing expired tokens
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);
                
                if (!IsJwtWithValidSecurityAlgorithm(validatedToken))
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }

        private bool IsJwtWithValidSecurityAlgorithm(SecurityToken validatedToken)
        {
            return (validatedToken is JwtSecurityToken jwtSecurityToken) &&
                   jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, 
                       StringComparison.InvariantCultureIgnoreCase);
        }

        public bool ValidateToken(string token)
        {
            return GetPrincipalFromToken(token) != null;
        }

        private string HashPassword(string password)
        {
            byte[] salt = new byte[16]; // Usar 16 bytes para el salt
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Usar HMACSHA256 en lugar de HMACSHA512
            using var hmac = new HMACSHA256(salt);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

            // Combine salt and hash
            var hashBytes = new byte[salt.Length + hash.Length];
            Array.Copy(salt, 0, hashBytes, 0, salt.Length);
            Array.Copy(hash, 0, hashBytes, salt.Length, hash.Length);

            return Convert.ToBase64String(hashBytes);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            try 
            {
                var hashBytes = Convert.FromBase64String(storedHash);
                
                // El salt ahora es de 16 bytes
                var saltSize = 16;
                
                // Verificar que el hash almacenado tiene el tamaño correcto
                if (hashBytes.Length < saltSize) 
                {
                    return false;
                }

                // Extract the salt (first 16 bytes)
                var salt = new byte[saltSize];
                Array.Copy(hashBytes, 0, salt, 0, saltSize);

                // Calculate the hash with the same salt
                using var hmac = new HMACSHA256(salt);
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

                // Verificar que el hash calculado tiene el tamaño correcto
                if (hashBytes.Length != saltSize + computedHash.Length)
                {
                    // Para usuarios existentes con hash en formato antiguo, intentamos verificar
                    // usando el método antiguo (64 bytes de salt + hash HMACSHA512)
                    if (TryVerifyOldPasswordFormat(password, storedHash))
                    {
                        return true;
                    }
                    
                    return false;
                }

                // Compare hashes
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != hashBytes[saltSize + i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private bool TryVerifyOldPasswordFormat(string password, string storedHash)
        {
            try
            {
                var hashBytes = Convert.FromBase64String(storedHash);
                
                // Probar diferentes tamaños de salt
                int[] possibleSaltSizes = new int[] { 64, 128 };
                
                foreach (var saltSize in possibleSaltSizes)
                {
                    if (hashBytes.Length < saltSize)
                    {
                        continue;
                    }
                    
                    // Extract the salt
                    var salt = new byte[saltSize];
                    Array.Copy(hashBytes, 0, salt, 0, saltSize);

                    // Calculate the hash with the same salt
                    using var hmac = new HMACSHA512(salt);
                    var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                    
                    // Verificar si hay suficientes bytes para comparar
                    if (hashBytes.Length < saltSize + computedHash.Length)
                    {
                        continue;
                    }
                    
                    // Compare hashes
                    bool match = true;
                    for (int i = 0; i < computedHash.Length; i++)
                    {
                        // Protegerse contra desbordamientos
                        if (saltSize + i >= hashBytes.Length)
                        {
                            match = false;
                            break;
                        }
                        
                        if (computedHash[i] != hashBytes[saltSize + i])
                        {
                            match = false;
                            break;
                        }
                    }
                    
                    if (match)
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? "defaultsecretkey12345678901234567890");
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
} 