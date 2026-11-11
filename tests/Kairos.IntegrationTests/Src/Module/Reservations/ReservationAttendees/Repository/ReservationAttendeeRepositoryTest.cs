using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Repositories;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Reservations.ReservationAttendees.Repository;

[Collection("Integration Tests")]
public sealed class ReservationAttendeeRepositoryTest : BaseIntegrationTest
{
    public ReservationAttendeeRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    // ---------------------------------------------------------------
    // GetByReservationAndUserAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByReservationAndUserAsync_ReturnsAttendee_WhenAttendeeExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var attendee = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: user.Id);

        var result = await repository.GetByReservationAndUserAsync(
            reservation.Id,
            user.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(attendee.Id);
        result.ReservationId.Should().Be(reservation.Id);
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByReservationAndUserAsync_ReturnsNull_WhenAttendeeDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var result = await repository.GetByReservationAndUserAsync(
            reservation.Id,
            user.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByReservationAndUserAsync_ReturnsNull_WhenUserBelongsToAnotherReservation()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var anotherReservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            anotherReservation,
            userId: user.Id);

        var result = await repository.GetByReservationAndUserAsync(
            reservation.Id,
            user.Id);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // GetByIdAndReservationAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByIdAndReservationAsync_ReturnsAttendee_WhenIdAndReservationMatch()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var attendee = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.GetByIdAndReservationAsync(
            attendee.Id,
            reservation.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(attendee.Id);
        result.ReservationId.Should().Be(reservation.Id);
    }

    [Fact]
    public async Task GetByIdAndReservationAsync_ReturnsNull_WhenAttendeeDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var result = await repository.GetByIdAndReservationAsync(
            Guid.NewGuid().ToString(),
            reservation.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAndReservationAsync_ReturnsNull_WhenAttendeeBelongsToAnotherReservation()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var anotherReservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var attendee = await CreateAttendeeAsync(
            scope.ServiceProvider,
            anotherReservation);

        var result = await repository.GetByIdAndReservationAsync(
            attendee.Id,
            reservation.Id);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // GetByReservationIdAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByReservationIdAsync_ReturnsAttendees_WhenReservationHasAttendees()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var firstUser = await CreateUserAsync(scope.ServiceProvider);
        var secondUser = await CreateUserAsync(scope.ServiceProvider);

        var firstAttendee = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: firstUser.Id);

