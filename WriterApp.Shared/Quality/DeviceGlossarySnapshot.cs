using System.Security.Cryptography;
using System.Text.Json;

namespace WriterApp.Shared.Quality;

/// <summary>Versioned, read-only glossary context. Order matches server quality analysis.</summary>
public sealed record DeviceGlossarySnapshot(int ContractVersion, Guid DocumentId, IReadOnlyList<string> Terms, string Revision)
{
    public const int MaximumTerms = 1000;
    public const int MaximumBytes = 1_000_000;
    public static DeviceGlossarySnapshot Create(Guid documentId, IReadOnlyList<string> terms)
    {
        ValidateTerms(documentId, terms);
        var snapshot = new DeviceGlossarySnapshot(1, documentId, terms.ToArray(), Hash(documentId, terms));
        snapshot.Validate(documentId);
        return snapshot;
    }
    public void Validate(Guid expectedDocumentId)
    {
        ValidateTerms(DocumentId, Terms);
        if (ContractVersion != 1 || DocumentId != expectedDocumentId || Revision != Hash(DocumentId, Terms)
            || JsonSerializer.SerializeToUtf8Bytes(this).Length > MaximumBytes)
            throw new InvalidDataException("Invalid, oversized or unsupported glossary snapshot.");
    }
    private static void ValidateTerms(Guid id, IReadOnlyList<string> terms)
    {
        if (id == Guid.Empty || terms is null || terms.Count > MaximumTerms
            || terms.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 256))
            throw new InvalidDataException("Invalid, oversized or unsupported glossary snapshot.");
    }
    private static string Hash(Guid id, IReadOnlyList<string> terms) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { DocumentId = id, Terms = terms })));
}
