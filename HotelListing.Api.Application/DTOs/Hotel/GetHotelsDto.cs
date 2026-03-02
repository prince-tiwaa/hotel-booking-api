namespace HotelListing.Api.Application.DTOs.Hotel;
public record GetHotelSlim(
    int Id,
    string Name,
    string Address,
    double Rating
 );
