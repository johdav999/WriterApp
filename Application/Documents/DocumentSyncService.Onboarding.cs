using Microsoft.EntityFrameworkCore;
using WriterApp.Shared.Sync;

namespace WriterApp.Application.Documents;

public sealed partial class DocumentSyncService
{
    public async Task<SyncChanges> OnboardingChangesAsync(string owner,string? cursor,CancellationToken ct) {
        var grant=await db.OnboardingDemoWorkspaces.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerUserId==owner,ct)
            ?? throw new DocumentSyncException(404,"demo_not_found","Deliberately create or reopen the server demo first.");
        await AuthorizeAsync(owner,ct,grant.DocumentId);
        if(cursor is not null && (!cursor.StartsWith("demo-v1:"+grant.DocumentId+":",StringComparison.Ordinal) || cursor.Length>256))
            throw new DocumentSyncException(400,"invalid_cursor","Restart demo synchronization without a cursor.");
        var state=await OwnedStateAsync(owner,grant.DocumentId,ct);
        string next="demo-v1:"+grant.DocumentId+":"+state.Version;
        return new(cursor==next ? [] : [State(state)],next,false);
    }
}
