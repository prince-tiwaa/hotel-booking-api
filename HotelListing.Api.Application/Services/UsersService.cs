using HotelListing.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using HotelListing.Api.Common.Constants;
using HotelListing.Api.Common.Models;
using HotelListing.Api.Common.Results;
using HotelListing.Api.Application.DTOs.Auth;
using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.Common.Models.Config;
using Microsoft.Extensions.Logging;

namespace HotelListing.Api.Application.Services;

public class UsersService(
    UserManager<ApplicationUser> userManager,
    HotelListingDbContext hotelListingDbContext,
    IOptions<JwtSettings> jwtoptions,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UsersService> logger) : IUsersService   // ILogger is usually injected in the context of the service it's being used
{
    public async Task<Result<RegisteredUserDto>> RegisterUserAsync(RegisterUserDto registerUserDto)
    {
        var user = new ApplicationUser
        {
            Email = registerUserDto.Email,
            FirstName = registerUserDto.FirstName,
            LastName = registerUserDto.LastName,
            UserName = registerUserDto.Email
        };
        var result = await userManager.CreateAsync(user, registerUserDto.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => new Error(ErrorCodes.BadRequest, e.Description)).ToArray();

            logger.LogError("User registration failed for {Email}: {Errors}",
                registerUserDto.Email, string.Join(", ", errors));

            return Result<RegisteredUserDto>.BadRequest(errors);
        }

        // Defensive: normalize & default the role before calling Identity
        var role = string.IsNullOrWhiteSpace(registerUserDto.Role)
            ? RoleNames.User
            : registerUserDto.Role.Trim();
        // if no role value is TokenProviderDescriptor, default it to 'user' role
        await userManager.AddToRoleAsync(user, role);

        if (role == RoleNames.HotelAdmin) // if the user is a hotel admin, create a HotelAdmin entry
        {
            var hotelAdmin = hotelListingDbContext.Add(
                new HotelAdmin
                {
                    UserId = user.Id,
                    HotelId = registerUserDto.AssociatedHotelId.GetValueOrDefault()
                });
            await hotelListingDbContext.SaveChangesAsync();
        }

        var registeredUser = new RegisteredUserDto
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Id = user.Id,
            Role = registerUserDto.Role
        };
        return Result<RegisteredUserDto>.Success(registeredUser);
    }

    public async Task<Result<string>> LoginAsync(LoginUserDto loginUserDto)
    {
        var user = await userManager.FindByEmailAsync(loginUserDto.Email);
        if (user == null)
        {
            logger.LogWarning("Failed login attempt for email: {Email}", loginUserDto.Email);
            return Result<string>.Failure(new Error(ErrorCodes.BadRequest, "Invalid Credentials"));
        }
        var isPasswordValid = await userManager.CheckPasswordAsync(user, loginUserDto.Password);
        if (!isPasswordValid)
        {
            return Result<string>.Failure(new Error(ErrorCodes.BadRequest, "Invalid Credentials"));
        }

        // generate JWT token

        var token = await GenerateToken(user);
        return Result<string>.Success(token);
    }

    public string UserId => httpContextAccessor?
        .HttpContext?
        .User?
        .FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? httpContextAccessor?
        .HttpContext?
        .User?
        .FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    public async Task<string> GenerateToken(ApplicationUser user)
    {
        // set basic user claims
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName)
        };

        // set user roles claims
        var roles = await userManager.GetRolesAsync(user);
        var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();

        claims = claims.Union(roleClaims).ToList();

        // set JWT key credentials
        if (string.IsNullOrWhiteSpace(jwtoptions.Value.Key))
            throw new InvalidOperationException("JWT Key not configured for token generation.");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtoptions.Value.Key));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // create the encoded token
        var token = new JwtSecurityToken(
            issuer: jwtoptions.Value.Issuer,
            audience: jwtoptions.Value.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToInt32(jwtoptions.Value.DurationInMinutes)),
            signingCredentials: signingCredentials);

        // return the token value
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
