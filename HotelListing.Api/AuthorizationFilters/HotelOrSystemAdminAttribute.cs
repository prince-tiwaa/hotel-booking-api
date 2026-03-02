using HotelListing.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HotelListing.Api.AuthorizationFilters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]

public sealed class HotelOrSystemAdminAttribute : TypeFilterAttribute
{
    public HotelOrSystemAdminAttribute() : base(typeof(HotelOrSystemAdminFilter))
    {
        
    }
}

public class HotelOrSystemAdminFilter(HotelListingDbContext dbContext) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpUser = context.HttpContext.User;

        // 1. Check if authenticated
        if (httpUser?.Identity?.IsAuthenticated == false)
        {
            context.Result = new UnauthorizedResult(); // 401
            return;
        }

        // 2. Global Admin bypass
        if (httpUser!.IsInRole(RoleNames.Administrator))
        {
            return;
        }

        // 3. Robust User ID extraction (Only do this ONCE)
        var userId = httpUser.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? httpUser.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Result = new ObjectResult(new { message = "User ID claim missing from token." })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        // 4. Extract hotelId from Route
        context.RouteData.Values.TryGetValue("hotelId", out var hotelIdObj);
        int.TryParse(hotelIdObj?.ToString(), out int hotelId);

        if (hotelId == 0)
        {
            context.Result = new BadRequestObjectResult(new { message = "Invalid Hotel ID in route." });
            return;
        }

        // 5. Database check
        var isHotelAdminUser = await dbContext.HotelAdmins
            .AnyAsync(q => q.UserId == userId && q.HotelId == hotelId);

        if (!isHotelAdminUser)
        {
            // Using ObjectResult avoids the "No authentication handler" error
            context.Result = new ObjectResult(new { message = "You are not an admin of the selected hotel" })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }
    }
}