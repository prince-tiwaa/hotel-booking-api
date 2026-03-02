using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace HotelListing.Api.Common.Models.Paging;

public class PaginationParameters
{
    private const int MaxPageSize = 50; // Maximum allowed page size
    private int _pageSize = 10; // Default page size
    [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
    public int PageNumber { get; set; } = 1; // Default page number
    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between 1 and 50 ")]
    public int PageSize
    { get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value; // Enforce maximum page size
    }
}

public class PagedResult<T>
{
    public IEnumerable<T> Data { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = new();
}

public class PaginationMetadata
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNext {  get; set; }
    public bool HasPrevious { get; set; }
}
