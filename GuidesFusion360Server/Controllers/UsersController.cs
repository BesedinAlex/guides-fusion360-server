using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GuidesFusion360Server.Dtos;
using GuidesFusion360Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuidesFusion360Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUsersService _usersService;

        public UsersController(IUsersService usersService)
        {
            _usersService = usersService;
        }

        [AllowAnonymous]
        [HttpGet("token")]
        public async Task<IActionResult> GetUserToken([Required] string email, [Required] string password)
        {
            var serviceResponse = await _usersService.GetUserToken(email, password);

            if (!serviceResponse.Success)
            {
                return NotFound(serviceResponse);
            }

            return Ok(serviceResponse);
        }

        [AllowAnonymous]
        [HttpPost("new")]
        public async Task<IActionResult> CreateNewUser(UserRegisterDto newUser)
        {
            var serviceResponse = await _usersService.CreateNewUser(newUser);

            if (!serviceResponse.Success)
            {
                return BadRequest(serviceResponse);
            }

            return Ok(serviceResponse);
        }

        [HttpGet("password-restore-code")]
        public async Task<IActionResult> GetRestorePasswordCode([Required] string email)
        {
            var userId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value);

            var (serviceResponse, statusCode) = await _usersService.GetPasswordRestoreCode(email, userId);

            return statusCode switch
            {
                401 => Unauthorized(serviceResponse),
                404 => NotFound(serviceResponse),
                _ => Ok(serviceResponse)
            };
        }

        [AllowAnonymous]
        [HttpPut("restore-password")]
        public async Task<IActionResult> RestorePassword([Required] string restoreCode, [Required] string password)
        {
            var (serviceResponse, statusCode) = await _usersService.RestorePassword(restoreCode, password);

            return statusCode switch
            {
                400 => BadRequest(serviceResponse),
                401 => Unauthorized(serviceResponse),
                404 => NotFound(serviceResponse),
                _ => Ok(serviceResponse)
            };
        }
    }
}