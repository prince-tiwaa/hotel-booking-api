namespace HotelListing.Api.Application.DTOs.Booking;

public record UpdateBookingDto
    (
    DateOnly CheckIn,
    DateOnly CheckOut,
    int NumberOfGuests
    );
