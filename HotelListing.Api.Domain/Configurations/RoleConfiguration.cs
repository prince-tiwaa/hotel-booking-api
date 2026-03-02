using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelListing.Api.Domain.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> builder)
    {
        builder.HasData(
            new IdentityRole
            {
                Id = "a16a27b1-8c9e-4b2f-83be-d445959228c4",
                Name = RoleNames.User,
                NormalizedName = RoleNames.User.ToUpper(),
                ConcurrencyStamp = "1"
            },
            new IdentityRole
            {
                Id = "b27b38c2-9d0f-4c3f-94cf-e556a6a339d5",
                Name = RoleNames.Administrator,
                NormalizedName = RoleNames.Administrator.ToUpper(),
                ConcurrencyStamp = "2"
            },
            new IdentityRole
            {
                Id = "bea6ca03-e5a7-4698-bc86-1c83f66a29c2",
                Name = RoleNames.HotelAdmin,
                NormalizedName = "HOTEL ADMIN",
                ConcurrencyStamp = "3"
            }
        );
    }
}
