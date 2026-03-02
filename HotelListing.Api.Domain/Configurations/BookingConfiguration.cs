using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelListing.Api.Domain.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.Property(b => b.Status) // so the enum is stored as string in the database
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasIndex(b => new { b.HotelId, b.UserId, b.CheckIn, b.CheckOut }); // composite index for faster lookups
        }
    }
}
