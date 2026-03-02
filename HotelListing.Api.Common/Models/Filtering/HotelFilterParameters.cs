namespace HotelListing.Api.Common.Models.Filtering;

public class HotelFilterParameters : BaseFilterParameters
{
    public int? CountryId { get; set; }
    public double? MinimumRating { get; set; }
    public double? MaximumRating { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Location { get; set; }
}
