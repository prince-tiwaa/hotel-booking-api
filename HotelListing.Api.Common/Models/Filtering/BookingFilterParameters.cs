using HotelListing.Api.Common.Enum;

namespace HotelListing.Api.Common.Models.Filtering;

public class BookingFilterParameters : BaseFilterParameters
{
    public BookingStatus BookingStatus { get; set; }
    public DateOnly? CheckInFrom { get; set; }
    public DateOnly? CheckInTo { get; set; }
    public DateOnly? CheckOutFrom { get; set; }
    public DateOnly? CheckOutTo { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinGuest { get; set; }
    public int? MaxGuest { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
}
