using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    [Fact]
    public async Task SharingUpgradeRevokesPriorSessionsAndRetainsBinaryWritingAndOwnership()
    {
        await Convert();
        var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page);
        var settings = new SessionOptions(); var protector = new EphemeralDataProtectionProvider();
        var sessions = new CollaborationSessionService(_db, protector, settings, _plans, new DeletedUserIdentityService(_db), TimeProvider.System, Access);
        var issued = await sessions.Issue("owner", _document, _page, new(page.Generation, Guid.NewGuid(), 2, 1, page.SchemaFingerprint, true), default);
        byte[] retained = (await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page)).State;
        await _db.GetService<IMigrator>().MigrateAsync(_database is null ? "20261017193757_CollaborationProjections" : "20261017193804_CollaborationProjectionsSqlServer");
        await _db.Database.MigrateAsync(); _db.ChangeTracker.Clear();
        var saved = await _db.CollaborationSessions.AsNoTracking().SingleAsync(s => s.Id == issued.SessionId);
        Assert.True(saved.Revoked); Assert.Equal("owner", saved.OwnerUserId); Assert.Equal("owner", saved.ResourceOwnerUserId); Assert.True(saved.Version > issued.Version);
        Assert.Equal(retained, (await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page)).State);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => sessions.Authorize(new(issued.Ticket, issued.RoomName, Guid.NewGuid(), true), default));
        await InviteMember("editor", CollaborationRole.Editor);
        await Assert.ThrowsAnyAsync<Exception>(() => _db.GetService<IMigrator>().MigrateAsync(_database is null ? "20261017193757_CollaborationProjections" : "20261017193804_CollaborationProjectionsSqlServer"));
        Assert.Single(await _db.CollaborationMembers.ToArrayAsync());
    }
}
