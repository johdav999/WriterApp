using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Data;

// No host startup, network listener, migrations, secret arguments or connection-string output.
try
{
    if (args.Length < 1 || args.Length % 2 != 1 || args[0] is not ("inspect" or "seal-restored-copy")) throw new ArgumentException();
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int i = 1; i < args.Length; i += 2)
        if (args[i] is not ("--provider" or "--connection-env" or "--document" or "--root" or "--expect-target" or "--writers-stopped" or "--operation") || !values.TryAdd(args[i], args[i + 1])) throw new ArgumentException();
    string Required(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException();
    string provider = Required("--provider"), variable = Required("--connection-env"), root = Path.GetFullPath(Required("--root"));
    if (!System.Text.RegularExpressions.Regex.IsMatch(variable, "^[A-Z][A-Z0-9_]{1,127}$") || !File.Exists(Path.Combine(root, "WriterApp.Collaboration/src/codec.mjs"))) throw new ArgumentException();
    string connection = Environment.GetEnvironmentVariable(variable) ?? throw new ArgumentException();
    bool sealing = args[0] == "seal-restored-copy";
    AppDbContext database;
    if (provider == "sqlite")
    {
        var sqlite = new SqliteConnectionStringBuilder(connection);
        if (sqlite.Mode == SqliteOpenMode.Memory || string.IsNullOrWhiteSpace(sqlite.DataSource) || sqlite.DataSource == ":memory:") throw new ArgumentException();
        sqlite.DataSource = Path.GetFullPath(sqlite.DataSource); if (!File.Exists(sqlite.DataSource)) throw new ArgumentException();
        sqlite.Mode = sealing ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly; sqlite.Pooling = false;
        database = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sqlite.ConnectionString).Options);
    }
    else if (provider == "sqlserver")
    {
        var sql = new SqlConnectionStringBuilder(connection);
        if (string.IsNullOrWhiteSpace(sql.InitialCatalog) || new[] { "master", "model", "msdb", "tempdb" }.Contains(sql.InitialCatalog, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException();
        database = new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(sql.ConnectionString).Options);
    }
    else throw new ArgumentException();
    await using var db = database;
    var address = db.Database.GetDbConnection();
    string target = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(provider + "\n" + address.DataSource + "\n" + address.Database))).ToLowerInvariant();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
    if ((await db.Database.GetPendingMigrationsAsync(timeout.Token)).Any() || db.Database.HasPendingModelChanges()) throw new InvalidOperationException("collaboration_migrations_required");
    var codec = new CollaborationCodec(Options.Create(new CollaborationConversionOptions()), new OfflineHost(root)); var operations = new CollaborationOperations(db, codec);
    if (sealing)
    {
        if (Required("--expect-target") != target || Required("--writers-stopped") != "true" || values.ContainsKey("--document") || !Guid.TryParse(Required("--operation"),out var operation) || operation == Guid.Empty) throw new ArgumentException();
        var result = await operations.FenceRestoredCopy(operation, true, new(), timeout.Token);
        Console.WriteLine(JsonSerializer.Serialize(new { target, result, requiresSharingReview = true })); return 0;
    }
    if (values.ContainsKey("--writers-stopped") || values.ContainsKey("--operation") || !Guid.TryParse(Required("--document"), out var document) || document == Guid.Empty) throw new ArgumentException();
    if (values.TryGetValue("--expect-target", out var expected) && expected != target) throw new ArgumentException();
    var report = await operations.Inspect(document, timeout.Token);
    var recoveryState = await db.CollaborationRecoveryStates.AsNoTracking().SingleOrDefaultAsync(timeout.Token);
    Console.WriteLine(JsonSerializer.Serialize(new { target, requiredEnvironment = recoveryState?.RequiredEnvironment, report })); return !report.IntegrityValid ? 2 : report.ConsistentReadReady ? 0 : 3;
}
catch (ArgumentException) { Console.Error.WriteLine("collaboration_operations_arguments_invalid"); return 4; }
catch (Exception) { Console.Error.WriteLine("collaboration_operations_failed_preserve_database_and_review_offline"); return 5; }

sealed class OfflineHost(string root) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Collaboration.Operations";
    public string EnvironmentName { get; set; } = "OfflineOperations";
    public string ContentRootPath { get; set; } = root;
    public string WebRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
