using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Collaboration;

string root = args[0], mode = args[1];
var scope = JsonSerializer.Deserialize<CollaborationJournalScope>(args[2], new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
var disk = mode == "stage" ? new DeviceCollaborationJournal(root, new AtomicDocumentWriter(_ => Crash())) : new DeviceCollaborationJournal(root);
var page = await disk.LoadAsync(scope) ?? throw new InvalidDataException("Missing owned fixture checkpoint");
if (mode == "ack")
{
    var pending = page.Pending.Single();
    await disk.AcknowledgeAsync(scope, new(pending.PageId, pending.Generation, pending.InstallationId, pending.UpdateId, pending.Sha256, 1, 1));
}
else
{
    byte[] bytes = [1, 0]; string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    var envelope = new CollaborationPendingEnvelope("commitUpdate", 1, scope.DocumentId, scope.PageId, scope.Generation, page.InstallationId,
        Guid.Parse("11111111-2222-3333-4444-555555555555"), Convert.ToBase64String(bytes), hash);
    await disk.SaveAsync(scope, new(page.Revision, Convert.ToBase64String(bytes), hash, envelope));
}
Crash();
static void Crash() { Process.GetCurrentProcess().Kill(); Thread.Sleep(Timeout.Infinite); }
