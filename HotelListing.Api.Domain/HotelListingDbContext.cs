using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace HotelListing.Api.Domain;

public class HotelListingDbContext : IdentityDbContext<ApplicationUser>
{
    public HotelListingDbContext(DbContextOptions<HotelListingDbContext> options) : base(options)
    {
        
    }
    public DbSet<Country> Countries { get; set; }
    public DbSet<Hotel> Hotels { get; set; }
    public DbSet<HotelAdmin> HotelAdmins { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        builder.Entity<Country>()
               .HasIndex(c => c.Name)
               .HasDatabaseName("IX_Countries_Name");

        builder.Entity<Country>()
               .HasIndex(c => c.ShortName)
               .HasDatabaseName("IX_Countries_ShortName");

        builder.Entity<Hotel>()
            .HasIndex(c => c.CountryId)
            .HasDatabaseName("IX_Hotels_CountryID");

        builder.Entity<Hotel>()
            .HasIndex(c => c.Name)
            .HasDatabaseName("IX_Hotels_Name");

        builder.Entity<Hotel>()
            .HasIndex(c => new { c.CountryId, c.Rating })
            .HasDatabaseName("IX_Hotels_CountryID_Rating");

        // Explicitly handle the DateOnly conversion for Bookings
        builder.Entity<Booking>(entity =>
        {
            entity.Property(b => b.CheckIn)
                  .HasConversion<DateOnly>();

            entity.Property(b => b.CheckOut)
                  .HasConversion<DateOnly>();
        });
    }
    
}
