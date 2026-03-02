using System.ComponentModel.DataAnnotations;

namespace HotelListing.Api.Application.DTOs.Country
{
    public class CreateCountryDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = null!;
        [Required]
        [MaxLength(3)]
        public string ShortName { get; set; } = null!;
    }
}
