using FluentAssertions;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Room.RoomOperatingHour.Repository;

[Collection("Integration Tests")]
public sealed class RoomOperatingHourRepositoryTest : BaseIntegrationTest
{
    public RoomOperatingHourRepositoryTest(KairosApiFactory factory) : base(factory) { }

    // ---------------------------------------------------------------
    // GetByRoomIdAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByRoomIdAsync_ReturnsOperatingHours_WhenRoomHasOperatingHours()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var monday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday);

        var wednesday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Wednesday);

        var friday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Friday);

        var result = await repository.GetByRoomIdAsync(room.Id);

        result.Should().HaveCount(3);

        result.Should().Contain(hour => hour.Id == monday.Id);
        result.Should().Contain(hour => hour.Id == wednesday.Id);
        result.Should().Contain(hour => hour.Id == friday.Id);

        result.Select(hour => hour.DayOfWeek)
            .Should()
            .ContainInOrder(
                DayOfWeek.Monday,
                DayOfWeek.Wednesday,
                DayOfWeek.Friday);
    }

    [Fact]
    public async Task GetByRoomIdAsync_ReturnsEmptyList_WhenRoomHasNoOperatingHours()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var result = await repository.GetByRoomIdAsync(room.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRoomIdAsync_DoesNotReturnOperatingHoursFromAnotherRoom()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var expectedRoom = await CreateRoomAsync(scope.ServiceProvider);
        var otherRoom = await CreateRoomAsync(scope.ServiceProvider);

        var expectedHour = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            expectedRoom);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            otherRoom);

        var result = await repository.GetByRoomIdAsync(expectedRoom.Id);

        result.Should().ContainSingle(
            hour => hour.Id == expectedHour.Id);

        result.Should().NotContain(
            hour => hour.RoomId == otherRoom.Id);
    }

    [Fact]
    public async Task GetByRoomIdAsync_OrdersByDayOfWeekAndOpeningTime()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var mondayAfternoon = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(13, 0),
            closesAt: new TimeOnly(18, 0));

        var friday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Friday);

        var mondayMorning = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0));

        var result = await repository.GetByRoomIdAsync(room.Id);

        result.Should().HaveCount(3);

        result.Select(hour => hour.Id)
            .Should()
            .ContainInOrder(
                mondayMorning.Id,
                mondayAfternoon.Id,
                friday.Id);
    }

    [Fact]
    public async Task GetByRoomIdAsync_ReturnsDetachedEntities()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.GetByRoomIdAsync(room.Id);

        result.Should().ContainSingle();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        foreach (var hour in result)
        {
            context.Entry(hour).State.Should().Be(EntityState.Detached);
        }
    }

    // ---------------------------------------------------------------
    // GetByRoomIdAndDayAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByRoomIdAndDayAsync_ReturnsOperatingHours_WhenRoomAndDayMatch()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var morning = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0));

        var afternoon = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(13, 0),
            closesAt: new TimeOnly(18, 0));

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday);

        var result = await repository.GetByRoomIdAndDayAsync(
            room.Id,
            DayOfWeek.Monday);

        result.Should().HaveCount(2);

        result.Select(hour => hour.Id)
            .Should()
            .ContainInOrder(morning.Id, afternoon.Id);

        result.Should().OnlyContain(
            hour => hour.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public async Task GetByRoomIdAndDayAsync_ReturnsEmptyList_WhenDayHasNoOperatingHours()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday);

        var result = await repository.GetByRoomIdAndDayAsync(
            room.Id,
            DayOfWeek.Sunday);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRoomIdAndDayAsync_ReturnsEmptyList_WhenRoomDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.GetByRoomIdAndDayAsync(
            Guid.NewGuid().ToString(),
            DayOfWeek.Monday);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRoomIdAndDayAsync_DoesNotReturnOperatingHoursFromAnotherRoom()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var expectedRoom = await CreateRoomAsync(scope.ServiceProvider);
        var otherRoom = await CreateRoomAsync(scope.ServiceProvider);

        var expectedHour = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            expectedRoom,
            dayOfWeek: DayOfWeek.Monday);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            otherRoom,
            dayOfWeek: DayOfWeek.Monday);

        var result = await repository.GetByRoomIdAndDayAsync(
            expectedRoom.Id,
            DayOfWeek.Monday);

        result.Should().ContainSingle(
            hour => hour.Id == expectedHour.Id);

        result.Should().OnlyContain(
            hour => hour.RoomId == expectedRoom.Id);
    }

    [Fact]
    public async Task GetByRoomIdAndDayAsync_ReturnsDetachedEntities()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday);

        var result = await repository.GetByRoomIdAndDayAsync(
            room.Id,
            DayOfWeek.Monday);

        result.Should().ContainSingle();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        foreach (var hour in result)
        {
            context.Entry(hour).State.Should().Be(EntityState.Detached);
        }
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

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                Id = expected.Id
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByVersionRange_WhenVersionRangeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            version: 2);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            version: 5);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Wednesday,
            version: 10);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                VersionMin = 4,
                VersionMax = 6
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatedAtMin_WhenMinimumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-10);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            createdAt: targetDate.AddDays(-5));

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            createdAt: targetDate);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                CreatedAtMin = targetDate
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByCreatedAtMax_WhenMaximumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-10);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            createdAt: targetDate);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            createdAt: targetDate.AddDays(5));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                CreatedAtMax = targetDate
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByUpdatedAtMin_WhenMinimumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-10);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            updatedAt: targetDate.AddDays(-5));

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            updatedAt: targetDate);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                UpdatedAtMin = targetDate
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByUpdatedAtMax_WhenMaximumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var targetDate = DateTimeOffset.UtcNow.AddDays(-10);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            updatedAt: targetDate);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            updatedAt: targetDate.AddDays(5));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                UpdatedAtMax = targetDate
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Room
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByRoomId_WhenRoomIdIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var expectedRoom = await CreateRoomAsync(scope.ServiceProvider);
        var otherRoom = await CreateRoomAsync(scope.ServiceProvider);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            expectedRoom);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            otherRoom);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = expectedRoom.Id
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByRoomName_WhenIgnoreCaseIsEnabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var roomName = $"Integration Room {Guid.NewGuid():N}";

        var room = await CreateRoomAsync(
            scope.ServiceProvider,
            model => model.Name = roomName);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                RoomName = roomName.ToLowerInvariant(),
                RoomNameIgnoreCase = true
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByRoomName_WhenIgnoreCaseIsDisabled()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var roomName = $"Integration Room {Guid.NewGuid():N}";

        var room = await CreateRoomAsync(
            scope.ServiceProvider,
            model => model.Name = roomName);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                RoomName = roomName,
                RoomNameIgnoreCase = false
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);
    }

    // ---------------------------------------------------------------
    // Find - Day of week
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByDayOfWeek_WhenDayIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Wednesday);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                DayOfWeek = DayOfWeek.Wednesday
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.Data.Should().OnlyContain(
            hour => hour.DayOfWeek == DayOfWeek.Wednesday);
    }

    // ---------------------------------------------------------------
    // Find - Opening time
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByOpensAtFrom_WhenMinimumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0));

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            opensAt: new TimeOnly(10, 0),
            closesAt: new TimeOnly(18, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                OpensAtFrom = new TimeOnly(10, 0)
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByOpensAtTo_WhenMaximumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0));

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            opensAt: new TimeOnly(10, 0),
            closesAt: new TimeOnly(18, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                OpensAtTo = new TimeOnly(8, 0)
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByOpensAtRange_WhenMinimumAndMaximumAreProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(7, 0),
            closesAt: new TimeOnly(12, 0));

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            opensAt: new TimeOnly(10, 0),
            closesAt: new TimeOnly(18, 0));

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Wednesday,
            opensAt: new TimeOnly(14, 0),
            closesAt: new TimeOnly(20, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                OpensAtFrom = new TimeOnly(9, 0),
                OpensAtTo = new TimeOnly(11, 0)
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Closing time
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByClosesAtFrom_WhenMinimumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0));

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            opensAt: new TimeOnly(10, 0),
            closesAt: new TimeOnly(18, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                ClosesAtFrom = new TimeOnly(18, 0)
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByClosesAtTo_WhenMaximumIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0));

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            opensAt: new TimeOnly(10, 0),
            closesAt: new TimeOnly(18, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                ClosesAtTo = new TimeOnly(12, 0)
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_FiltersByClosesAtRange_WhenMinimumAndMaximumAreProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0),
            closesAt: new TimeOnly(12, 0));

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            opensAt: new TimeOnly(10, 0),
            closesAt: new TimeOnly(17, 0));

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Wednesday,
            opensAt: new TimeOnly(10, 0),
            closesAt: new TimeOnly(21, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                ClosesAtFrom = new TimeOnly(16, 0),
                ClosesAtTo = new TimeOnly(18, 0)
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Find - Status
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_FiltersByIsClosed_WhenIsClosedIsTrue()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var closed = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            isClosed: true);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            isClosed: false);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                IsClosed = true
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == closed.Id);

        result.Data.Should().OnlyContain(hour => hour.IsClosed);
    }

    [Fact]
    public async Task Find_FiltersByIsClosed_WhenIsClosedIsFalse()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            isClosed: true);

        var open = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            isClosed: false);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                IsClosed = false
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == open.Id);

        result.Data.Should().OnlyContain(hour => !hour.IsClosed);
    }

    [Fact]
    public async Task Find_FiltersByIsActive_WhenIsActiveIsTrue()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var active = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            isActive: true);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            isActive: false);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                IsActive = true
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == active.Id);

        result.Data.Should().OnlyContain(hour => hour.IsActive);
    }

    [Fact]
    public async Task Find_FiltersByIsActive_WhenIsActiveIsFalse()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            isActive: true);

        var inactive = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday,
            isActive: false);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                IsActive = false
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == inactive.Id);

        result.Data.Should().OnlyContain(hour => !hour.IsActive);
    }

    // ---------------------------------------------------------------
    // Find - Relationships
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_IncludesRoom_WhenOperatingHourExists()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var operatingHour = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                Id = operatingHour.Id
            });

        result.Data.Should().ContainSingle();

        var returnedHour = result.Data.Single();

        returnedHour.Room.Should().NotBeNull();
        returnedHour.Room!.Id.Should().Be(room.Id);
    }

    [Fact]
    public async Task Find_ReturnsDetachedEntities_WhenOperatingHoursExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id
            });

        result.Data.Should().NotBeEmpty();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        foreach (var hour in result.Data)
        {
            context.Entry(hour).State.Should().Be(EntityState.Detached);
        }
    }

    // ---------------------------------------------------------------
    // Find - Pagination
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ReturnsPaginatedResults_WhenPageSizeIsProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var monday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday);

        var tuesday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday);

        var wednesday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Wednesday);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                Page = 1,
                PageSize = 2,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(2);

        result.Data.Select(hour => hour.Id)
            .Should()
            .ContainInOrder(monday.Id, tuesday.Id);

        result.Data.Should().NotContain(
            hour => hour.Id == wednesday.Id);
    }

    [Fact]
    public async Task Find_ReturnsSecondPage_WhenPageNumberIsTwo()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Tuesday);

        var wednesday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Wednesday);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                Page = 2,
                PageSize = 2,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == wednesday.Id);

        result.TotalCount.Should().Be(3);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Find_ReturnsDefaultPagination_WhenPageAndPageSizeAreNotProvided()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id
            });

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);

        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Find_ClampsPageNumberToOne_WhenPageIsZero()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var expected = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                Page = 0
            });

        result.PageNumber.Should().Be(1);

        result.Data.Should().ContainSingle(
            hour => hour.Id == expected.Id);
    }

    [Fact]
    public async Task Find_ClampsPageSizeToOneHundred_WhenPageSizeExceedsMaximum()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                Page = 1,
                PageSize = 1000
            });

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Find_ReturnsEmptyPage_WhenRequestedPageDoesNotExist()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room);

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                Page = 10,
                PageSize = 20
            });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
        result.PageNumber.Should().Be(10);
    }

    // ---------------------------------------------------------------
    // Find - Ordering
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_OrdersByDayOfWeekAndOpeningTimeAscending_WhenColumnOrderIsAsc()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var friday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Friday,
            opensAt: new TimeOnly(8, 0));

        var mondayAfternoon = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(13, 0));

        var mondayMorning = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Should().HaveCount(3);

        result.Data.Select(hour => hour.Id)
            .Should()
            .ContainInOrder(
                mondayMorning.Id,
                mondayAfternoon.Id,
                friday.Id);
    }

    [Fact]
    public async Task Find_OrdersByDayOfWeekAndOpeningTimeDescending_WhenColumnOrderIsDesc()
    {
        using var scope = CreateScope();

        var repository = GetRepository(scope);

        var room = await CreateRoomAsync(scope.ServiceProvider);

        var mondayMorning = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(8, 0));

        var mondayAfternoon = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Monday,
            opensAt: new TimeOnly(13, 0));

        var friday = await CreateOperatingHourAsync(
            scope.ServiceProvider,
            room,
            dayOfWeek: DayOfWeek.Friday,
            opensAt: new TimeOnly(8, 0));

        var result = await repository.Find(
            new FilterRoomOperatingHourDto
            {
                RoomId = room.Id,
                ColumnOrder = ColumnOrderEnum.DESC
            });

        result.Data.Should().HaveCount(3);

        result.Data.Select(hour => hour.Id)
            .Should()
            .ContainInOrder(
                friday.Id,
                mondayAfternoon.Id,
                mondayMorning.Id);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static IRoomOperatingHourRepository GetRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IRoomOperatingHourRepository>();
    }

    private static async Task<RoomOperatingHourModel> CreateOperatingHourAsync(
        IServiceProvider services,
        RoomModel room,
        DayOfWeek dayOfWeek = DayOfWeek.Monday,
        TimeOnly? opensAt = null,
        TimeOnly? closesAt = null,
        bool isClosed = false,
        bool isActive = true,
        string? notes = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        long version = 1)
    {
        ArgumentNullException.ThrowIfNull(room);

        var creationDate = createdAt ?? DateTimeOffset.UtcNow;

        var operatingHour = new RoomOperatingHourModel
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = room.Id,
            DayOfWeek = dayOfWeek,
            OpensAt = opensAt ?? new TimeOnly(9, 0),
            ClosesAt = closesAt ?? new TimeOnly(18, 0),
            IsClosed = isClosed,
            IsActive = isActive,
            Notes = notes,
            CreatedAt = creationDate,
            UpdatedAt = updatedAt,
            Version = version
        };

        var context = services.GetRequiredService<AppDbContext>();

        await context.Set<RoomOperatingHourModel>()
            .AddAsync(operatingHour);

        await context.SaveChangesAsync();

        // Restore explicit audit values after SaveChanges.
        // This keeps BaseFilter tests deterministic.
        await context.Set<RoomOperatingHourModel>()
            .Where(hour => hour.Id == operatingHour.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(hour => hour.CreatedAt, creationDate)
                .SetProperty(hour => hour.UpdatedAt, updatedAt)
                .SetProperty(hour => hour.Version, version));

        return operatingHour;
    }
}
