namespace HotelListing.Api.Common.Models.Filtering;

public abstract class BaseFilterParameters
{
    public string? Search { get; set; }
    public string? SortBy { get; set; } // column to sort by
    public bool SortDescending { get; set; } = false;
}
