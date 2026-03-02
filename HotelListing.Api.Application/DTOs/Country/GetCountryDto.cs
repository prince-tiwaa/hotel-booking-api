using HotelListing.Api.Application.DTOs.Hotel;
using HotelListing.Api.Common.Models.Paging;

namespace HotelListing.Api.Application.DTOs.Country;

public record GetCountryDto
(
    int Id,
    string Name,
    string ShortName,
    List<GetHotelSlim>? Hotels
);

public record GetCountryHotelsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PagedResult<GetHotelSlim> Hotels { get; set; } = new ();
}

public record GetCountriesDto
 (
        int Id,
        string Name,
        string ShortName
 );
