using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Admin;

namespace WriterApp.Application.Security
{
    public interface IDeletedUserIdentityService
    {
        Task<bool> IsDeletedAsync(string userId, CancellationToken ct = default);
        Task ThrowIfDeletedAsync(string userId, CancellationToken ct = default);
        Task UpsertDeletedIdentityAsync(
            string userId,
            string? email,
            string? displayName,
            string? deletedByAdminUserId,
            string? deletedByAdminEmail,
            string? reason,
            CancellationToken ct = default);
    }

    public sealed class DeletedUserIdentityException : InvalidOperationException
    {
        public DeletedUserIdentityException(string userId)
            : base("This Prosa account has been deleted. Sign out before registering again.")
        {
            UserId = userId;
        }

        public string UserId { get; }
    }

    public sealed class DeletedUserIdentityService : IDeletedUserIdentityService
    {
        private readonly AppDbContext _dbContext;

        public DeletedUserIdentityService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<bool> IsDeletedAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            string normalizedUserId = IdNorm.Norm(userId);
            return await _dbContext.DeletedUserIdentities
                .AsNoTracking()
                .AnyAsync(item => item.UserId == normalizedUserId, ct);
        }

        public async Task ThrowIfDeletedAsync(string userId, CancellationToken ct = default)
        {
            if (await IsDeletedAsync(userId, ct))
            {
                throw new DeletedUserIdentityException(IdNorm.Norm(userId));
            }
        }

        public async Task UpsertDeletedIdentityAsync(
            string userId,
            string? email,
            string? displayName,
            string? deletedByAdminUserId,
            string? deletedByAdminEmail,
            string? reason,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            string normalizedUserId = IdNorm.Norm(userId);
            DeletedUserIdentity? existing = await _dbContext.DeletedUserIdentities
                .FirstOrDefaultAsync(item => item.UserId == normalizedUserId, ct);

            if (existing is null)
            {
                existing = new DeletedUserIdentity
                {
                    UserId = normalizedUserId,
                    DeletedAtUtc = DateTime.UtcNow
                };
                _dbContext.DeletedUserIdentities.Add(existing);
            }

            existing.Email = email;
            existing.DisplayName = displayName;
            existing.DeletedByAdminUserId = deletedByAdminUserId;
            existing.DeletedByAdminEmail = deletedByAdminEmail;
            existing.Reason = reason;
            existing.DeletedAtUtc = DateTime.UtcNow;
        }
    }
}