        var secondAttendee = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: secondUser.Id);

        var result = await repository.GetByReservationIdAsync(
            reservation.Id);

        result.Should().HaveCount(2);

        result.Should().Contain(
            attendee => attendee.Id == firstAttendee.Id);

        result.Should().Contain(
            attendee => attendee.Id == secondAttendee.Id);
    }

    [Fact]
    public async Task GetByReservationIdAsync_ReturnsEmptyList_WhenReservationHasNoAttendees()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var result = await repository.GetByReservationIdAsync(
            reservation.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByReservationIdAsync_IncludesUser_WhenAttendeesExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var attendee = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: user.Id);

        var result = await repository.GetByReservationIdAsync(
            reservation.Id);

        result.Should().ContainSingle();

        var returnedAttendee = result.Single();

        returnedAttendee.Id.Should().Be(attendee.Id);
        returnedAttendee.User.Should().NotBeNull();
        returnedAttendee.User!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByReservationIdAsync_OrdersByCreatedAtAscending()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var firstDate = DateTimeOffset.UtcNow.AddDays(-3);
        var secondDate = DateTimeOffset.UtcNow.AddDays(-2);
        var thirdDate = DateTimeOffset.UtcNow.AddDays(-1);

        var first = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            createdAt: firstDate);

        var second = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            createdAt: secondDate);

        var third = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            createdAt: thirdDate);

        var result = await repository.GetByReservationIdAsync(
            reservation.Id);

        result.Should().HaveCount(3);

        result.Select(attendee => attendee.Id)
            .Should()
            .ContainInOrder(
                first.Id,
                second.Id,
                third.Id);
    }

    [Fact]
    public async Task GetByReservationIdAsync_ReturnsEmptyList_WhenReservationDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.GetByReservationIdAsync(
            Guid.NewGuid().ToString());

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // ExistsByReservationAndUserAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByReservationAndUserAsync_ReturnsTrue_WhenAttendeeExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: user.Id);

        var exists = await repository.ExistsByReservationAndUserAsync(
            reservation.Id,
            user.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByReservationAndUserAsync_ReturnsFalse_WhenAttendeeDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var exists = await repository.ExistsByReservationAndUserAsync(
            reservation.Id,
            user.Id);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByReservationAndUserAsync_ReturnsFalse_WhenAttendeeBelongsToAnotherReservation()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var anotherReservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            anotherReservation,
            userId: user.Id);

        var exists = await repository.ExistsByReservationAndUserAsync(
            reservation.Id,
            user.Id);

        exists.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // CountOccupyingByReservationAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task CountOccupyingByReservationAsync_ReturnsZero_WhenReservationHasNoAttendees()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var result = await repository.CountOccupyingByReservationAsync(
            reservation.Id);

        result.Should().Be(0);
    }

    [Fact]
    public async Task CountOccupyingByReservationAsync_CountsAllAttendeesExceptDeclined()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            status: AttendeeStatusEnum.Pending);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            status: AttendeeStatusEnum.Pending);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            status: AttendeeStatusEnum.Declined);

        var result = await repository.CountOccupyingByReservationAsync(
            reservation.Id);

        result.Should().Be(2);
    }

    [Fact]
    public async Task CountOccupyingByReservationAsync_DoesNotCountAttendeesFromAnotherReservation()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var anotherReservation = await CreateReservationAsync(
            scope.ServiceProvider);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            anotherReservation);

        var result = await repository.CountOccupyingByReservationAsync(
            reservation.Id);

        result.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var act = async () => await repository.Find(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ---------------------------------------------------------------
    // Find - BaseFilter
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersById_WhenIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var first = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                Id = first.Id
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == first.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByVersionRange_WhenVersionRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            version: 5);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            version: 2);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                VersionMin = 5,
                VersionMax = 5
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatedAtRange_WhenDateRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-2);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            createdAt: targetDate);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            createdAt: targetDate.AddDays(-5));

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                CreatedAtMin = targetDate.AddMinutes(-1),
                CreatedAtMax = targetDate.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByUpdatedAtRange_WhenDateRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-2);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            updatedAt: targetDate);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            updatedAt: targetDate.AddDays(-5));

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                UpdatedAtMin = targetDate.AddMinutes(-1),
                UpdatedAtMax = targetDate.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Reservation
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByReservationId_WhenReservationIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var anotherReservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            anotherReservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByReservationTitle_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var title = $"Integration Reservation {Guid.NewGuid():N}";

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            title: title);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                ReservationTitle = title.ToLowerInvariant(),
                ReservationTitleIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);
    }

    [Fact]
    public async Task Find_FiltersByReservationStatus_WhenStatusIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            status: ReservationStatusEnum.Confirmed);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                ReservationStatus = ReservationStatusEnum.Confirmed
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);
    }

    [Fact]
    public async Task Find_FiltersByReservationStartsAtRange_WhenRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var targetStart = DateTimeOffset.UtcNow.AddDays(10);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider,
            startsAt: targetStart);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var anotherReservation = await CreateReservationAsync(
            scope.ServiceProvider,
            startsAt: targetStart.AddDays(10));

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            anotherReservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationStartsAtFrom = targetStart.AddMinutes(-1),
                ReservationStartsAtTo = targetStart.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);
        
    }

    // ---------------------------------------------------------------
    // Find - User
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByUserId_WhenUserIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(scope.ServiceProvider);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: user.Id);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                UserId = user.Id
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);
    }

    [Fact]
    public async Task Find_FiltersByUserName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.UserName = $"Attendee-{Guid.NewGuid():N}");

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: user.Id);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                UserName = user.UserName!.ToLowerInvariant(),
                UserNameIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);
    }

    [Fact]
    public async Task Find_FiltersByEmail_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var user = await CreateUserAsync(
            scope.ServiceProvider,
            model => model.Email = $"{Guid.NewGuid():N}@example.com");

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            userId: user.Id);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                Email = user.Email!.ToUpperInvariant(),
                EmailIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);
    }

    // ---------------------------------------------------------------
    // Find - Role, status and inviter
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByRole_WhenRoleIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        AttendeeRoleEnum anotherRole = Enum
            .GetValues<AttendeeRoleEnum>()
            .First(role => role != AttendeeRoleEnum.Required);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            role: AttendeeRoleEnum.Required);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            role: anotherRole);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                Role = AttendeeRoleEnum.Required
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByStatus_WhenStatusIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            status: AttendeeStatusEnum.Pending);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            status: AttendeeStatusEnum.Declined);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                Status = AttendeeStatusEnum.Pending
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByInvitedBy_WhenInviterIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var inviter = await CreateUserAsync(scope.ServiceProvider);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            invitedBy: inviter.Id);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                InvitedBy = inviter.Id
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_IncludesInviter_WhenAttendeeHasInviter()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var inviter = await CreateUserAsync(scope.ServiceProvider);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            invitedBy: inviter.Id);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                Id = expected.Id
            });

        result.Data.Should().ContainSingle();

        var attendee = result.Data.Single();

        attendee.Inviter.Should().NotBeNull();
        attendee.Inviter!.Id.Should().Be(inviter.Id);
    }

    // ---------------------------------------------------------------
    // Find - Response and check-in dates
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByRespondedAtRange_WhenRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-2);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            configure: attendee =>
                attendee.RespondedAt = targetDate);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            configure: attendee =>
                attendee.RespondedAt = targetDate.AddDays(-5));

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                RespondedAtFrom = targetDate.AddMinutes(-1),
                RespondedAtTo = targetDate.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCheckedInAtRange_WhenRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-2);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            configure: attendee =>
                attendee.CheckedInAt = targetDate);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation,
            configure: attendee =>
                attendee.CheckedInAt = targetDate.AddDays(-5));

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                CheckedInAtFrom = targetDate.AddMinutes(-1),
                CheckedInAtTo = targetDate.AddMinutes(1)
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Pagination
    // ---------------------------------------------------------------
    
    [Fact]
    public async Task Find_ReturnsDefaultPagination_WhenPaginationIsNotProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        var expected = await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id
            });

        result.Data.Should().ContainSingle(
            attendee => attendee.Id == expected.Id);

        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Find_ClampsPageNumberToOne_WhenPageIsZero()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                Page = 0
            });

        result.PageNumber.Should().Be(1);
        result.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task Find_ClampsPageSizeToMaximum_WhenPageSizeExceedsLimit()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                Page = 1,
                PageSize = 1000
            });

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenPageIsBeyondAvailableResults()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var reservation = await CreateReservationAsync(
            scope.ServiceProvider);

        await CreateAttendeeAsync(
            scope.ServiceProvider,
            reservation);

        var result = await repository.Find(
            new FilterReservationAttendeeDto
            {
                ReservationId = reservation.Id,
                Page = 10,
                PageSize = 20
            });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(10);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static IReservationAttendeeRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IReservationAttendeeRepository>();
    }

    private async Task<ReservationModel> CreateReservationAsync(
        IServiceProvider services,
        DateTimeOffset? startsAt = null,
        ReservationStatusEnum status = ReservationStatusEnum.Confirmed,
        string? title = null)
    {
        var user = await CreateUserAsync(services);
        var room = await CreateRoomAsync(services);
        var category = await CreateCategoryAsync(services);

        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(10);

        var reservation = new ReservationModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = room.Id,
            UserId = user.Id,
            CategoryId = category.Id,
            Title = title ?? $"Integration Reservation {Guid.NewGuid():N}",
            StartsAt = start,
            EndsAt = start.AddHours(1),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var context = services.GetRequiredService<AppDbContext>();

        await context.Set<ReservationModel>()
            .AddAsync(reservation);

        await context.SaveChangesAsync();

        return reservation;
    }

    private async Task<ReservationAttendeeModel> CreateAttendeeAsync(
        IServiceProvider services,
        ReservationModel reservation,
        string? userId = null,
        AttendeeRoleEnum role = AttendeeRoleEnum.Required,
        AttendeeStatusEnum status = AttendeeStatusEnum.Pending,
        string? invitedBy = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        long version = 1,
        Action<ReservationAttendeeModel>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var user = userId is null
            ? await CreateUserAsync(services)
            : null;

        var creationDate = createdAt ?? DateTimeOffset.UtcNow;

        var attendee = new ReservationAttendeeModel
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = reservation.Id,
            UserId = userId ?? user!.Id,
            Role = role,
            Status = status,
            InvitedBy = invitedBy,
            CreatedAt = creationDate,
            UpdatedAt = updatedAt,
            Version = version
        };

        configure?.Invoke(attendee);

        var context = services.GetRequiredService<AppDbContext>();

        await context.Set<ReservationAttendeeModel>()
            .AddAsync(attendee);

        await context.SaveChangesAsync();

        // Restore deterministic audit values after SaveChanges.
        // This avoids depending on automatic audit behavior in tests.
        await context.Set<ReservationAttendeeModel>()
            .Where(item => item.Id == attendee.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.CreatedAt, creationDate)
                .SetProperty(item => item.UpdatedAt, updatedAt)
                .SetProperty(item => item.Version, version));

        return attendee;
    }
}
