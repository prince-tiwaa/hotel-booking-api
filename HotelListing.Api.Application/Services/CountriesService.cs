using HotelListing.Api.Application.DTOs.Country;
using HotelListing.Api.Application.DTOs.Hotel;
using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.Common.Constants;
using HotelListing.Api.Common.Models.Extensions;
using HotelListing.Api.Common.Models.Filtering;
using HotelListing.Api.Common.Models.Paging;
using HotelListing.Api.Common.Results;
using HotelListing.Api.Domain;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;

namespace HotelListing.Api.Application.Services;

public class CountriesService(HotelListingDbContext context) : ICountriesService
{
    public async Task<Result<IEnumerable<GetCountriesDto>>> GetAllAsync(CountryFilterParameters filters)
    {
        var query = context.Countries.AsQueryable(); 
        // Apply filtering based on CountryName or ShortName
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim().ToLower();
            query = query.Where(c => EF.Functions.Like(c.Name.ToLower(), $"%{term}%")
            || EF.Functions.Like(c.ShortName.ToLower(), $"%{term}%")); // column to compare and value to compare with
        }


        var countries = await query.AsNoTracking()  // improves performance for read-only queries
            .Select(c => new GetCountriesDto(c.CountryId, c.Name, c.ShortName))
            .ToListAsync();
        

        return Result<IEnumerable<GetCountriesDto>>.Success(countries);
    }
    public async Task<Result<GetCountryDto>> GetCountryAsync(int id)
    {
            var country = await context.Countries
            .AsNoTracking()
            .Where(c => c.CountryId == id)
            .Select(c => new GetCountryDto(
                c.CountryId,
                c.Name,
                c.ShortName,
                c.Hotels.Select(h => new GetHotelSlim(h.Id, h.Name, h.Address, h.Rating)).ToList()
                ))
                .FirstOrDefaultAsync();    

        return country is null
           ? Result<GetCountryDto>.NotFound()
           : Result<GetCountryDto>.Success(country);
    }

    public async Task<Result<GetCountryHotelsDto>> GetCountryHotelsAsync(int countryId,
        PaginationParameters paginationParameters, CountryFilterParameters filters)
    {
        var isCountryExists = await context.Countries
            .Where(c => c.CountryId == countryId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();

        if (isCountryExists is null)
        {
            return Result<GetCountryHotelsDto>.Failure
                (new Error(ErrorCodes.NotFound, $"Country {countryId} was not found."));
        }
        // Start query on HOTELS, filtered by this CountryId
        var hotelQuery = context.Hotels
            .Where(h => h.CountryId == countryId)
            .AsQueryable();
        // Apply filtering based on Hotel Name
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim().ToLower();
            hotelQuery = hotelQuery.Where(c => EF.Functions.Like(c.Name.ToLower(), $"%{term}%"));
        }
        // Apply sorting based on SortBy and SortDescending
        hotelQuery = (filters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "name" => filters.SortDescending ? hotelQuery.OrderByDescending(c => c.Name)
                : hotelQuery.OrderBy(c => c.Name),
            "shortname" => filters.SortDescending ? hotelQuery.OrderByDescending(c => c.Rating)
                : hotelQuery.OrderBy(c => c.Rating),
            _ => hotelQuery.OrderBy(c => c.Name)
        };
        // Apply pagination
        var pagedHotels = await hotelQuery
            .Select(c => new GetHotelSlim(c.Id, c.Name, c.Address, c.Rating))
            .ToPagedResultAsync(paginationParameters);

        var response = new GetCountryHotelsDto
        {
            Id = countryId,
            Name = isCountryExists,
            Hotels = pagedHotels
        };


        return Result<GetCountryHotelsDto>.Success(response);
    }
    public async Task<Result> UpdateCountryAsync(int id, UpdateCountryDto countryDto)
    {
        try
        {
            if (!id.Equals(countryDto.Id))
            {
                return Result.BadRequest(new Error(ErrorCodes.Validation, "ID route value does not match payload Id. "));
            }

            var country = await context.Countries.FindAsync(id);
            if (country == null)
            {
                return Result.NotFound(new Error(ErrorCodes.NotFound, $"Country with ID {id} was not found."));
            }

            country.Name = countryDto.Name;
            country.ShortName = countryDto.ShortName;

            context.Countries.Update(country);
            await context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception)
        {
            return Result.Failure(new Error(ErrorCodes.BadRequest, "An unexpected error occurred."));
        }
    }
    public async Task<Result<GetCountryDto>> CreateCountryAsync(CreateCountryDto countryDto)
    {
        try
        {
            var exists = await CountryExistsAsync(countryDto.Name);
            if (exists)
            {
                return Result<GetCountryDto>.Failure(new Error(ErrorCodes.Conflict, $"A country with name '{countryDto.Name}' already exists."));
            }

            var country = new Country
            {
                Name = countryDto.Name,
                ShortName = countryDto.ShortName
            };
            context.Countries.Add(country);
            await context.SaveChangesAsync();

            var dto = new GetCountryDto(
                country.CountryId,
                country.Name,
                country.ShortName,
                []
            );

            return Result<GetCountryDto>.Success(dto);
        }
        catch (Exception)
        {
            return Result<GetCountryDto>.Failure(new Error(ErrorCodes.BadRequest, "An unexpected error occurred."));
        }
    }
    public async Task<Result> DeleteCountryAsync(int id)
    {
        try
        {
            var country = await context.Countries.FindAsync(id);
            if (country == null)
            {
                return Result.NotFound(new Error(ErrorCodes.NotFound, $"Country with ID {id} was not found."));
            }
            context.Countries.Remove(country);
            await context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception)
        {
            return Result.Failure(new Error(ErrorCodes.BadRequest, "An unexpected error occurred."));
        }
    }

    public async Task<bool> CountryExistsAsync(string name)
    {
        return await context.Countries
            .AsNoTracking()
            .AnyAsync(e => e.Name.ToLower().Trim() == name.ToLower().Trim());

    }

    public async Task<Result> PatchCountryAsync(int id, JsonPatchDocument<UpdateCountryDto> patchDoc)
    {
        var country = await context.Countries.FindAsync(id);
        if (country == null)
        {
            return Result.NotFound(new Error(ErrorCodes.NotFound, $"Country with ID {id} was not found."));
        }
        var countryToPatch = new UpdateCountryDto // Create a DTO to apply the patch to
        {
            Name = country.Name,
            ShortName = country.ShortName
        };
        patchDoc.ApplyTo(countryToPatch); // Apply the patch to the DTO

        var normalizedName = countryToPatch.Name.ToLower().Trim();
        var duplicateExists = await context.Countries
            .AnyAsync(c => c.Name.ToLower().Trim() == normalizedName && c.CountryId != id);
        if (duplicateExists)
        {
            return Result.Failure(new Error(ErrorCodes.Conflict, $"A country with name '{countryToPatch.Name}' already exists."));
        }
        country.Name = countryToPatch.Name;
        country.ShortName = countryToPatch.ShortName;
        await context.SaveChangesAsync();

        return Result.Success();
    }
}

