using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Tests;

/// <summary>
/// Old-schema upgrade fixtures must insert the old shape, without asking the current
/// EF model to SELECT/RETURN columns that did not exist in the historical schema.
/// Only used before upgrading disposable migration-test databases.
/// </summary>
internal static class LegacyMigrationSeed
{
    internal static async Task SaveAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlite() && !db.Database.IsSqlServer()) throw new InvalidOperationException("Unsupported migration fixture provider.");
        var entries = db.ChangeTracker.Entries().ToArray();
        if (entries.Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Historical fixtures may only insert initial test data.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var entry in entries.Where(e => e.State == EntityState.Added).OrderBy(e => e.Entity switch
            { ProjectRecord => 0, DocumentRecord => 1, SectionRecord => 2, PageRecord => 3, _ => 4 }))
        {
            if (entry.Entity is DocumentRecord document)
            {
                document.CreatedAtUnixSeconds = document.CreatedAt.ToUnixTimeSeconds();
                document.UpdatedAtUnixSeconds = document.UpdatedAt.ToUnixTimeSeconds();
            }
            string table = entry.Metadata.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entry.Metadata.GetSchema());
            var properties = entry.Properties.Where(p => entry.Entity is not DocumentRecord
                || p.Metadata.Name is not (nameof(DocumentRecord.CollaborationMode) or nameof(DocumentRecord.CollaborationMigrationState))).ToArray();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = $"INSERT INTO [{table}] ({string.Join(',', properties.Select(p => "[" + p.Metadata.GetColumnName(store) + "]"))}) VALUES ({string.Join(',', properties.Select((_, index) => "@p" + index))})";
            for (int i = 0; i < properties.Length; i++)
                command.Parameters.Add(properties[i].Metadata.GetRelationalTypeMapping().CreateParameter(command, "@p" + i, properties[i].CurrentValue, properties[i].Metadata.IsNullable));
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync(); db.ChangeTracker.Clear();
    }
}
