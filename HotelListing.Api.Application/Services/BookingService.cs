using HotelListing.Api.Application.DTOs.Booking;
using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.Common.Constants;
using HotelListing.Api.Common.Enum;
using HotelListing.Api.Common.Models.Extensions;
using HotelListing.Api.Common.Models.Paging;
using HotelListing.Api.Common.Results;
using HotelListing.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HotelListing.Api.Application.Services;

public class BookingService(HotelListingDbContext context, IUsersService usersService) : IBookingService
{
    public async Task<Result<PagedResult<GetBookingDto>>> GetBookingsForHotelAsync(int hotelId, PaginationParameters paginationParameters)
    {
        var hotelExists = await context.Hotels.AnyAsync(h => h.Id == hotelId);
        if (!hotelExists)
        {
            return Result<PagedResult<GetBookingDto>>.Failure(new Error(ErrorCodes.NotFound, $"Hotel {hotelId} was not found. "));
        }
        var bookings = await context.Bookings
            .Where(b => b.HotelId == hotelId)
            .OrderBy(b => b.CheckIn)
            .Select(b => new GetBookingDto(
                b.Id,
                b.HotelId,
                b.Hotel!.Name,
                b.CheckIn,
                b.CheckOut,
                b.NumberOfGuests,
                b.TotalPrice,
                b.Status.ToString(),
                b.CreatedAt,
                b.UpdatedAt
                ))
            .ToPagedResultAsync(paginationParameters);

        return Result<PagedResult<GetBookingDto>>.Success(bookings);
    }
    public async Task<Result<PagedResult<GetBookingDto>>> GetUserBookingsForHotelAsync(int hotelId, PaginationParameters paginationParameters)
    {
        var userId = usersService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Result<PagedResult<GetBookingDto>>.Failure(new Error(ErrorCodes.Validation, "User is not authorized to view bookings."));
        }
        var hotelExists = await context.Hotels.AnyAsync(h => h.Id == hotelId);
        if (!hotelExists)
        {
            return Result<PagedResult<GetBookingDto>>.Failure(new Error(ErrorCodes.NotFound, $"Hotel {hotelId} was not found. "));
        }
        var bookings = await context.Bookings
            .Where(b => b.HotelId == hotelId && b.UserId == userId)
            .OrderBy(b => b.CheckIn)
            .Select(b => new GetBookingDto(
                b.Id,
                b.HotelId,
                b.Hotel!.Name,
                b.CheckIn,
                b.CheckOut,
                b.NumberOfGuests,
                b.TotalPrice,
                b.Status.ToString(),
                b.CreatedAt,
                b.UpdatedAt
                ))
            .ToPagedResultAsync(paginationParameters);
        return Result<PagedResult<GetBookingDto>>.Success(bookings);
    }
    public async Task<Result<GetBookingDto>> CreateBookingAsync(CreateBookingDto bookingDto)
    {
        var userId = usersService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Validation, "User is not authorized to create a booking."));
        }

        var nights = bookingDto.CheckOut.DayNumber - bookingDto.CheckIn.DayNumber;
        if (nights <= 0)
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Validation, "Check-out must be later than Check-in"));
        }

        var hotel = await context.Hotels
            .Where(h => h.Id == bookingDto.HotelId)
            .FirstOrDefaultAsync();

        if (hotel == null)
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.NotFound, $"Hotel {bookingDto.HotelId} was not found."));
        }

        var totalPrice = hotel.PerNightRate * nights * bookingDto.NumberOfGuests;

        var overlaps = await context.Bookings.AnyAsync(
            b => b.HotelId == bookingDto.HotelId
                && b.Status != BookingStatus.Cancelled
                && bookingDto.CheckIn < b.CheckOut
                && bookingDto.CheckOut > b.CheckIn
                && b.UserId == userId);

        if (overlaps)
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Conflict, "You already have a booking that overlaps with the selected dates."));
        }

        var booking = new Booking
        {
            HotelId = bookingDto.HotelId,
            UserId = userId,
            CheckIn = bookingDto.CheckIn,
            CheckOut = bookingDto.CheckOut,
            NumberOfGuests = bookingDto.NumberOfGuests,
            TotalPrice = totalPrice,
            Status = BookingStatus.Pending,
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var createdBookingDto = new GetBookingDto(
            booking.Id,
            hotel.Id,
            hotel.Name,
            booking.CheckIn,
            booking.CheckOut,
            booking.NumberOfGuests,
            totalPrice,
            booking.Status.ToString(),
            booking.CreatedAt,
            booking.UpdatedAt
            );

        return Result<GetBookingDto>.Success(createdBookingDto);

    }

    public async Task<Result<GetBookingDto>> UpdateBookingAsync(int hotelId, int bookingId, UpdateBookingDto bookingDto)
    {
        var userId = usersService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Validation, "User is not authorized to create a booking."));
        }

        var nights = bookingDto.CheckOut.DayNumber - bookingDto.CheckIn.DayNumber;

        // since booking is per hotel, we need to check if the overlap exists

        var overlaps = await context.Bookings.AnyAsync(
            b => b.HotelId == hotelId
                && b.Id != bookingId
                && b.Status != BookingStatus.Cancelled
                && bookingDto.CheckIn < b.CheckOut
                && bookingDto.CheckOut > b.CheckIn
                && b.UserId == userId);

        if (overlaps)
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Conflict, "You already have a booking that overlaps with the selected dates."));
        }

        var booking = await context.Bookings
            .Include(b => b.Hotel)
            .Where(b => b.Id == bookingId && b.HotelId == hotelId && b.UserId == userId)
            .FirstOrDefaultAsync();

        if (booking == null)
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.NotFound, $"Booking {bookingId} for Hotel {hotelId} was not found."));
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result<GetBookingDto>.Failure(new Error(ErrorCodes.Conflict, "Cancelled bookings cannot be modified"));
        }

        var perNightRate = booking.Hotel!.PerNightRate;
        booking.CheckIn = bookingDto.CheckIn;
        booking.CheckOut = bookingDto.CheckOut;
        booking.NumberOfGuests = bookingDto.NumberOfGuests;
        booking.TotalPrice = perNightRate * nights * bookingDto.NumberOfGuests;
        booking.UpdatedAt = DateTime.UtcNow;

        context.Bookings.Update(booking);
        await context.SaveChangesAsync();

        var updatedBookingDto = new GetBookingDto(
            booking.Id,
            booking.HotelId,
            booking.Hotel!.Name,
            booking.CheckIn,
            booking.CheckOut,
            booking.NumberOfGuests,
            booking.TotalPrice,
            booking.Status.ToString(),
            booking.CreatedAt,
            booking.UpdatedAt
            );

        return Result<GetBookingDto>.Success(updatedBookingDto);
    }

    public async Task<Result> CancelBookingAsync(int hotelId, int bookingId)
    {
        var userId = usersService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Result.Failure(new Error(ErrorCodes.Validation, "User is not authorized to cancel a booking."));
        }
        var booking = await context.Bookings
            .Where(b => b.Id == bookingId && b.HotelId == hotelId && b.UserId == userId)
            .FirstOrDefaultAsync();
        if (booking == null)
        {
            return Result.NotFound(new Error(ErrorCodes.NotFound, $"Booking {bookingId} for Hotel {hotelId} was not found."));
        }
        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result.BadRequest(new Error(ErrorCodes.Conflict, "This booking has already been cancelled."));
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        context.Bookings.Update(booking);
        await context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> AdminCancelBookingAsync(int hotelId, int bookingId)
    {
        var userId = usersService.UserId;

        var isHotelAdminUser = await context.HotelAdmins
            .AnyAsync(q => q.UserId == userId && q.HotelId == hotelId);

        if (!isHotelAdminUser)
        {
            return Result.Failure(new Error(ErrorCodes.Forbid, "You are not an admin of the selected hotel"));
        }

        var booking = await context.Bookings
                .Include(b => b.Hotel)
                .Where(b => b.Id == bookingId && b.HotelId == hotelId)
                .FirstOrDefaultAsync();

        if (booking == null)
        {
            return Result.NotFound(new Error(ErrorCodes.NotFound, $"Booking {bookingId} was not found."));
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result.BadRequest(new Error(ErrorCodes.Conflict, "This booking has already been cancelled."));
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> AdminConfirmBookingAsync(int hotelId, int bookingId)
    {
        var userId = usersService.UserId;

        var isHotelAdminUser = await context.HotelAdmins
            .AnyAsync(q => q.UserId == userId && q.HotelId == hotelId);

        if (!isHotelAdminUser)
        {
            return Result.Failure(new Error(ErrorCodes.Forbid, "You are not an admin of the selected hotel"));
        }

        var booking = await context.Bookings
                .Include(b => b.Hotel)
                .Where(b => b.Id == bookingId && b.HotelId == hotelId)
                .FirstOrDefaultAsync();

        if (booking == null)
        {
            return Result.NotFound(new Error(ErrorCodes.NotFound, $"Booking {bookingId} was not found."));
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result.BadRequest(new Error(ErrorCodes.Conflict, "This booking has already been cancelled."));
        }

        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Result.Success();
    }
}