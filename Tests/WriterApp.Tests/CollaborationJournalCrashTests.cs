using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed class CollaborationJournalCrashTests
{
    [Fact]
    public async Task ForcedTerminationBeforeSwapAfterSaveAndAfterReceiptRecoversExactAtomicPrefix()
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp_CollaborationCrash_" + Guid.NewGuid().ToString("N"));
        var scope = new CollaborationJournalScope("http://localhost:5188/", "owner", "test", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(2, 1, new string('a', 64)));
        var disk = new DeviceCollaborationJournal(root);
        try
        {
            await disk.InitializeAsync(scope, Convert.ToBase64String([0, 0]), Convert.ToHexString(SHA256.HashData([0, 0])).ToLowerInvariant());
            await Crash("stage");
            var old = await disk.LoadAsync(scope); Assert.Equal(0, old!.Revision); Assert.Empty(old.Pending);
            Assert.NotEmpty(Directory.GetFiles(root, "*.tmp")); // The killed process never reached finally/cleanup.
            await Crash("saved");
            var saved = await disk.LoadAsync(scope); Assert.Equal(1, saved!.Revision); Assert.Single(saved.Pending); Assert.Equal(Convert.ToBase64String([1, 0]), saved.State);
            await Crash("ack");
            var acknowledged = await disk.LoadAsync(scope); Assert.Equal(2, acknowledged!.Revision); Assert.Empty(acknowledged.Pending); Assert.Single(acknowledged.Receipts);
        }
        finally
        {
            if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("WriterApp_CollaborationCrash_", StringComparison.Ordinal)) Directory.Delete(root, true);
        }
        async Task Crash(string mode)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BlazorApp.csproj"))) directory = directory.Parent;
            string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            string dll = Path.Combine(directory!.FullName, "Tests", "Collaboration.JournalCrash", "bin", configuration, "net10.0", "WriterApp.CollaborationJournalCrash.dll");
            var start = new ProcessStartInfo("dotnet") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(dll); start.ArgumentList.Add(root); start.ArgumentList.Add(mode); start.ArgumentList.Add(JsonSerializer.Serialize(scope, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); Assert.NotEqual(0, process.ExitCode); Assert.Empty(await output); Assert.Empty(await error); }
            finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        }
    }
}
