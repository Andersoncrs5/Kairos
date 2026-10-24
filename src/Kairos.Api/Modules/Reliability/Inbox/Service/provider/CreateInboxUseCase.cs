using System.Text.Json;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reliability.Inbox.DTOs.Request;
using Kairos.Api.Modules.Reliability.Inbox.Mapper;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Service.Interface;

namespace Kairos.Api.Modules.Reliability.Inbox.Service.provider;

public sealed class CreateInboxUseCase(
    IUnitOfWork uow) : ICreateInboxUseCase
{
    private const int MaxMessageIdLength = 256;
    private const int MaxMessageTypeLength = 256;
    private const int MaxSourceLength = 256;
    private const int MaxCorrelationIdLength = 256;

    public async Task<Result<InboxModel>> ExecuteAsync(
        CreateInboxDto dto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (dto is null)
        {
            return Result<InboxModel>.ValidationFailure(
                ["Inbox data is required."]);
        }

        if (string.IsNullOrWhiteSpace(dto.MessageId))
        {
            return Result<InboxModel>.ValidationFailure(
                ["Message ID is required."]);
        }

        var messageId = dto.MessageId.Trim();

        if (messageId.Length > MaxMessageIdLength)
        {
            return Result<InboxModel>.ValidationFailure(
                [$"Message ID cannot exceed {MaxMessageIdLength} characters."]);
        }

        if (string.IsNullOrWhiteSpace(dto.MessageType))
        {
            return Result<InboxModel>.ValidationFailure(
                ["Message type is required."]);
        }

        var messageType = dto.MessageType.Trim();

        if (messageType.Length > MaxMessageTypeLength)
        {
            return Result<InboxModel>.ValidationFailure(
                [$"Message type cannot exceed {MaxMessageTypeLength} characters."]);
        }

        if (string.IsNullOrWhiteSpace(dto.Payload))
        {
            return Result<InboxModel>.ValidationFailure(
                ["Payload is required."]);
        }

        if (!IsValidJson(dto.Payload))
        {
            return Result<InboxModel>.ValidationFailure(
                ["Payload must be valid JSON."]);
        }

        var source = NormalizeOptional(dto.Source);
        var headers = NormalizeOptional(dto.Headers);
        var correlationId = NormalizeOptional(dto.CorrelationId);

        if (source is { Length: > MaxSourceLength })
        {
            return Result<InboxModel>.ValidationFailure(
                [$"Source cannot exceed {MaxSourceLength} characters."]);
        }

        if (correlationId is { Length: > MaxCorrelationIdLength })
        {
            return Result<InboxModel>.ValidationFailure(
                [$"Correlation ID cannot exceed {MaxCorrelationIdLength} characters."]);
        }

        if (headers is not null && !IsValidJson(headers))
        {
            return Result<InboxModel>.ValidationFailure(
                ["Headers must be valid JSON."]);
        }

        var exists = await uow.InboxRepository.ExistsByMessageIdAsync(
            messageId,
            cancellationToken);

        if (exists)
        {
            return Result<InboxModel>.Conflict(
                "An inbox message with this MessageId already exists.");
        }

        var normalizedDto = new CreateInboxDto
        {
            MessageId = messageId,
            MessageType = messageType,
            Payload = dto.Payload.Trim(),
            Source = source,
            Headers = headers,
            CorrelationId = correlationId
        };

        var now = DateTimeOffset.UtcNow;

        var inbox = InboxMapper.ToModel(
            normalizedDto,
            Guid.NewGuid().ToString(),
            now,
            now);

        inbox.Status = InboxStatusEnum.Pending;
        inbox.RetryCount = 0;

        await uow.InboxRepository.AddAsync(inbox, cancellationToken);

        return Result<InboxModel>.Created(inbox);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}