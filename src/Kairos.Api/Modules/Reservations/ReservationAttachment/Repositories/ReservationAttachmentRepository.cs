using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;

using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;

public sealed class ReservationAttachmentRepository(AppDbContext context)
    : BaseRepositoryImpl<ReservationAttachmentModel>(context),
        IReservationAttachmentRepository
{
    public Task<ReservationAttachmentModel?> GetByStorageKeyAsync(
        string storageKey,
        CancellationToken ct = default)
    {
        return context.ReservationAttachments.AsNoTracking()
            .SingleOrDefaultAsync(attachment => attachment.StorageKey == storageKey, ct);
    }
    public Task<ReservationAttachmentModel?> GetByIdWithDetailsAsync(
        string attachmentId,
        CancellationToken ct = default)
    {
        return context.ReservationAttachments
            .AsNoTracking()
            .Include(attachment => attachment.Reservation)
            .Include(attachment => attachment.Uploader)
            .SingleOrDefaultAsync(attachment => attachment.Id == attachmentId, ct);
    }

    public async Task<Page<ReservationAttachmentModel>> Find(
        FilterReservationAttachmentDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IQueryable<ReservationAttachmentModel> query =
            context.ReservationAttachments
                .AsNoTracking()
                .Include(attachment => attachment.Reservation)
                .Include(attachment => attachment.Uploader)
                .ApplyBaseFilter(dto);

        // ---------------------------------------------------------------
        // Identifiers
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.ReservationId))
        {
            string reservationId = dto.ReservationId.Trim();

            query = query.Where(attachment =>
                attachment.ReservationId == reservationId);
        }

        if (!string.IsNullOrWhiteSpace(dto.UploadedBy))
        {
            string uploadedBy = dto.UploadedBy.Trim();

            query = query.Where(attachment =>
                attachment.UploadedBy == uploadedBy);
        }

        // ---------------------------------------------------------------
        // Reservation
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.ReservationTitle))
        {
            string pattern = $"%{dto.ReservationTitle.Trim()}%";

            query = dto.ReservationTitleIgnoreCase == true
                ? query.Where(attachment =>
                    attachment.Reservation != null &&
                    EF.Functions.ILike(
                        attachment.Reservation.Title,
                        pattern))
                : query.Where(attachment =>
                    attachment.Reservation != null &&
                    EF.Functions.Like(
                        attachment.Reservation.Title,
                        pattern));
        }

        // ---------------------------------------------------------------
        // Uploader
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.UploadedUserName))
        {
            string pattern = $"%{dto.UploadedUserName.Trim()}%";

            query = dto.IgnoreCaseUploadedUserName == true
                ? query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.UserName != null &&
                    EF.Functions.ILike(
                        attachment.Uploader.UserName,
                        pattern))
                : query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.UserName != null &&
                    EF.Functions.Like(
                        attachment.Uploader.UserName,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.UploadedEmail))
        {
            string pattern = $"%{dto.UploadedEmail.Trim()}%";

            query = dto.IgnoreCaseUploadedEmail == true
                ? query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.Email != null &&
                    EF.Functions.ILike(
                        attachment.Uploader.Email,
                        pattern))
                : query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.Email != null &&
                    EF.Functions.Like(
                        attachment.Uploader.Email,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.UploadedFullName))
        {
            string pattern = $"%{dto.UploadedFullName.Trim()}%";

            query = dto.IgnoreCaseUploadedFullName == true
                ? query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.FullName != null &&
                    EF.Functions.ILike(
                        attachment.Uploader.FullName,
                        pattern))
                : query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.FullName != null &&
                    EF.Functions.Like(
                        attachment.Uploader.FullName,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.UploadedPhoneNumber))
        {
            string pattern = $"%{dto.UploadedPhoneNumber.Trim()}%";

            query = dto.IgnoreCaseUploadedPhoneNumber == true
                ? query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.PhoneNumber != null &&
                    EF.Functions.ILike(
                        attachment.Uploader.PhoneNumber,
                        pattern))
                : query.Where(attachment =>
                    attachment.Uploader != null &&
                    attachment.Uploader.PhoneNumber != null &&
                    EF.Functions.Like(
                        attachment.Uploader.PhoneNumber,
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

        if (!string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            string pattern = $"%{dto.DisplayName.Trim()}%";

            query = dto.DisplayNameIgnoreCase == true
                ? query.Where(attachment =>
                    EF.Functions.ILike(
                        attachment.DisplayName,
                        pattern))
                : query.Where(attachment =>
                    EF.Functions.Like(
                        attachment.DisplayName,
                        pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.ContentType))
        {
            string contentType = dto.ContentType.Trim();

            query = query.Where(attachment =>
                attachment.ContentType == contentType);
        }

        // ---------------------------------------------------------------
        // Visibility and scan status
        // ---------------------------------------------------------------

        if (dto.Visibility.HasValue)
        {
            ReservationAttachmentVisibilityEnum visibility =
                dto.Visibility.Value;

            query = query.Where(attachment =>
                attachment.Visibility == visibility);
        }

        if (dto.ScanStatus.HasValue)
        {
            FileScanStatusEnum scanStatus = dto.ScanStatus.Value;

            query = query.Where(attachment =>
                attachment.ScanStatus == scanStatus);
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
        // Description
        // ---------------------------------------------------------------

        if (dto.HasDescription.HasValue)
        {
            query = dto.HasDescription.Value
                ? query.Where(attachment =>
                    attachment.Description != null &&
                    attachment.Description != "")
                : query.Where(attachment =>
                    attachment.Description == null ||
                    attachment.Description == "");
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

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(attachment => attachment.CreatedAt)
                .ThenByDescending(attachment => attachment.Id)
            : query
                .OrderBy(attachment => attachment.CreatedAt)
                .ThenBy(attachment => attachment.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<ReservationAttachmentModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}