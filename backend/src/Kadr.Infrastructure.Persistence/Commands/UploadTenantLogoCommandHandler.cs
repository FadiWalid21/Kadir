using Kadr.Platform.Application.Tenants;
using Kadr.Platform.Domain.Files;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Commands;

public sealed class UploadTenantLogoCommandHandler(
    KadrDbContext db,
    ITenantContext tenantContext,
    IFileStorage fileStorage,
    IImageProcessor imageProcessor) : ICommandHandler<UploadTenantLogoCommand, TenantLogoResult>
{
    public async Task<Result<TenantLogoResult>> HandleAsync(
        UploadTenantLogoCommand command,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<TenantLogoResult>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;

        ProcessedImage processed;
        try
        {
            processed = await imageProcessor
                .ProcessLogoAsync(command.Content, command.ContentType, command.FileName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidImageException ex)
        {
            return Result.Failure<TenantLogoResult>(ex.ErrorCode, ex.ErrorCode);
        }

        await using (processed.Content)
        {
            var stored = await fileStorage
                .SaveAsync(
                    tenantId,
                    FilePurpose.Logo,
                    processed.Content,
                    processed.SuggestedFileName,
                    processed.ContentType,
                    cancellationToken)
                .ConfigureAwait(false);

            var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (tenant is null)
            {
                await fileStorage.DeleteAsync(stored.StoragePath, cancellationToken).ConfigureAwait(false);
                return Result.Failure<TenantLogoResult>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
            }

            var previousLogoId = tenant.LogoFileId;
            string? previousPath = null;
            if (previousLogoId is not null)
            {
                var previous = await db.FileObjects
                    .SingleOrDefaultAsync(f => f.Id == previousLogoId.Value, cancellationToken)
                    .ConfigureAwait(false);
                if (previous is not null)
                {
                    previousPath = previous.StoragePath;
                    db.FileObjects.Remove(previous);
                }
            }

            var fileObject = FileObject.Create(
                stored.FileId,
                tenantId,
                stored.StoragePath,
                stored.FileName,
                stored.ContentType,
                stored.SizeBytes,
                processed.Sha256Hex,
                FilePurpose.Logo);

            db.FileObjects.Add(fileObject);
            tenant.SetLogo(stored.FileId);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (previousPath is not null)
            {
                await fileStorage.DeleteAsync(previousPath, cancellationToken).ConfigureAwait(false);
            }

            return Result.Success(new TenantLogoResult(stored.FileId, $"/api/v1/files/{stored.FileId:D}"));
        }
    }
}

public sealed class DeleteTenantLogoCommandHandler(
    KadrDbContext db,
    ITenantContext tenantContext,
    IFileStorage fileStorage) : ICommandHandler<DeleteTenantLogoCommand>
{
    public async Task<Result> HandleAsync(
        DeleteTenantLogoCommand command,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (tenant.LogoFileId is null)
        {
            return Result.Success();
        }

        var logoId = tenant.LogoFileId.Value;
        var file = await db.FileObjects.SingleOrDefaultAsync(f => f.Id == logoId, cancellationToken)
            .ConfigureAwait(false);

        tenant.ClearLogo();
        if (file is not null)
        {
            db.FileObjects.Remove(file);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (file is not null)
        {
            await fileStorage.DeleteAsync(file.StoragePath, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
