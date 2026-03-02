using Microsoft.AspNetCore.Mvc;
using HotelListing.Api.Application.DTOs.Hotel;
using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.Common.Models.Paging;
using HotelListing.Api.Common.Models.Filtering;
using HotelListing.Api.AuthorizationFilters;
using Microsoft.AspNetCore.Authorization;

namespace HotelListing.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class HotelsController(IHotelService hotelService) : BaseApiController
{

    // GET: api/Hotels
    [HttpGet]
    [HotelOrSystemAdmin]
    public async Task<ActionResult<PagedResult<GetHotelDto>>> GetHotels(
        [FromQuery] PaginationParameters paginationParameters,
        [FromQuery] HotelFilterParameters filters)
    {
        var hotels = await hotelService.GetAllHotelAsync(paginationParameters, filters);
        return ToActionResult(hotels);
    }

    // GET: api/Hotels/5
    [HttpGet("{id}")]
    public async Task<ActionResult<GetHotelDto>> GetHotel(int id)
    {
        var hotel = await hotelService.GetHotelAsync(id);
        return ToActionResult(hotel);
    }

    // PUT: api/Hotels/5
    [HttpPut("{id}")]
    public async Task<ActionResult<UpdateHotelDto>> PutHotel(int id, UpdateHotelDto hotelDto)
    {
        var hotel = await hotelService.UpdateHotelAsync(id, hotelDto);
        return ToActionResult(hotel);
    }

    // POST: api/Hotels
    [HttpPost]
    public async Task<ActionResult<CreateHotelDto>> PostHotel(CreateHotelDto hotelDto)
    {
        var hotel = await hotelService.CreateHotelAsync(hotelDto);
        if (!hotel.IsSuccess) return MapErrorsToResponse(hotel.Errors);

        return CreatedAtAction(nameof(GetHotel), new { id = hotel.Value!.Id }, hotel.Value);
    }

    // DELETE: api/Hotels/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHotel(int id)
    {
        var hotel = await hotelService.DeleteHotelAsync(id);

        return ToActionResult(hotel);
    }
}
