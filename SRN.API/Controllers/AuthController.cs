using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SRN.Application.DTOs;
using SRN.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SRN.API.Controllers
{
    /// <summary>
    /// Manages user identity, including registration, login, and JWT token issuance.
    /// Utilizes ASP.NET Core Identity for secure password hashing and role management.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        /// <summary>
        /// Registers a new user and assigns them the default 'Member' role.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                WalletAddress = model.WalletAddress,
                Role = "Member" // Default role assignment for new sign-ups
            };

            // CreateAsync automatically hashes the password before persisting it to the database
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Member");
                return Ok(new { message = "User registered successfully!" });
            }

            // Return specific identity validation errors (e.g., password too weak, email taken)
            return BadRequest(result.Errors);
        }

        /// <summary>
        /// Authenticates user credentials and returns a signed JWT for session management.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            // Validate user existence and verify the provided password against the stored hash
            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return Unauthorized("Invalid login attempt.");
            }

            var token = await GenerateJwtTokenAsync(user);

            return Ok(new
            {
                token,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    walletAddress = user.WalletAddress,
                    role = user.Role
                }
            });
        }

        /// <summary>
        /// Secure endpoint to retrieve the currently logged-in user's profile information.
        /// Relies on the provided JWT Bearer token to identify the user.
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found.");

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                walletAddress = user.WalletAddress,
                role = user.Role
            });
        }

        /// <summary>
        /// Helper method to construct and sign a JSON Web Token (JWT).
        /// Embeds essential user claims (ID, Email, Roles) securely into the payload.
        /// </summary>
        private async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured."));
            var tokenHandler = new JwtSecurityTokenHandler();

            // Standard claims payload
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("WalletAddress", user.WalletAddress ?? "")
            };

            // Embed all assigned roles into the token claims for RBAC (Role-Based Access Control)
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Fallback to ensure the primary role property is included
            if (!string.IsNullOrEmpty(user.Role) && !roles.Contains(user.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["DurationInMinutes"] ?? "60")),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}