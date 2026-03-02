using Asp.Versioning;
using HotelListing.Api.Application.DTOs.Country;
using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.Common.Models.Filtering;
using HotelListing.Api.Common.Models.Paging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace HotelListing.Api.Controllers;

/// <summary>
/// Provides API endpoints for managing countries and their related hotel information. Supports operations such as
/// retrieving, creating, updating, and deleting country records, as well as querying hotels within a specific country.
/// </summary>
/// <remarks>This controller is versioned at 1.0 and applies rate limiting to its endpoints. Administrative
/// privileges are required for operations that modify country data. Endpoints support filtering, pagination, and
/// partial updates where applicable.</remarks>
/// <param name="countriesService">The service used to perform country-related operations, including data retrieval and modification.</param>

[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[ApiVersion("1.0")]
[EnableRateLimiting("fixed")]
public class CountriesController(ICountriesService countriesService) : BaseApiController
{
    /// <summary>
    /// Retrieves a collection of countries that match the specified filter criteria.
    /// </summary>
    /// <param name="filters">The filter parameters to apply when retrieving countries. May include criteria such as name, region, or status.</param>
    /// <returns>An asynchronous operation that returns an <see cref="ActionResult{T}"/> containing a collection of <see
    /// cref="GetCountriesDto"/> objects that match the provided filters. Returns an empty collection if no countries
    /// match the criteria.</returns>
    [HttpGet]
    [OutputCache]
    public async Task<ActionResult<IEnumerable<GetCountriesDto>>> GetCountries(
        [FromQuery] CountryFilterParameters filters)
    {
        var countries = await countriesService.GetAllAsync(filters); 
        return ToActionResult(countries);
    }

    /// <summary>
    /// Retrieves a paginated list of hotels for the specified country, applying optional filtering criteria.
    /// </summary>
    /// <param name="countryId">The unique identifier of the country for which to retrieve hotels.</param>
    /// <param name="paginationParameters">The pagination settings that determine the page size and number of results to return.</param>
    /// <param name="filters">The filtering criteria to apply when retrieving hotels, such as name or rating filters.</param>
    /// <returns>An asynchronous operation that returns an <see cref="ActionResult{GetCountryHotelsDto}"/> containing the
    /// paginated list of hotels for the specified country. Returns a 404 response if the country is not found.</returns>
    /// <response code="200"></response>
    /// <response code="404">Country not found</response>
    [HttpGet("{countryId:int}/hotels")]
    public async Task<ActionResult<GetCountryHotelsDto>> GetCountryHotels(
        [FromRoute] int countryId,
        [FromQuery] PaginationParameters paginationParameters,
        [FromQuery] CountryFilterParameters filters)
    {
        var country = await countriesService.GetCountryHotelsAsync(countryId, paginationParameters, filters);
        return ToActionResult(country);
    }

    /// <summary>
    /// Retrieves the details of a country with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the country to retrieve.</param>
    /// <returns>An <see cref="ActionResult{GetCountryDto}"/> containing the country details if found; otherwise, a 404 Not Found
    /// response.</returns>
    // GET: api/Countries/5
    [HttpGet("{id}")]
    public async Task<ActionResult<GetCountryDto>> GetCountry(int id)
    {
        var country = await countriesService.GetCountryAsync(id);
        return ToActionResult(country);

    }

    /// <summary>
    /// Updates the details of an existing country with the specified identifier.
    /// </summary>
    /// <remarks>This action requires the caller to have the 'Administrator' role. The country must exist;
    /// otherwise, a 404 Not Found response is returned. The request body must contain valid country data.</remarks>
    /// <param name="id">The unique identifier of the country to update.</param>
    /// <param name="countryDto">An object containing the updated country information. Cannot be null.</param>
    /// <returns>An <see cref="ActionResult"/> indicating the result of the update operation. Returns 204 No Content if the
    /// update is successful, or an appropriate error response if the operation fails.</returns>
    // PUT: api/Countries/5
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> PutCountry(int id, UpdateCountryDto countryDto)
    {
        var result = await countriesService.UpdateCountryAsync(id, countryDto);
        return ToActionResult(result);
    }

    /// <summary>
    /// Applies a JSON Patch document to update an existing country resource.
    /// </summary>
    /// <remarks>This action requires the caller to have the Administrator role. The patch document must
    /// conform to the structure of <see cref="UpdateCountryDto"/>. Only the fields specified in the patch document will
    /// be updated.</remarks>
    /// <param name="id">The identifier of the country to update.</param>
    /// <param name="patchDoc">The JSON Patch document containing the operations to apply to the country. Cannot be null.</param>
    /// <returns>An <see cref="ActionResult"/> indicating the result of the operation. Returns 200 OK if the update is
    /// successful, 400 Bad Request if the patch document is invalid, or an appropriate error response if the update
    /// fails.</returns>
    [HttpPatch("{id}")]
    [Authorize(Roles =RoleNames.Administrator)]
    public async Task<ActionResult> PatchCountry(int id, [FromBody] JsonPatchDocument<UpdateCountryDto> patchDoc)
    {
        if (patchDoc == null)
        {
            return BadRequest("Patch document is required. ");
        }

        var result = await countriesService.PatchCountryAsync(id, patchDoc);
        return ToActionResult(result);
    }

    /// <summary>
    /// Creates a new country using the specified data and returns the created country details.
    /// </summary>
    /// <remarks>This action requires the caller to be authorized with the "Administrator" role. On success,
    /// the response includes a Location header with the URI of the newly created country resource.</remarks>
    /// <param name="countryDto">The data for the country to create. Must not be null.</param>
    /// <returns>An <see cref="ActionResult{T}"/> containing the details of the created country if successful; otherwise, an
    /// error response describing why the creation failed.</returns>
    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<GetCountryDto>> PostCountry(CreateCountryDto countryDto)
    {
        var result = await countriesService.CreateCountryAsync(countryDto);
        if (!result.IsSuccess) return MapErrorsToResponse(result.Errors);

        return CreatedAtAction(nameof(GetCountry), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Deletes the country with the specified identifier.
    /// </summary>
    /// <remarks>This action requires the caller to have the Administrator role. Only users with the
    /// appropriate authorization can perform this operation.</remarks>
    /// <param name="id">The unique identifier of the country to delete.</param>
    /// <returns>An <see cref="IActionResult"/> that represents the result of the delete operation. Returns <see
    /// cref="OkResult"/> if the country was deleted successfully; otherwise, returns an appropriate error response such
    /// as <see cref="NotFoundResult"/> if the country does not exist.</returns>
    // DELETE: api/Countries/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteCountry(int id)
    {
        var result = await countriesService.DeleteCountryAsync(id);
        return ToActionResult(result);
    }
}
