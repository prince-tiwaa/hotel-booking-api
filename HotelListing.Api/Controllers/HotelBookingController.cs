using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.AuthorizationFilters;
using HotelListing.Api.Application.DTOs.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HotelListing.Api.Common.Models.Paging;

namespace HotelListing.Api.Controllers;

[Route("api/hotels/{hotelId:int}/bookings[controller]")] // There can't be a booking without an hotel. Get bookings for an hotel via /api/hotels/{hotelId}/bookings.
[ApiController]
[Authorize]
public class HotelBookingController(IBookingService bookingService) : BaseApiController
{
    // GET: api/hotels/5/bookings
    [HttpGet("admin")]
    [HotelOrSystemAdmin]
    public async Task <ActionResult<PagedResult<GetBookingDto>>> GetBookingsAdmin(
        [FromRoute] int hotelId,
        [FromQuery] PaginationParameters paginationParameters)  //get from route (hotelId)
    {
        var result = await bookingService.GetBookingsForHotelAsync(hotelId, paginationParameters);
        return ToActionResult(result);
    }
    [HttpGet]
    public async Task <ActionResult<PagedResult<GetBookingDto>>> GetBookings(
        [FromRoute] int hotelId,
        [FromQuery] PaginationParameters paginationParameters) //get from route (hotelId)
    {
        var result = await bookingService.GetUserBookingsForHotelAsync(hotelId, paginationParameters);
        return ToActionResult(result);
    }
    [HttpPost]
    public async Task <ActionResult<GetBookingDto>> CreateBookingForHotel([FromRoute] int hotelId, [FromBody] CreateBookingDto createBookingDto) //get from route (hotelId)
    {
        var result = await bookingService.CreateBookingAsync(createBookingDto);

        return ToActionResult(result);
    }

    [HttpPut("{bookingid:int}")] //api/hotels/5/bookings/3
    public async Task <ActionResult<GetBookingDto>> UpdateBookingForHotel([FromRoute] int hotelId,
        [FromRoute] int bookingid,
        [FromBody] UpdateBookingDto updateBookingDto) //get from route (hotelId)
    {
        var result = await bookingService.UpdateBookingAsync(hotelId, bookingid, updateBookingDto);
        return ToActionResult(result);
    }

    [HttpPut("{bookingid:int}/cancel")] //api/hotels/5/bookings/3/cancel
    public async Task <ActionResult> CancelBookingForHotel([FromRoute] int hotelId, [FromRoute] int bookingid) 
    {
        var result = await bookingService.CancelBookingAsync(hotelId, bookingid);
        return ToActionResult(result);
    }

    [HttpPut("{bookingid:int}/admin/cancel")] //api/hotels/5/bookings/3/admin/cancel
    [HotelOrSystemAdmin]
    public async Task <ActionResult> AdminCancelBooking([FromRoute] int hotelId, [FromRoute] int bookingid) 
    {
        var result = await bookingService.AdminCancelBookingAsync(hotelId, bookingid);
        return ToActionResult(result);
    }

    [HttpPut("{bookingid:int}/admin/confirm")]
    [HotelOrSystemAdmin]
    public async Task <ActionResult> AdminConfirmBooking([FromRoute] int hotelId, [FromRoute] int bookingid) 
    {
        var result = await bookingService.AdminConfirmBookingAsync(hotelId, bookingid);
        return ToActionResult(result);
    }
}
