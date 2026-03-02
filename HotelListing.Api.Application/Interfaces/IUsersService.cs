using HotelListing.Api.Application.DTOs.Auth;
using HotelListing.Api.Common.Results;

namespace HotelListing.Api.Application.Interfaces
{
    public interface IUsersService
    {
        string UserId { get; }

        Task<Result<string>> LoginAsync(LoginUserDto loginUserDto);
        Task<Result<RegisteredUserDto>> RegisterUserAsync(RegisterUserDto registerUserDto);
    }
}