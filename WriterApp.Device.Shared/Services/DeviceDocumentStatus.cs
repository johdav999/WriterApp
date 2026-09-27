using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public enum LocalSaveState { Unsaved, Saving, Saved, Error }

public sealed record DeviceDocumentStatus(string Label, string Detail, string Tone)
{
    public static DeviceDocumentStatus For(LocalDocument document, LocalSaveState saveState = LocalSaveState.Saved,
        bool isSyncing = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (saveState == LocalSaveState.Error)
            return new("Save failed", "Recent changes are not safely stored on this device.", "danger");
        if (saveState == LocalSaveState.Unsaved)
            return new("Unsaved", "Recent changes have not been saved to this device.", "warning");
        if (saveState == LocalSaveState.Saving)
            return new("Saving", "Writing recent changes to this device.", "warning");
        if (document.SyncState == LocalSyncState.Conflict)
            return new("Conflict", "Local and cloud versions need review. Your local copy is saved.", "danger");
        if (document.SyncState == LocalSyncState.Error)
            return new("Sync error", "Saved on this device. Cloud synchronization needs attention.", "danger");
        if (isSyncing)
            return new("Syncing", "Saved on this device. Updating the cloud copy.", "progress");
        if (document.ServerDocumentId is null)
            return new("Offline · Saved locally", "Available on this device without an account or connection.", "local");
        if (document.SyncState == LocalSyncState.PendingUpload)
            return new("Waiting to sync", "Saved on this device. Cloud changes are pending.", "warning");
        return new("Synced", "Saved on this device and synchronized.", "success");
    }
}
