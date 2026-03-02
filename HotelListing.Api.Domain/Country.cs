using System.ComponentModel.DataAnnotations;

namespace HotelListing.Api.Domain
{
    public class Country
    {
        public int CountryId { get; set; }
        public string Name { get; set; } = null!;
        public string ShortName { get; set; } = null!;
        public IList<Hotel> Hotels { get; set; } = [];
    }
}
