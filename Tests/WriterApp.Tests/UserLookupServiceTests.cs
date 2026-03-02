using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Users;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class UserLookupServiceTests
    {
        [Fact]
        public async Task SaveChanges_NormalizesGuidLikeStringIdsToLowercase()
        {
            await using AppDbContext db = BuildDbContext();
            string mixedCaseUserId = "A0B1C2D3-E4F5-4678-9ABC-DEF012345678";

            db.UserProfiles.Add(new UserProfile
            {
                UserId = mixedCaseUserId,
                DisplayName = "User",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                HasOnboarded = true
            });
            await db.SaveChangesAsync(CancellationToken.None);

            string stored = await db.UserProfiles.AsNoTracking()
                .Select(item => item.UserId)
                .FirstAsync(CancellationToken.None);

            Assert.Equal("a0b1c2d3-e4f5-4678-9abc-def012345678", stored);
        }

        [Fact]
        public async Task FindByUserIdAsync_MixedCaseInput_MatchesStoredLowercaseId()
        {
            await using AppDbContext db = BuildDbContext();
            string lowercaseUserId = "a0b1c2d3-e4f5-4678-9abc-def012345678";

            db.UserProfiles.Add(new UserProfile
            {
                UserId = lowercaseUserId,
                DisplayName = "User",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                HasOnboarded = true
            });
            await db.SaveChangesAsync(CancellationToken.None);

            UserLookupService service = new(db);
            var result = await service.FindByUserIdAsync("A0B1C2D3-E4F5-4678-9ABC-DEF012345678", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(lowercaseUserId, result!.UserId);
        }

        private static AppDbContext BuildDbContext()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Filename=:memory:")
                .Options;

            AppDbContext context = new(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            return context;
        }
    }
}
