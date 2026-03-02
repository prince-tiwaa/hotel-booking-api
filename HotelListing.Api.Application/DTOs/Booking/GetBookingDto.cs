namespace HotelListing.Api.Application.DTOs.Booking;

public record GetBookingDto
    (
    int Id,
    int HotelId,
    string HotelName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int NumberOfGuests,
    decimal TotalPrice,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
