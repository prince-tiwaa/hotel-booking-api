using System.ComponentModel.DataAnnotations;

namespace HotelListing.Api.Application.DTOs.Booking;

public record CreateBookingDto
(
    [Required] int HotelId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    [Required] [Range(1, 10)] int NumberOfGuests) : IValidatableObject

{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CheckOut <= CheckIn)
        {
            yield return new ValidationResult(
                "Check-out date must be later than check-in date.",
                [nameof(CheckOut), nameof(CheckIn)]);
        }
    }
}
