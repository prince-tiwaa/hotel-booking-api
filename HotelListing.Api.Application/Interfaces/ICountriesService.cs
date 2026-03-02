using Azure;
using HotelListing.Api.Application.DTOs.Country;
using HotelListing.Api.Common.Models.Filtering;
using HotelListing.Api.Common.Models.Paging;
using HotelListing.Api.Common.Results;
using Microsoft.AspNetCore.JsonPatch;

namespace HotelListing.Api.Application.Interfaces
{
    public interface ICountriesService
    {
        Task<bool> CountryExistsAsync(string name);
        Task<Result<GetCountryDto>> CreateCountryAsync(CreateCountryDto countryDto);
        Task<Result> DeleteCountryAsync(int id);
        Task<Result<IEnumerable<GetCountriesDto>>> GetAllAsync(CountryFilterParameters filters);
        Task<Result<GetCountryDto>> GetCountryAsync(int id);
        Task<Result<GetCountryHotelsDto>> GetCountryHotelsAsync(int countryId,
            PaginationParameters paginationParameters, CountryFilterParameters filters);
        Task<Result> UpdateCountryAsync(int id, UpdateCountryDto countryDto);
        Task<Result> PatchCountryAsync(int id, JsonPatchDocument<UpdateCountryDto> patchDoc); // JSON Patch
    }
}