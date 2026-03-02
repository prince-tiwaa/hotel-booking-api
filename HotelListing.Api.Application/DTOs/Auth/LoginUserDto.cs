using System.ComponentModel.DataAnnotations;

namespace HotelListing.Api.Application.DTOs.Auth
{
    public class LoginUserDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;
    }
}
