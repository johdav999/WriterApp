# Runbook: Azure SQL Cutover and Migrations

This runbook documents how WriterApp runs on Azure SQL with provider-specific EF Core migrations.

## Provider and migrations model

WriterApp keeps provider migration chains separate:

- SQLite migrations: `Migrations/`
- SQL Server migrations: `MigrationsSqlServer/`

Runtime provider selection:

- `DatabaseProvider=Sqlite` uses SQLite connection and SQLite migration chain.
- `DatabaseProvider=SqlServer` uses SQL Server connection and SQL Server migration chain.

For SQL Server migrations, always use context `WriterApp.Data.SqlServerMigrationsDbContext`.

## Required Azure App Service settings

Set these in App Service Configuration:

- `DatabaseProvider=SqlServer`
- `ConnectionStrings__SqlServer=Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`

Optional (only for local/dev SQLite deployments):

- `ConnectionStrings__DefaultConnection=Data Source=<path-to-sqlite-db>`

## Applying migrations

### Preferred: CI/CD migration step

Run SQL Server migrations during deployment (before swapping traffic):

```powershell
dotnet ef database update `
  --context WriterApp.Data.SqlServerMigrationsDbContext `
  --project BlazorApp.csproj `
  --startup-project BlazorApp.csproj
```

To create new SQL Server migrations:

```powershell
dotnet ef migrations add <MigrationName> `
  --context WriterApp.Data.SqlServerMigrationsDbContext `
  --project BlazorApp.csproj `
  --startup-project BlazorApp.csproj `
  --output-dir MigrationsSqlServer
```

### Fallback: admin migration endpoint

If available in the deployed build, use:

- `POST /api/admin/db/migrate`

Provider behavior of the endpoint is determined by runtime configuration:

- `DatabaseProvider=SqlServer` applies SQL Server migrations (`MigrationsSqlServer`).
- `DatabaseProvider=Sqlite` applies SQLite migrations (`Migrations`).

## Rollback

Application rollback (no data sync):

1. Set `DatabaseProvider=Sqlite`.
2. Redeploy the previous SQLite-based application version.

Notes:

- This rollback is application-level only.
- Data written to Azure SQL is not synchronized back into SQLite.

## SQL Server notes

- Collation: SQL Server string comparisons and ordering depend on DB/column collation; plan for collation-aware behavior in case-insensitive lookups.
- GUID casing: WriterApp normalizes string IDs via `IdNorm`; keep normalized GUID-string handling consistent across providers.
- Search: current search subsystem uses `SearchIndexEntries` + `LIKE` matching (no SQLite FTS5, no SQL Server full-text requirement in current implementation).
- Backups: rely on Azure SQL automated backups (point-in-time restore). For additional recovery workflows, use export options (for example, BACPAC export) per environment policy.
