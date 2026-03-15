using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Admin;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AdminAccessResolverTests
    {
        [Fact]
        public void Resolve_PersistedRoleAdmin_ReturnsRoleAccess()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);
            dbContext.AdminRoleAssignments.Add(new AdminRoleAssignment { UserId = "user-1", AssignedUtc = System.DateTime.UtcNow });
            dbContext.SaveChanges();

            IAdminAccessResolver resolver = BuildResolver(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(new Claim("oid", "user-1"));

            AdminAccessResolution result = resolver.Resolve(principal);

            Assert.True(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.Role, result.Source);
            Assert.Equal(AdminAccessReason.GrantedRole, result.Reason);
        }

        [Fact]
        public void Resolve_LegacyRoleClaim_ReturnsRoleAccess()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);

            IAdminAccessResolver resolver = BuildResolver(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("oid", "user-1"));

            AdminAccessResolution result = resolver.Resolve(principal);

            Assert.True(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.Role, result.Source);
            Assert.Equal(AdminAccessReason.GrantedRole, result.Reason);
        }

        [Fact]
        public void Resolve_BootstrapEnabledAndOidMatch_ReturnsBootstrapAccess()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);

            IAdminAccessResolver resolver = BuildResolver(dbContext, new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                ["BOOTSTRAP_ADMIN_OID"] = "bootstrap-oid"
            });
            ClaimsPrincipal principal = BuildPrincipal(new Claim("oid", "bootstrap-oid"));

            AdminAccessResolution result = resolver.Resolve(principal);

            Assert.True(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.Bootstrap, result.Source);
            Assert.Equal(AdminAccessReason.GrantedBootstrap, result.Reason);
        }

        [Fact]
        public void Resolve_RoleAdmin_TakesPrecedenceOverBootstrapFallback()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);
            dbContext.AdminRoleAssignments.Add(new AdminRoleAssignment { UserId = "same-user", AssignedUtc = System.DateTime.UtcNow });
            dbContext.SaveChanges();

            IAdminAccessResolver resolver = BuildResolver(dbContext, new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                ["BOOTSTRAP_ADMIN_OID"] = "same-user"
            });
            ClaimsPrincipal principal = BuildPrincipal(new Claim("oid", "same-user"));

            AdminAccessResolution result = resolver.Resolve(principal);

            Assert.True(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.Role, result.Source);
            Assert.Equal(AdminAccessReason.GrantedRole, result.Reason);
        }

        [Fact]
        public void Resolve_NoAccess_ReturnsNone()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);

            IAdminAccessResolver resolver = BuildResolver(dbContext);
            ClaimsPrincipal principal = BuildPrincipal(new Claim("oid", "user-1"));

            AdminAccessResolution result = resolver.Resolve(principal);

            Assert.False(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.None, result.Source);
            Assert.Equal(AdminAccessReason.BootstrapDisabled, result.Reason);
        }

        [Fact]
        public void Resolve_BootstrapEnabledButOidMismatch_ReturnsNone()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);

            IAdminAccessResolver resolver = BuildResolver(dbContext, new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                ["BOOTSTRAP_ADMIN_OID"] = "bootstrap-oid"
            });
            ClaimsPrincipal principal = BuildPrincipal(new Claim("oid", "different-oid"));

            AdminAccessResolution result = resolver.Resolve(principal);

            Assert.False(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.None, result.Source);
            Assert.Equal(AdminAccessReason.BootstrapOidMismatch, result.Reason);
        }

        [Fact]
        public void ResolveForUserId_PersistedRoleAdmin_ReturnsRole()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);
            dbContext.AdminRoleAssignments.Add(new AdminRoleAssignment { UserId = "listed-user", AssignedUtc = System.DateTime.UtcNow });
            dbContext.SaveChanges();

            IAdminAccessResolver resolver = BuildResolver(dbContext);

            AdminAccessResolution result = resolver.ResolveForUserId("listed-user");

            Assert.True(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.Role, result.Source);
        }

        [Fact]
        public void ResolveForUserId_CurrentLegacyRoleAdminSessionMarksMatchingUserAsRoleAdmin()
        {
            using SqliteConnection connection = new("Data Source=:memory:");
            connection.Open();
            using AppDbContext dbContext = BuildDbContext(connection);

            IAdminAccessResolver resolver = BuildResolver(dbContext, new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                ["BOOTSTRAP_ADMIN_OID"] = "bootstrap-user"
            });
            ClaimsPrincipal currentPrincipal = BuildPrincipal(
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("oid", "role-admin-user"));

            AdminAccessResolution result = resolver.ResolveForUserId("role-admin-user", currentPrincipal);

            Assert.True(result.IsAdminAccess);
            Assert.Equal(AdminAccessSource.Role, result.Source);
            Assert.Equal(AdminAccessReason.GrantedRole, result.Reason);
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

        private static IAdminAccessResolver BuildResolver(AppDbContext dbContext, Dictionary<string, string?>? values = null)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
                .Build();

            return new AdminAccessResolver(configuration, dbContext);
        }

        private static ClaimsPrincipal BuildPrincipal(params Claim[] claims)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        }
    }
}
