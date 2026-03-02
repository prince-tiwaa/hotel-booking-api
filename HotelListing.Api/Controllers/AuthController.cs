using HotelListing.Api.Application.DTOs.Auth;
using HotelListing.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelListing.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
  
public class AuthController(IUsersService usersService) : BaseApiController
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisteredUserDto>> Register(RegisterUserDto registerUserDto)
    {
        var result = await usersService.RegisterUserAsync(registerUserDto);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<string>> Login(LoginUserDto registerUserDto)
    {
        var result = await usersService.LoginAsync(registerUserDto);
        return ToActionResult(result);
    }
}
