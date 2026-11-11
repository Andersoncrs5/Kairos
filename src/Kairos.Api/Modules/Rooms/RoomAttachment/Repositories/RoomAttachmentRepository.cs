using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;

using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;

public sealed class RoomAttachmentRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomAttachmentModel>(context),
        IRoomAttachmentRepository
{
    public Task<RoomAttachmentModel?> GetByStorageKeyAsync(
        string storageKey,
        CancellationToken ct = default)
    {
        return context.Set<RoomAttachmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attachment => attachment.StorageKey == storageKey,
                ct);
    }

    public Task<RoomAttachmentModel?> GetByIdWithDetailsAsync(
        string attachmentId,
        CancellationToken ct = default)
    {
        return context.Set<RoomAttachmentModel>()
            .AsNoTracking()
            .Include(attachment => attachment.Room)
            .Include(attachment => attachment.Creator)
            .SingleOrDefaultAsync(
                attachment => attachment.Id == attachmentId,
                ct);
    }

    public async Task<Page<RoomAttachmentModel>> Find(
        FilterRoomAttachmentDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IQueryable<RoomAttachmentModel> query =
            context.Set<RoomAttachmentModel>()
                .AsNoTracking()
                .Include(attachment => attachment.Room)
                .Include(attachment => attachment.Creator)
                .ApplyBaseFilter(dto);

        // ---------------------------------------------------------------
        // Identifiers
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.RoomId))
        {
            string roomId = dto.RoomId.Trim();

            query = query.Where(attachment =>
                attachment.RoomId == roomId);
        }

        if (!string.IsNullOrWhiteSpace(dto.CreatedBy))
        {
            string createdBy = dto.CreatedBy.Trim();

            query = query.Where(attachment =>
                attachment.CreatedBy == createdBy);
        }

        // ---------------------------------------------------------------
        // Creator
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.CreatorUserName))
        {
            string pattern = $"%{dto.CreatorUserName.Trim()}%";

            query = dto.IgnoreCaseCreatorUserName == true
                ? query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.UserName != null &&
                    EF.Functions.ILike(
                        attachment.Creator.UserName,
                        pattern))
                : query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.UserName != null &&
                    EF.Functions.Like(
                        attachment.Creator.UserName,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CreatorEmail))
        {
            string pattern = $"%{dto.CreatorEmail.Trim()}%";

            query = dto.IgnoreCaseCreatorEmail == true
                ? query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.Email != null &&
                    EF.Functions.ILike(
                        attachment.Creator.Email,
                        pattern))
                : query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.Email != null &&
                    EF.Functions.Like(
                        attachment.Creator.Email,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CreatorFullName))
        {
            string pattern = $"%{dto.CreatorFullName.Trim()}%";

            query = dto.IgnoreCaseCreatorFullName == true
                ? query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.FullName != null &&
                    EF.Functions.ILike(
                        attachment.Creator.FullName,
                        pattern))
                : query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.FullName != null &&
                    EF.Functions.Like(
                        attachment.Creator.FullName,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CreatorPhoneNumber))
        {
            string pattern = $"%{dto.CreatorPhoneNumber.Trim()}%";

            query = dto.IgnoreCaseCreatorPhoneNumber == true
                ? query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.PhoneNumber != null &&
                    EF.Functions.ILike(
                        attachment.Creator.PhoneNumber,
                        pattern))
                : query.Where(attachment =>
                    attachment.Creator != null &&
                    attachment.Creator.PhoneNumber != null &&
                    EF.Functions.Like(
                        attachment.Creator.PhoneNumber,
                        pattern));
        }

        // ---------------------------------------------------------------
        // File
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.OriginalFileName))
        {
            string pattern = $"%{dto.OriginalFileName.Trim()}%";

            query = dto.OriginalFileNameIgnoreCase == true
                ? query.Where(attachment =>
                    EF.Functions.ILike(
                        attachment.OriginalFileName,
                        pattern))
                : query.Where(attachment =>
                    EF.Functions.Like(
                        attachment.OriginalFileName,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.ContentType))
        {
            string contentType = dto.ContentType.Trim();

            query = query.Where(attachment =>
                attachment.ContentType == contentType);
        }

        // ---------------------------------------------------------------
        // Attachment type
        // ---------------------------------------------------------------

        if (dto.Type.HasValue)
        {
            RoomAttachmentTypeEnum type = dto.Type.Value;

            query = query.Where(attachment =>
                attachment.Type == type);
        }

        // ---------------------------------------------------------------
        // Alt text
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.AltText))
        {
            string pattern = $"%{dto.AltText.Trim()}%";

            query = dto.AltTextIgnoreCase == true
                ? query.Where(attachment =>
                    attachment.AltText != null &&
                    EF.Functions.ILike(
                        attachment.AltText,
                        pattern))
                : query.Where(attachment =>
                    attachment.AltText != null &&
                    EF.Functions.Like(
                        attachment.AltText,
                        pattern));
        }

        // ---------------------------------------------------------------
        // Cover
        // ---------------------------------------------------------------

        if (dto.IsCover.HasValue)
        {
            bool isCover = dto.IsCover.Value;

            query = query.Where(attachment =>
                attachment.IsCover == isCover);
        }

        // ---------------------------------------------------------------
        // Sort order
        // ---------------------------------------------------------------

        if (dto.MinSortOrder.HasValue)
        {
            int minSortOrder = dto.MinSortOrder.Value;

            query = query.Where(attachment =>
                attachment.SortOrder >= minSortOrder);
        }

        if (dto.MaxSortOrder.HasValue)
        {
            int maxSortOrder = dto.MaxSortOrder.Value;

            query = query.Where(attachment =>
                attachment.SortOrder <= maxSortOrder);
        }

        // ---------------------------------------------------------------
        // Creation date
        // ---------------------------------------------------------------

        if (dto.CreatedAtFrom.HasValue)
        {
            DateTimeOffset createdAtFrom = dto.CreatedAtFrom.Value;

            query = query.Where(attachment =>
                attachment.CreatedAt >= createdAtFrom);
        }

        if (dto.CreatedAtTo.HasValue)
        {
            DateTimeOffset createdAtTo = dto.CreatedAtTo.Value;

            query = query.Where(attachment =>
                attachment.CreatedAt <= createdAtTo);
        }

        // ---------------------------------------------------------------
        // File size
        // ---------------------------------------------------------------

        if (dto.MinSizeBytes.HasValue)
        {
            long minSizeBytes = dto.MinSizeBytes.Value;

            query = query.Where(attachment =>
                attachment.SizeBytes >= minSizeBytes);
        }

        if (dto.MaxSizeBytes.HasValue)
        {
            long maxSizeBytes = dto.MaxSizeBytes.Value;

            query = query.Where(attachment =>
                attachment.SizeBytes <= maxSizeBytes);
        }

        // ---------------------------------------------------------------
        // Total count
        // ---------------------------------------------------------------

        long totalCount = await query.LongCountAsync(ct);

        // ---------------------------------------------------------------
        // Pagination
        // ---------------------------------------------------------------

        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        // ---------------------------------------------------------------
        // Deterministic ordering
        // ---------------------------------------------------------------

        // SortOrder remains ascending to preserve the room gallery order.
        // ColumnOrder controls the creation date ordering.

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderBy(attachment => attachment.SortOrder)
                .ThenByDescending(attachment => attachment.CreatedAt)
                .ThenByDescending(attachment => attachment.Id)
            : query
                .OrderBy(attachment => attachment.SortOrder)
                .ThenBy(attachment => attachment.CreatedAt)
                .ThenBy(attachment => attachment.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<RoomAttachmentModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}