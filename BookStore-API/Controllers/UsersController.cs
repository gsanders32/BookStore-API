using BookStore_API.Contracts;
using BookStore_API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BookStore_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly SignInManager<IdentityUser> _signManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILoggerService _logger;
        private readonly IConfiguration _configuration;

        public UsersController(SignInManager<IdentityUser> signManager, UserManager<IdentityUser> userManager, ILoggerService logger, IConfiguration configuration)
        {
            _signManager = signManager;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// User Login
        /// </summary>
        /// <param name="userDTO"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] UserDTO userDTO)
        {
            var location = GetControllerActionNames();
            try
            {
                var userName = userDTO.UserName;
                var password = userDTO.Password;
                _logger.LogInfo($"{location}: Attempt from users {userName}");
                
                var result = await _signManager.PasswordSignInAsync(userName, password, false, false);
                if (result.Succeeded)
                {
                    _logger.LogInfo($"{location}: {userName} successfully Authenticated");
                    var user = await _userManager.FindByNameAsync(userName);
                    string tokenString = await GenerateJSONWebToken(user);
                    return Ok(new { token = tokenString});
                }
                _logger.LogWarn($"{location}: {userName} Not Authenticated");
                return Unauthorized(userDTO);
            }
            catch (Exception e)
            {
                return InternalError($"{location}: {e.Message} - {e.InnerException}");
            }
            
        }

        private async Task<string> GenerateJSONWebToken(IdentityUser user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };
            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimsIdentity.DefaultRoleClaimType, r)));

            var token = new JwtSecurityToken(_configuration["Jwt:Issuer"], _configuration["Jwt:Issuer"], claims, null, expires: DateTime.Now.AddMinutes(5), signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        private string GetControllerActionNames()
        {
            var controller = ControllerContext.ActionDescriptor.ControllerName;
            var action = ControllerContext.ActionDescriptor.ActionName;
            return $"{controller} - {action}";
        }

        private ObjectResult InternalError(string message)
        {
            _logger.LogError(message);
            return StatusCode(500, "Something when wrong. Please contact the Administrator");
        }
    }
}
