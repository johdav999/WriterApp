using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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
        public async Task FindByEmailAsync_MatchesCaseInsensitively()
        {
            await using SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();
            await using AppDbContext dbContext = BuildDbContext(connection);

            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = "oid-123",
                DisplayName = "User@Example.com",
                HasOnboarded = true,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            UserLookupService service = new(dbContext);

            UserLookupResult? result = await service.FindByEmailAsync("user@example.com");

            Assert.NotNull(result);
            Assert.Single(result!.Matches);
            Assert.Equal("oid-123", result.Matches[0].UserId);
            Assert.Equal("User@Example.com", result.Matches[0].Email);
        }

        private static AppDbContext BuildDbContext(SqliteConnection connection)
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            AppDbContext context = new(options);
            context.Database.EnsureCreated();
            return context;
        }
    }
}
