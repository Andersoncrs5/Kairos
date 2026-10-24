using Kairos.Api.Modules.Reliability.Inbox.DTOs.Request;
using Kairos.Api.Modules.Reliability.Inbox.DTOs.Response;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Reliability.Inbox.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class InboxMapper
{
    public static partial InboxModel ToModel(
        CreateInboxDto source,
        string id,
        DateTimeOffset createdAt,
        DateTimeOffset receivedAt);

    public static partial InboxDto ToDto(
        InboxModel source);

    public static partial void ApplyUpdate(
        UpdateInboxDto source,
        [MappingTarget] InboxModel target);
}