using System;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using WriterApp.Data.Documents;

namespace WriterApp.Data
{
    public sealed class SqlServerMigrationsDbContext : AppDbContext
    {
        public SqlServerMigrationsDbContext(DbContextOptions<SqlServerMigrationsDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<DocumentOutlineNodeRecord>(entity =>
            {
                entity.HasOne(node => node.Parent)
                    .WithMany(node => node.Children)
                    .HasForeignKey(node => node.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(node => node.LinkedSection)
                    .WithMany()
                    .HasForeignKey(node => node.LinkedSectionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PageRecord>(entity =>
            {
                entity.HasOne(page => page.Document)
                    .WithMany()
                    .HasForeignKey(page => page.DocumentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // SQL Server safety: default all FKs to Restrict to avoid multiple cascade path errors.
            foreach (var fk in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }

    public sealed class SqlServerMigrationsDbContextFactory : IDesignTimeDbContextFactory<SqlServerMigrationsDbContext>
    {
        public SqlServerMigrationsDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = DesignTimeSqlServerFactorySupport.BuildConfiguration();
            (string sourceName, string connectionString) = DesignTimeSqlServerFactorySupport.ResolveConnectionString(configuration);
            (string finalConnectionString, bool forcedSqlPassword) = DesignTimeSqlServerFactorySupport.PrepareConnectionString(connectionString);
            Console.WriteLine($"[EF] Context=SqlServerMigrationsDbContext ConnectionSource={sourceName} ForcedSqlPassword={forcedSqlPassword}");

            DbContextOptions<SqlServerMigrationsDbContext> options = new DbContextOptionsBuilder<SqlServerMigrationsDbContext>()
                .UseSqlServer(finalConnectionString, sql => sql.EnableRetryOnFailure())
                .Options;

            return new SqlServerMigrationsDbContext(options);
        }
    }

    public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = DesignTimeSqlServerFactorySupport.BuildConfiguration();
            (string sourceName, string connectionString) = DesignTimeSqlServerFactorySupport.ResolveConnectionString(configuration);
            (string finalConnectionString, bool forcedSqlPassword) = DesignTimeSqlServerFactorySupport.PrepareConnectionString(connectionString);
            Console.WriteLine($"[EF] Context=AppDbContext ConnectionSource={sourceName} ForcedSqlPassword={forcedSqlPassword}");

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(finalConnectionString, sql => sql.EnableRetryOnFailure())
                .Options;

            return new AppDbContext(options);
        }
    }

    internal static class DesignTimeSqlServerFactorySupport
    {
        internal static IConfigurationRoot BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        internal static (string SourceName, string ConnectionString) ResolveConnectionString(IConfiguration configuration)
        {
            string? sqlServerConnection = configuration.GetConnectionString("SqlServer");
            if (!string.IsNullOrWhiteSpace(sqlServerConnection))
            {
                return ("SqlServer", sqlServerConnection);
            }

            string? defaultConnection = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(defaultConnection))
            {
                return ("DefaultConnection", defaultConnection);
            }

            throw new InvalidOperationException(
                "No SQL Server connection string found for design-time EF. Set ConnectionStrings:SqlServer or ConnectionStrings:DefaultConnection.");
        }

        internal static (string ConnectionString, bool ForcedSqlPassword) PrepareConnectionString(string connectionString)
        {
            SqlConnectionStringBuilder builder = new(connectionString);
            bool hasSqlPasswordCredentials =
                !string.IsNullOrWhiteSpace(builder.UserID) &&
                !string.IsNullOrWhiteSpace(builder.Password);

            if (!hasSqlPasswordCredentials)
            {
                return (builder.ConnectionString, false);
            }

            builder.Authentication = SqlAuthenticationMethod.SqlPassword;
            builder.Encrypt = true;
            builder.TrustServerCertificate = false;
            if (builder.ConnectTimeout < 60)
            {
                builder.ConnectTimeout = 60;
            }

            return (builder.ConnectionString, true);
        }
    }
}
