using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Reservations.Reservation;

[Collection("Integration Tests")]
public sealed class ReservationRepositoryTest : BaseIntegrationTest
{
    public ReservationRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetByIdempotencyKeyAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);
    
        var result = await repository.GetByIdempotencyKeyAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());
    
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetByIdempotencyKeyAsync_ReturnsReservation_WhenKeyExists()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);
    
        var idempotencyKey = Guid.NewGuid().ToString("N");
    
        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            idempotencyKey: idempotencyKey);
    
        var result = await repository.GetByIdempotencyKeyAsync(
            reservation.UserId,
            idempotencyKey);
    
        result.Should().NotBeNull();
        result!.Id.Should().Be(reservation.Id);
        result.IdempotencyKey.Should().Be(idempotencyKey);
    }
    
    [Fact]
    public async Task GetByUserIdAsync_ReturnsReservations_WhenUserHasReservations()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);
    
        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);
    
        var result = await repository.GetByUserIdAsync(
            reservation.UserId);
    
        result.Should().ContainSingle(
            item => item.Id == reservation.Id);
    }
    
    [Fact]
    public async Task GetConflictingByRoomAsync_ReturnsReservation_WhenPeriodsOverlap()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);
    
        var startsAt = DateTimeOffset.UtcNow.AddDays(2);
        var endsAt = startsAt.AddHours(1);
    
        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            startsAt: startsAt,
            endsAt: endsAt);
    
        var result = await repository.GetConflictingByRoomAsync(
            reservation.RoomId,
            startsAt.AddMinutes(30),
            endsAt.AddMinutes(30));
    
        result.Should().ContainSingle(
            item => item.Id == reservation.Id);
    }
    
    [Fact]
    public async Task GetConflictingByRoomAsync_DoesNotReturnReservation_WhenPeriodsDoNotOverlap()
    {
        using var scope = CreateScope();
        var repository = GetRepository(scope);
    
        var startsAt = DateTimeOffset.UtcNow.AddDays(2);
        var endsAt = startsAt.AddHours(1);
    
        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            startsAt: startsAt,
            endsAt: endsAt);
    
        var result = await repository.GetConflictingByRoomAsync(
            reservation.RoomId,
            endsAt,
            endsAt.AddHours(1));
    
        result.Should().NotContain(
            item => item.Id == reservation.Id);
    }
    
    private static IReservationRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IReservationRepository>();
    }
    
    private async Task<ReservationModel> CreateReservationAsync(
        IServiceProvider services,
        string? userId = null,
        string? roomId = null,
        string? categoryId = null,
        string? idempotencyKey = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        ReservationStatusEnum status = ReservationStatusEnum.Confirmed)
    {
        var user = userId is null
            ? await CreateUserAsync(services)
            : null;
    
        var room = roomId is null
            ? await CreateRoomAsync(services)
            : null;
    
        var category = categoryId is null
            ? await CreateCategoryAsync(services)
            : null;
    
        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(2);
    
        var reservation = new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId ?? room!.Id,
            UserId = userId ?? user!.Id,
            CategoryId = categoryId ?? category!.Id,
            Title = $"Integration Reservation {Guid.NewGuid():N}",
            StartsAt = start,
            EndsAt = endsAt ?? start.AddHours(1),
            Status = status,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow
        };
    
        var dbContext = services.GetRequiredService<AppDbContext>();
    
        await dbContext.Set<ReservationModel>().AddAsync(reservation);
        await dbContext.SaveChangesAsync();
    
        return reservation;
    }
}
