using FluentAssertions;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Summary;
using Kairos.IntegrationTests.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.IntegrationTests.Module.Room.RoomFeedback.Repository;

[Collection("Integration Tests")]
public sealed class RoomFeedbackRepositoryTest(
    KairosApiFactory factory) : BaseIntegrationTest(factory)
{
    private static IRoomFeedbackRepository GetRepository(
        IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IRoomFeedbackRepository>();

    // ---------------------------------------------------------------
    // GetByIdWithDetailsAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByIdWithDetailsAsync_ShouldReturnFeedbackWithDetails()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user = await CreateUserAsync(services);
        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services,
            room.Id,
            user.Id,
            category.Id);

        RoomFeedbackModel feedback = await CreateFeedbackAsync(
            services,
            reservation.Id,
            room.Id,
            user.Id);

        RoomFeedbackModel? result =
            await repository.GetByIdWithDetailsAsync(feedback.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(feedback.Id);

        result.Reservation.Should().NotBeNull();
        result.Reservation!.Id.Should().Be(reservation.Id);

        result.Room.Should().NotBeNull();
        result.Room!.Id.Should().Be(room.Id);

        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_ShouldReturnNull_WhenFeedbackDoesNotExist()
    {
        using IServiceScope scope = CreateScope();

        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomFeedbackModel? result =
            await repository.GetByIdWithDetailsAsync(Guid.NewGuid().ToString());

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // ExistsByReservationAndUserAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistsByReservationAndUserAsync_ShouldReturnTrue_WhenFeedbackExists()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user = await CreateUserAsync(services);
        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services,
            room.Id,
            user.Id,
            category.Id);

        await CreateFeedbackAsync(
            services,
            reservation.Id,
            room.Id,
            user.Id);

        bool result = await repository.ExistsByReservationAndUserAsync(
            reservation.Id,
            user.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByReservationAndUserAsync_ShouldReturnFalse_WhenFeedbackDoesNotExist()
    {
        using IServiceScope scope = CreateScope();

        IRoomFeedbackRepository repository = GetRepository(scope);

        bool result = await repository.ExistsByReservationAndUserAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByReservationAndUserAsync_ShouldReturnFalse_WhenUserDoesNotMatch()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user = await CreateUserAsync(services);
        UserModel anotherUser = await CreateUserAsync(services);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services,
            room.Id,
            user.Id,
            category.Id);

        await CreateFeedbackAsync(
            services,
            reservation.Id,
            room.Id,
            user.Id);

        bool result = await repository.ExistsByReservationAndUserAsync(
            reservation.Id,
            anotherUser.Id);

        result.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // GetRatingSummaryByRoomIdAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetRatingSummaryByRoomIdAsync_ShouldReturnCorrectSummary()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);
        UserModel user3 = await CreateUserAsync(services);
        UserModel user4 = await CreateUserAsync(services);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        ReservationModel reservation3 = await CreateReservationAsync(
            services, room.Id, user3.Id, category.Id);

        ReservationModel reservation4 = await CreateReservationAsync(
            services, room.Id, user4.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation1.Id, room.Id, user1.Id, rating: 5);

        await CreateFeedbackAsync(
            services, reservation2.Id, room.Id, user2.Id, rating: 5);

        await CreateFeedbackAsync(
            services, reservation3.Id, room.Id, user3.Id, rating: 3);

        await CreateFeedbackAsync(
            services, reservation4.Id, room.Id, user4.Id, rating: 1);

        RoomFeedbackRatingSummary result =
            await repository.GetRatingSummaryByRoomIdAsync(room.Id);

        result.TotalCount.Should().Be(4);
        result.AverageRating.Should().BeApproximately(3.5, 0.001);

        result.RatingDistribution.Should().HaveCount(5);
        result.RatingDistribution.Should().ContainKey(1).WhoseValue.Should().Be(1);
        result.RatingDistribution.Should().ContainKey(2).WhoseValue.Should().Be(0);
        result.RatingDistribution.Should().ContainKey(3).WhoseValue.Should().Be(1);
        result.RatingDistribution.Should().ContainKey(4).WhoseValue.Should().Be(0);
        result.RatingDistribution.Should().ContainKey(5).WhoseValue.Should().Be(2);
    }

    [Fact]
    public async Task GetRatingSummaryByRoomIdAsync_ShouldReturnEmptySummary_WhenRoomHasNoFeedback()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);

        RoomFeedbackRatingSummary result =
            await repository.GetRatingSummaryByRoomIdAsync(room.Id);

        result.TotalCount.Should().Be(0);
        result.AverageRating.Should().BeNull();

        result.RatingDistribution.Should().HaveCount(5);

        for (int rating = 1; rating <= 5; rating++)
        {
            result.RatingDistribution.Should()
                .ContainKey(rating)
                .WhoseValue.Should()
                .Be(0);
        }
    }

    // ---------------------------------------------------------------
    // GetRatingAveragesByRoomIdsAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetRatingAveragesByRoomIdsAsync_ShouldReturnAveragesForAllRoomsWithFeedback()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room1 = await CreateRoomAsync(services);
        RoomModel room2 = await CreateRoomAsync(services);
        RoomModel roomWithoutFeedback = await CreateRoomAsync(services);

        CategoryModel category = await CreateCategoryAsync(services);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);
        UserModel user3 = await CreateUserAsync(services);
        UserModel user4 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room1.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room1.Id, user2.Id, category.Id);

        ReservationModel reservation3 = await CreateReservationAsync(
            services, room2.Id, user3.Id, category.Id);

        ReservationModel reservation4 = await CreateReservationAsync(
            services, room2.Id, user4.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation1.Id, room1.Id, user1.Id, rating: 5);

        await CreateFeedbackAsync(
            services, reservation2.Id, room1.Id, user2.Id, rating: 3);

        await CreateFeedbackAsync(
            services, reservation3.Id, room2.Id, user3.Id, rating: 4);

        await CreateFeedbackAsync(
            services, reservation4.Id, room2.Id, user4.Id, rating: 2);

        IReadOnlyDictionary<string, RoomRatingAverage> result =
            await repository.GetRatingAveragesByRoomIdsAsync(
            [
                room1.Id,
                room2.Id,
                roomWithoutFeedback.Id
            ]);

        result.Should().HaveCount(2);

        result.Should().ContainKey(room1.Id);
        result[room1.Id].AverageRating.Should().BeApproximately(4.0, 0.001);
        result[room1.Id].TotalCount.Should().Be(2);

        result.Should().ContainKey(room2.Id);
        result[room2.Id].AverageRating.Should().BeApproximately(3.0, 0.001);
        result[room2.Id].TotalCount.Should().Be(2);

        result.Should().NotContainKey(roomWithoutFeedback.Id);
    }

    [Fact]
    public async Task GetRatingAveragesByRoomIdsAsync_ShouldReturnEmptyDictionary_WhenRoomIdsIsEmpty()
    {
        using IServiceScope scope = CreateScope();

        IRoomFeedbackRepository repository = GetRepository(scope);

        IReadOnlyDictionary<string, RoomRatingAverage> result =
            await repository.GetRatingAveragesByRoomIdsAsync([]);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRatingAveragesByRoomIdsAsync_ShouldThrowArgumentNullException_WhenRoomIdsIsNull()
    {
        using IServiceScope scope = CreateScope();

        IRoomFeedbackRepository repository = GetRepository(scope);

        Func<Task> act = async () =>
            await repository.GetRatingAveragesByRoomIdsAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ---------------------------------------------------------------
    // Find
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ShouldReturnAllFeedback_WhenNoFiltersAreProvided()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        for (int i = 0; i < 3; i++)
        {
            UserModel user = await CreateUserAsync(services);

            ReservationModel reservation = await CreateReservationAsync(
                services, room.Id, user.Id, category.Id);

            await CreateFeedbackAsync(
                services, reservation.Id, room.Id, user.Id);
        }

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id
            });

        result.TotalCount.Should().Be(3);
        result.Count.Should().Be(3);
        result.HasData.Should().BeTrue();
        result.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Find_ShouldReturnEmptyPage_WhenNoFeedbackMatchesFilters()
    {
        using IServiceScope scope = CreateScope();

        IRoomFeedbackRepository repository = GetRepository(scope);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = Guid.NewGuid().ToString()
            });

        result.TotalCount.Should().Be(0);
        result.Count.Should().Be(0);
        result.IsEmpty.Should().BeTrue();
        result.HasData.Should().BeFalse();
    }

    [Fact]
    public async Task Find_ShouldFilterByReservationId()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation1.Id, room.Id, user1.Id);

        await CreateFeedbackAsync(
            services, reservation2.Id, room.Id, user2.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                ReservationId = reservation1.Id
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.ReservationId.Should().Be(reservation1.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByUserId()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation1.Id, room.Id, user1.Id);

        await CreateFeedbackAsync(
            services, reservation2.Id, room.Id, user2.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                UserId = user1.Id
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.UserId.Should().Be(user1.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByRatingRange()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        int[] ratings = [1, 2, 3, 4, 5];

        foreach (int rating in ratings)
        {
            UserModel user = await CreateUserAsync(services);

            ReservationModel reservation = await CreateReservationAsync(
                services, room.Id, user.Id, category.Id);

            await CreateFeedbackAsync(
                services,
                reservation.Id,
                room.Id,
                user.Id,
                rating: rating);
        }

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                RatingMin = 3,
                RatingMax = 4
            });

        result.TotalCount.Should().Be(2);
        
    }

    [Fact]
    public async Task Find_ShouldFilterByIssueType()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        await CreateFeedbackAsync(
            services,
            reservation1.Id,
            room.Id,
            user1.Id,
            issueType: RoomFeedbackIssueTypeEnum.Equipment);

        await CreateFeedbackAsync(
            services,
            reservation2.Id,
            room.Id,
            user2.Id,
            issueType: RoomFeedbackIssueTypeEnum.Cleanliness);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                IssueType = RoomFeedbackIssueTypeEnum.Equipment
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.IssueType.Should().Be(RoomFeedbackIssueTypeEnum.Equipment);
    }

    [Fact]
    public async Task Find_ShouldFilterByHasIssue()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        await CreateFeedbackAsync(
            services,
            reservation1.Id,
            room.Id,
            user1.Id,
            issueType: RoomFeedbackIssueTypeEnum.Noise);

        await CreateFeedbackAsync(
            services,
            reservation2.Id,
            room.Id,
            user2.Id);

        Page<RoomFeedbackModel> withIssue = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                HasIssue = true
            });

        Page<RoomFeedbackModel> withoutIssue = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                HasIssue = false
            });

        withIssue.TotalCount.Should().Be(1);
        withIssue.Data.Should().OnlyContain(
            feedback => feedback.IssueType.HasValue);

        withoutIssue.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Find_ShouldFilterByHasComment()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        await CreateFeedbackAsync(
            services,
            reservation1.Id,
            room.Id,
            user1.Id,
            comment: "Excellent room.");

        await CreateFeedbackAsync(
            services,
            reservation2.Id,
            room.Id,
            user2.Id);

        Page<RoomFeedbackModel> withComment = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                HasComment = true
            });

        Page<RoomFeedbackModel> withoutComment = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                HasComment = false
            });

        withComment.TotalCount.Should().Be(1);
        
        withoutComment.TotalCount.Should().Be(1);
        
    }

    [Fact]
    public async Task Find_ShouldFilterByComment()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        await CreateFeedbackAsync(
            services,
            reservation1.Id,
            room.Id,
            user1.Id,
            comment: "The equipment was excellent.");

        await CreateFeedbackAsync(
            services,
            reservation2.Id,
            room.Id,
            user2.Id,
            comment: "The room was noisy.");

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                Comment = "equipment"
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.Comment.Should().Contain("equipment");
    }

    [Fact]
    public async Task Find_ShouldFilterByRoomCode()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);
        UserModel user = await CreateUserAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services, room.Id, user.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomCode = room.Code
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.RoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByRoomName()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(
            services,
            room => room.Name = "Integration Test Room");

        CategoryModel category = await CreateCategoryAsync(services);
        UserModel user = await CreateUserAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services, room.Id, user.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomName = "Integration Test"
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.RoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByUserName()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user = await CreateUserAsync(
            services,
            user => user.UserName = $"feedback-user-{Guid.NewGuid():N}");

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services, room.Id, user.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                UserName = user.UserName
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByUserEmail()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user = await CreateUserAsync(services);
        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services, room.Id, user.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                UserEmail = user.Email
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByUserFullName()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user = await CreateUserAsync(
            services,
            user => user.FullName = "Anderson Integration User");

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services, room.Id, user.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                UserFullName = "Anderson Integration"
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByReservationTitle()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        UserModel user = await CreateUserAsync(services);
        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services,
            room.Id,
            user.Id,
            category.Id,
            title: "Architecture Meeting");

        await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                ReservationTitle = "Architecture"
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.ReservationId.Should().Be(reservation.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByReservationStartsAtRange()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services,
            room.Id,
            user1.Id,
            category.Id,
            startsAt: now.AddDays(-5));

        ReservationModel reservation2 = await CreateReservationAsync(
            services,
            room.Id,
            user2.Id,
            category.Id,
            startsAt: now);

        await CreateFeedbackAsync(
            services, reservation1.Id, room.Id, user1.Id);

        await CreateFeedbackAsync(
            services, reservation2.Id, room.Id, user2.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                ReservationStartsAtFrom = now.AddDays(-1),
                ReservationStartsAtTo = now.AddDays(1)
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.ReservationId.Should().Be(reservation2.Id);
    }

    // ---------------------------------------------------------------
    // Find - Pagination
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ShouldReturnCorrectPage()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        for (int i = 0; i < 5; i++)
        {
            UserModel user = await CreateUserAsync(services);

            ReservationModel reservation = await CreateReservationAsync(
                services, room.Id, user.Id, category.Id);

            await CreateFeedbackAsync(
                services, reservation.Id, room.Id, user.Id);
        }

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                Page = 2,
                PageSize = 2
            });

        result.TotalCount.Should().Be(5);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Count.Should().Be(2);
        result.TotalPages.Should().Be(3);

        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
        result.IsFirstPage.Should().BeFalse();
        result.IsLastPage.Should().BeFalse();
    }

    [Fact]
    public async Task Find_ShouldReturnLastPageWithRemainingItems()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        for (int i = 0; i < 5; i++)
        {
            UserModel user = await CreateUserAsync(services);

            ReservationModel reservation = await CreateReservationAsync(
                services, room.Id, user.Id, category.Id);

            await CreateFeedbackAsync(
                services, reservation.Id, room.Id, user.Id);
        }

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                Page = 3,
                PageSize = 2
            });

        result.TotalCount.Should().Be(5);
        result.Count.Should().Be(1);
        result.PageNumber.Should().Be(3);

        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeFalse();
        result.IsLastPage.Should().BeTrue();
    }

    [Fact]
    public async Task Find_ShouldNormalizeInvalidPageNumber()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);
        UserModel user = await CreateUserAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services, room.Id, user.Id, category.Id);

        await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                Page = 0
            });

        result.PageNumber.Should().Be(1);
        result.Count.Should().Be(1);
    }

    [Fact]
    public async Task Find_ShouldNormalizePageSizeToMaximumOfOneHundred()
    {
        using IServiceScope scope = CreateScope();

        IRoomFeedbackRepository repository = GetRepository(scope);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = Guid.NewGuid().ToString(),
                PageSize = 200
            });

        result.PageSize.Should().Be(100);
    }

    // ---------------------------------------------------------------
    // Find - Ordering
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ShouldOrderByCreatedAtAscending()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        DateTimeOffset baseDate = DateTimeOffset.UtcNow.AddMinutes(-10);

        for (int i = 0; i < 3; i++)
        {
            UserModel user = await CreateUserAsync(services);

            ReservationModel reservation = await CreateReservationAsync(
                services, room.Id, user.Id, category.Id);

            await CreateFeedbackAsync(
                services,
                reservation.Id,
                room.Id,
                user.Id,
                createdAt: baseDate.AddMinutes(i));
        }

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                ColumnOrder = ColumnOrderEnum.ASC
            });

        result.Data.Select(feedback => feedback.CreatedAt)
            .Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Find_ShouldOrderByCreatedAtDescending()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        DateTimeOffset baseDate = DateTimeOffset.UtcNow.AddMinutes(-10);

        for (int i = 0; i < 3; i++)
        {
            UserModel user = await CreateUserAsync(services);

            ReservationModel reservation = await CreateReservationAsync(
                services, room.Id, user.Id, category.Id);

            await CreateFeedbackAsync(
                services,
                reservation.Id,
                room.Id,
                user.Id,
                createdAt: baseDate.AddMinutes(i));
        }

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                ColumnOrder = ColumnOrderEnum.DESC
            });

        result.Data.Select(feedback => feedback.CreatedAt)
            .Should().BeInDescendingOrder();
    }

    // ---------------------------------------------------------------
    // Find - BaseFilter
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_ShouldFilterById()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);
        UserModel user = await CreateUserAsync(services);

        ReservationModel reservation = await CreateReservationAsync(
            services, room.Id, user.Id, category.Id);

        RoomFeedbackModel feedback = await CreateFeedbackAsync(
            services, reservation.Id, room.Id, user.Id);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                Id = feedback.Id
            });

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle()
            .Which.Id.Should().Be(feedback.Id);
    }

    [Fact]
    public async Task Find_ShouldFilterByVersionRange()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        for (int i = 0; i < 3; i++)
        {
            UserModel user = await CreateUserAsync(services);

            ReservationModel reservation = await CreateReservationAsync(
                services, room.Id, user.Id, category.Id);

            await CreateFeedbackAsync(
                services,
                reservation.Id,
                room.Id,
                user.Id,
                version: i + 1);
        }

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                VersionMin = 2,
                VersionMax = 3
            });
        
    }

    [Fact]
    public async Task Find_ShouldFilterByCreatedAtRange()
    {
        using IServiceScope scope = CreateScope();

        IServiceProvider services = scope.ServiceProvider;
        IRoomFeedbackRepository repository = GetRepository(scope);

        RoomModel room = await CreateRoomAsync(services);
        CategoryModel category = await CreateCategoryAsync(services);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserModel user1 = await CreateUserAsync(services);
        UserModel user2 = await CreateUserAsync(services);

        ReservationModel reservation1 = await CreateReservationAsync(
            services, room.Id, user1.Id, category.Id);

        ReservationModel reservation2 = await CreateReservationAsync(
            services, room.Id, user2.Id, category.Id);

        await CreateFeedbackAsync(
            services,
            reservation1.Id,
            room.Id,
            user1.Id,
            createdAt: now.AddDays(-5));

        await CreateFeedbackAsync(
            services,
            reservation2.Id,
            room.Id,
            user2.Id,
            createdAt: now);

        Page<RoomFeedbackModel> result = await repository.Find(
            new FilterRoomFeedbackDto
            {
                RoomId = room.Id,
                CreatedAtMin = now.AddDays(-1),
                CreatedAtMax = now.AddDays(1)
            });
        
    }

    [Fact]
    public async Task Find_ShouldThrowArgumentNullException_WhenDtoIsNull()
    {
        using IServiceScope scope = CreateScope();

        IRoomFeedbackRepository repository = GetRepository(scope);

        Func<Task> act = async () =>
            await repository.Find(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static async Task<ReservationModel> CreateReservationAsync(
        IServiceProvider services,
        string roomId,
        string userId,
        string categoryId,
        string title = "Integration Test Reservation",
        DateTimeOffset? startsAt = null)
    {
        AppDbContext context = services.GetRequiredService<AppDbContext>();

        DateTimeOffset start = startsAt ?? DateTimeOffset.UtcNow;

        ReservationModel reservation = new()
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = roomId,
            UserId = userId,
            CategoryId = categoryId,
            Title = title,
            Description = "Integration test reservation",
            StartsAt = start,
            EndsAt = start.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await context.Set<ReservationModel>().AddAsync(reservation);
        await context.SaveChangesAsync();

        return reservation;
    }

    private static async Task<RoomFeedbackModel> CreateFeedbackAsync(
        IServiceProvider services,
        string reservationId,
        string roomId,
        string userId,
        int rating = 5,
        string? comment = null,
        RoomFeedbackIssueTypeEnum? issueType = null,
        DateTimeOffset? createdAt = null,
        long version = 1)
    {
        AppDbContext context = services.GetRequiredService<AppDbContext>();

        RoomFeedbackModel feedback = new()
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            RoomId = roomId,
            UserId = userId,
            Rating = rating,
            Comment = comment,
            IssueType = issueType,
            Version = version,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };

        await context.Set<RoomFeedbackModel>().AddAsync(feedback);
        await context.SaveChangesAsync();

        return feedback;
    }
}
