using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;

namespace WriterApp.Application.Users
{
    public sealed class UserLookupService : IUserLookupService
    {
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly AppDbContext _dbContext;

        public UserLookupService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            string normalized = email.Trim().ToLowerInvariant();

            // UserProfile currently stores display name and user id; match email-like display names case-insensitively.
            List<UserLookupUser> matches = await _dbContext.UserProfiles
                .AsNoTracking()
                .Where(item =>
                    item.DisplayName != null && item.DisplayName.ToLower() == normalized
                    || item.UserId.ToLower() == normalized)
                .OrderBy(item => item.UserId)
                .Select(item => new UserLookupUser(
                    item.UserId,
                    item.DisplayName,
                    ResolveEmail(item.DisplayName, item.UserId)))
                .ToListAsync(ct);

            return matches.Count == 0
                ? null
                : new UserLookupResult(normalized, matches);
        }

        public async Task<UserLookupUser?> FindByUserIdAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            string normalized = userId.Trim();
            UserLookupUser? user = await _dbContext.UserProfiles
                .AsNoTracking()
                .Where(item => item.UserId == normalized)
                .Select(item => new UserLookupUser(
                    item.UserId,
                    item.DisplayName,
                    ResolveEmail(item.DisplayName, item.UserId)))
                .FirstOrDefaultAsync(ct);

            if (user is not null)
            {
                return user;
            }

            return EmailRegex.IsMatch(normalized)
                ? new UserLookupUser(normalized, normalized, normalized)
                : null;
        }

        private static string? ResolveEmail(string? displayName, string userId)
        {
            if (!string.IsNullOrWhiteSpace(displayName) && EmailRegex.IsMatch(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrWhiteSpace(userId) && EmailRegex.IsMatch(userId))
            {
                return userId;
            }

            return null;
        }
    }
}
