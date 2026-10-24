using System.Text.Json.Nodes;
using FluentAssertions;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.IntegrationTests.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kairos.IntegrationTests.Module.Reliability.Repository;

[Collection("Integration Tests")]
public sealed class InboxRepositoryTest : BaseIntegrationTest
{
    public InboxRepositoryTest(KairosApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task TryMarkAsProcessingAsync_ReturnsTrueAndUpdatesInbox_WhenMessageIsPendingAndEligible()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);
        var now = DateTimeOffset.UtcNow;
        var workerId = $"worker-{Guid.NewGuid():N}";

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider,
            receivedAt: now.AddMinutes(-2),
            nextAttemptAt: now.AddMinutes(-1));

        // Act
        var result = await repository.TryMarkAsProcessingAsync(
            inbox.Id,
            workerId);

        // Assert
        result.Should().BeTrue();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var updatedInbox = await dbContext.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == inbox.Id);

        updatedInbox.Status.Should().Be(InboxStatusEnum.Processing);
        updatedInbox.LockedBy.Should().Be(workerId);
        updatedInbox.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TryMarkAsProcessingAsync_ReturnsFalse_WhenMessageDoesNotExist()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);
        var id = Guid.NewGuid().ToString();
        var workerId = $"worker-{Guid.NewGuid():N}";

        // Act
        var result = await repository.TryMarkAsProcessingAsync(
            id,
            workerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryMarkAsProcessingAsync_ReturnsFalse_WhenMessageIsNotPending()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);
        var workerId = $"worker-{Guid.NewGuid():N}";

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider,
            status: InboxStatusEnum.Processing,
            lockedBy: $"other-worker-{Guid.NewGuid():N}");

        // Act
        var result = await repository.TryMarkAsProcessingAsync(
            inbox.Id,
            workerId);

        // Assert
        result.Should().BeFalse();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistedInbox = await dbContext.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == inbox.Id);

        persistedInbox.Status.Should().Be(InboxStatusEnum.Processing);
        persistedInbox.LockedBy.Should().Be(inbox.LockedBy);
    }

    [Fact]
    public async Task TryMarkAsProcessingAsync_ReturnsFalse_WhenNextAttemptIsInTheFuture()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider,
            nextAttemptAt: DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var result = await repository.TryMarkAsProcessingAsync(
            inbox.Id,
            $"worker-{Guid.NewGuid():N}");

        // Assert
        result.Should().BeFalse();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistedInbox = await dbContext.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == inbox.Id);

        persistedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        persistedInbox.LockedBy.Should().BeNull();
    }

    [Fact]
    public async Task TryMarkAsProcessedAsync_ReturnsTrueAndUpdatesInbox_WhenWorkerOwnsMessage()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);
        var workerId = $"worker-{Guid.NewGuid():N}";

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider,
            status: InboxStatusEnum.Processing,
            lockedBy: workerId,
            nextAttemptAt: DateTimeOffset.UtcNow.AddMinutes(5),
            lastError: "Previous attempt failed.");

        // Act
        var result = await repository.TryMarkAsProcessedAsync(
            inbox.Id,
            workerId);

        // Assert
        result.Should().BeTrue();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var processedInbox = await dbContext.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == inbox.Id);

        processedInbox.Status.Should().Be(InboxStatusEnum.Processed);
        processedInbox.ProcessedAt.Should().NotBeNull();
        processedInbox.NextAttemptAt.Should().BeNull();
        processedInbox.LastError.Should().BeNull();
        processedInbox.LockedBy.Should().BeNull();
        processedInbox.UpdatedAt.Should().NotBeNull();

        // O histórico de tentativas deve ser preservado.
        processedInbox.RetryCount.Should().Be(inbox.RetryCount);
    }

    [Fact]
    public async Task TryMarkAsProcessedAsync_ReturnsFalse_WhenMessageDoesNotExist()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);
        var id = Guid.NewGuid().ToString();
        var workerId = $"worker-{Guid.NewGuid():N}";

        // Act
        var result = await repository.TryMarkAsProcessedAsync(
            id,
            workerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryMarkAsProcessedAsync_ReturnsFalse_WhenWorkerDoesNotOwnMessage()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);
        var ownerWorkerId = $"worker-{Guid.NewGuid():N}";
        var otherWorkerId = $"worker-{Guid.NewGuid():N}";

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider,
            status: InboxStatusEnum.Processing,
            lockedBy: ownerWorkerId);

        // Act
        var result = await repository.TryMarkAsProcessedAsync(
            inbox.Id,
            otherWorkerId);

        // Assert
        result.Should().BeFalse();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistedInbox = await dbContext.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == inbox.Id);

        persistedInbox.Status.Should().Be(InboxStatusEnum.Processing);
        persistedInbox.LockedBy.Should().Be(ownerWorkerId);
        persistedInbox.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task TryMarkAsProcessedAsync_ReturnsFalse_WhenMessageIsNotProcessing()
    {
        // Arrange
        using var scope = CreateScope();

        var repository = GetInboxRepository(scope);
        var workerId = $"worker-{Guid.NewGuid():N}";

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider,
            status: InboxStatusEnum.Pending,
            lockedBy: workerId);

        // Act
        var result = await repository.TryMarkAsProcessedAsync(
            inbox.Id,
            workerId);

        // Assert
        result.Should().BeFalse();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistedInbox = await dbContext.Set<InboxModel>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == inbox.Id);

        persistedInbox.Status.Should().Be(InboxStatusEnum.Pending);
        persistedInbox.ProcessedAt.Should().BeNull();
    }
    
    [Fact]
    public async Task GetByMessageIdAsync_ReturnsNull_WhenMessageDoesNotExist()
    {
        // Arrange
        using var scope = CreateScope();
        var repository = GetInboxRepository(scope);

        var messageId = $"missing-{Guid.NewGuid():N}";

        // Act
        var result = await repository.GetByMessageIdAsync(messageId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByMessageIdAsync_ReturnsInbox_WhenMessageExists()
    {
        // Arrange
        using var scope = CreateScope();
        var repository = GetInboxRepository(scope);

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider);

        // Act
        var result = await repository.GetByMessageIdAsync(
            inbox.MessageId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(inbox.Id);
        result.MessageId.Should().Be(inbox.MessageId);
        result.MessageType.Should().Be(inbox.MessageType);
        JsonNode.DeepEquals(
                JsonNode.Parse(result!.Payload),
                JsonNode.Parse(inbox.Payload))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ExistsByMessageIdAsync_ReturnsFalse_WhenMessageDoesNotExist()
    {
        // Arrange
        using var scope = CreateScope();
        var repository = GetInboxRepository(scope);

        var messageId = $"missing-{Guid.NewGuid():N}";

        // Act
        var exists = await repository.ExistsByMessageIdAsync(messageId);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByMessageIdAsync_ReturnsTrue_WhenMessageExists()
    {
        // Arrange
        using var scope = CreateScope();
        var repository = GetInboxRepository(scope);

        var inbox = await CreateInboxAsync(
            scope.ServiceProvider);

        // Act
        var exists = await repository.ExistsByMessageIdAsync(
            inbox.MessageId);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsEligibleMessages_InReceivedOrder_AndRespectsBatchSize()
    {
        // Arrange
        using var scope = CreateScope();

        await ClearInboxAsync(scope.ServiceProvider);

        var repository = GetInboxRepository(scope);

        var now = DateTimeOffset.UtcNow;

        var oldestEligible = await CreateInboxAsync(
            scope.ServiceProvider,
            receivedAt: now.AddMinutes(-3),
            nextAttemptAt: now.AddMinutes(-1));

        var secondEligible = await CreateInboxAsync(
            scope.ServiceProvider,
            receivedAt: now.AddMinutes(-2));

        // Uma retentativa futura não deve ser retornada.
        await CreateInboxAsync(
            scope.ServiceProvider,
            receivedAt: now.AddMinutes(-4),
            nextAttemptAt: now.AddHours(1));

        // Um estado diferente de Pending não deve ser retornado.
        var nonPendingStatus = Enum.GetValues<InboxStatusEnum>()
            .FirstOrDefault(status => status != InboxStatusEnum.Pending);

        if (nonPendingStatus != InboxStatusEnum.Pending)
        {
            await CreateInboxAsync(
                scope.ServiceProvider,
                receivedAt: now.AddMinutes(-5),
                status: nonPendingStatus);
        }

        // Act
        var result = await repository.GetPendingAsync(batchSize: 2);

        // Assert
        result.Should().HaveCount(2);

        result.Select(inbox => inbox.MessageId)
            .Should()
            .ContainInOrder(
                oldestEligible.MessageId,
                secondEligible.MessageId);

        result.Should().OnlyContain(
            inbox => inbox.Status == InboxStatusEnum.Pending);
    }
    
    [Fact]
    public async Task GetByCorrelationIdAsync_ReturnsOnlyMatchingMessages_InReceivedOrder()
    {
        // Arrange
        using var scope = CreateScope();
        var repository = GetInboxRepository(scope);

        var correlationId = $"correlation-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var olderMessage = await CreateInboxAsync(
            scope.ServiceProvider,
            correlationId: correlationId,
            receivedAt: now.AddMinutes(-2));

        var newerMessage = await CreateInboxAsync(
            scope.ServiceProvider,
            correlationId: correlationId,
            receivedAt: now.AddMinutes(-1));

        await CreateInboxAsync(
            scope.ServiceProvider,
            correlationId: $"other-{Guid.NewGuid():N}",
            receivedAt: now);

        // Act
        var result = await repository.GetByCorrelationIdAsync(
            correlationId);

        // Assert
        result.Should().HaveCount(2);

        result.Select(inbox => inbox.MessageId)
            .Should()
            .ContainInOrder(
                olderMessage.MessageId,
                newerMessage.MessageId);

        result.Should().OnlyContain(
            inbox => inbox.CorrelationId == correlationId);
    }

    private static IInboxRepository GetInboxRepository(
        IServiceScope scope)
    {
        return scope.ServiceProvider
            .GetRequiredService<IInboxRepository>();
    }

    private static async Task<InboxModel> CreateInboxAsync(
        IServiceProvider serviceProvider,
        string? messageId = null,
        string? correlationId = null,
        DateTimeOffset? receivedAt = null,
        DateTimeOffset? nextAttemptAt = null,
        InboxStatusEnum status = InboxStatusEnum.Pending,
        string? lockedBy = null,
        string? lastError = null)
    {
        var context = serviceProvider
            .GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        var inbox = new InboxModel
        {
            Id = Guid.NewGuid().ToString(),
            MessageId = messageId ?? $"message-{Guid.NewGuid():N}",
            MessageType = "test.event",
            Payload = """{"value":"integration-test"}""",
            Status = status,
            ReceivedAt = receivedAt ?? now,
            NextAttemptAt = nextAttemptAt,
            RetryCount = 0,
            CorrelationId = correlationId,
            LockedBy = lockedBy,
            LastError = lastError,
            CreatedAt = now
        };

        await context.Set<InboxModel>().AddAsync(inbox);
        await context.SaveChangesAsync();

        return inbox;
    }
    
    private static async Task ClearInboxAsync(
        IServiceProvider serviceProvider)
    {
        var context = serviceProvider
            .GetRequiredService<AppDbContext>();

        var existingMessages = await context
            .Set<InboxModel>()
            .ToListAsync();

        context.Set<InboxModel>().RemoveRange(existingMessages);

        await context.SaveChangesAsync();
    }
}