using AutoMapper;
using AutoMapper.QueryableExtensions;
using HotelListing.Api.Domain;
using Microsoft.EntityFrameworkCore;
using HotelListing.Api.Common.Constants;
using HotelListing.Api.Common.Results;
using HotelListing.Api.Application.DTOs.Hotel;
using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.Common.Models.Paging;
using HotelListing.Api.Common.Models.Extensions;
using HotelListing.Api.Common.Models.Filtering;

namespace HotelListing.Api.Application.Services;

public class HotelService(HotelListingDbContext context, IMapper mapper) : IHotelService
{
    public async Task<Result<GetHotelDto>> CreateHotelAsync(CreateHotelDto hotelDto)
    {
        // No try-catch needed here! 
        // If the database connection fails, the Global Handler catches it.

        var countryExists = await CountryExists(hotelDto.CountryId);
        if (!countryExists)
        {
            return Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, $"Country with Id {hotelDto.CountryId} was not found."));
        }

        var duplicate = await HotelExistsAsync(hotelDto.Name, hotelDto.CountryId);
        if (duplicate)
        {
            return Result<GetHotelDto>.Failure(new Error(ErrorCodes.Conflict, $"Hotel with name {hotelDto.Name} already exists."));
        }

        var hotel = mapper.Map<Hotel>(hotelDto);
        context.Hotels.Add(hotel);
        await context.SaveChangesAsync();

        var returnObj = mapper.Map<GetHotelDto>(hotel);
        return Result<GetHotelDto>.Success(returnObj);
    }

    public async Task<Result> DeleteHotelAsync(int id)
    {
        try
        {
            var hotel = await context.Hotels.FindAsync(id);
            if (hotel == null)
            {
                return Result.NotFound(new Error(ErrorCodes.NotFound, $"Hotel with Id {id} not found."));
            }
            context.Hotels.Remove(hotel);
            await context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception)
        {
            return Result.Failure(new Error(ErrorCodes.BadRequest, $"An error occurred while deleting the hotel"));
        }
    }

    public async Task<Result<PagedResult<GetHotelDto>>> GetAllHotelAsync(PaginationParameters paginationParameters, HotelFilterParameters filters)
    {
        var query = context.Hotels.AsQueryable().AsNoTracking(); // Start with the base query (filtering can be applied here)
        if (filters.CountryId.HasValue)
        {
            query = query.Where(r => r.CountryId == filters.CountryId);
        }
        if (filters.MinimumRating.HasValue)
        {
            query = query.Where(r => r.Rating >= filters.MinimumRating);
        }
        if (filters.MaximumRating.HasValue)
        {
            query = query.Where(r => r.Rating <= filters.MaximumRating);
        }
        if (filters.MinPrice.HasValue)
        {
            query = query.Where(r => r.PerNightRate >= filters.MinPrice);
        }
        if(filters.MaxPrice.HasValue)
        {
            query = query.Where(r => r.PerNightRate <= filters.MaxPrice);
        }
        if (!string.IsNullOrWhiteSpace(filters.Location))
        {
            query = query.Where(r => r.Address.Contains(filters.Location));
        }
        // generic search param
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            query = query.Where(r => r.Name.Contains(filters.Search) || r.Address.Contains(filters.Search)); // check if name or address contain a letter.
        }
        // to sort
        query = filters.SortBy?.ToLower() switch
        {
            "name" => filters.SortDescending ?
                query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            "rating" => filters.SortDescending ?
                query.OrderByDescending(r => r.Rating) : query.OrderBy(r => r.Rating),
            "price" => filters.SortDescending ?
                query.OrderByDescending(r => r.PerNightRate) : query.OrderBy(r => r.PerNightRate),
            _ => query.OrderBy(r => r.Name) // break statement i.e., order by name if nothing is returned.
        };

        var hotel = await query
       .Include(h => h.Country)
       .ProjectTo<GetHotelDto>(mapper.ConfigurationProvider)
       .ToPagedResultAsync(paginationParameters);

        return Result<PagedResult<GetHotelDto>>.Success(hotel);
    }

    public async Task<Result<GetHotelDto>> GetHotelAsync(int id)
    {
        var hotel = await context.Hotels
            .Include(h => h.Country)
            .Where(h => h.Id == id)
            .FirstOrDefaultAsync();
        var hotelDto = mapper.Map<GetHotelDto>(hotel);
        
        if (hotel is null)
        {
            return Result<GetHotelDto>.Failure(new Error(ErrorCodes.NotFound, $"Hotel with Id {id} not found."));
        }
        return Result<GetHotelDto>.Success(hotelDto);

    }

    public async Task<Result> UpdateHotelAsync(int id, UpdateHotelDto hotelDto)
    {
        // 1. Logic/Validation checks
        if (id != hotelDto.Id)
        {
            return Result.BadRequest(new Error(ErrorCodes.Validation, "Id from URL does not match Id from body."));
        }

        var hotel = await context.Hotels.FindAsync(id);
        if (hotel == null)
        {
            return Result.NotFound(new Error(ErrorCodes.NotFound, $"Hotel with Id {id} not found."));
        }

        var countryExists = await CountryExists(hotelDto.CountryId);
        if (!countryExists)
        {
            return Result.NotFound(new Error(ErrorCodes.NotFound, $"Country {hotelDto.CountryId} was not found."));
        }

        hotel.Name = hotelDto.Name;
        hotel.Address = hotelDto.Address;
        hotel.Rating = hotelDto.Rating;
        hotel.CountryId = hotelDto.CountryId;

        // If SaveChangesAsync fails (e.g., DB connection loss), 
        // the GlobalExceptionHandler will catch it automatically.
        context.Hotels.Update(hotel);
        await context.SaveChangesAsync();

        return Result.Success();
    }
    public async Task<bool> HotelExistsAsync(string name, int countryId)
    {
        var normalizedName = name.ToLower().Trim();
        return await context.Hotels.AnyAsync(h => h.Name.ToLower() == normalizedName && h.CountryId == countryId );
    }
    public async Task<bool> CountryExists(int countryId)
    {
        return await context.Countries.AnyAsync(c => c.CountryId == countryId);
    }
}