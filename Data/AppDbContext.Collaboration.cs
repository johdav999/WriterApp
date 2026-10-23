using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Collaboration;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Data;

public partial class AppDbContext
{
    public DbSet<CollaborationBrainstormDraftRecord> CollaborationBrainstormDrafts => Set<CollaborationBrainstormDraftRecord>();
    public DbSet<CollaborationRecoveryStateRecord> CollaborationRecoveryStates => Set<CollaborationRecoveryStateRecord>();
    public DbSet<CollaborationCommentRecord> CollaborationComments => Set<CollaborationCommentRecord>();
    public DbSet<CollaborationCommentMessageRecord> CollaborationCommentMessages => Set<CollaborationCommentMessageRecord>();
    public DbSet<CollaborationCommentOperationRecord> CollaborationCommentOperations => Set<CollaborationCommentOperationRecord>();
    public DbSet<CollaborationMemberRecord> CollaborationMembers => Set<CollaborationMemberRecord>();
    public DbSet<CollaborationInvitationRecord> CollaborationInvitations => Set<CollaborationInvitationRecord>();
    public DbSet<CollaborationSupportingRecord> CollaborationSupportingLinks => Set<CollaborationSupportingRecord>();
    public DbSet<CollaborationProjectionRecord> CollaborationProjections => Set<CollaborationProjectionRecord>();
    public DbSet<CollaborationManifestRecord> CollaborationManifests => Set<CollaborationManifestRecord>();
    public DbSet<CollaborationCommandRecord> CollaborationCommands => Set<CollaborationCommandRecord>();
    public DbSet<CollaborationSessionRecord> CollaborationSessions => Set<CollaborationSessionRecord>();
    public DbSet<CollaborationPageRecord> CollaborationPages => Set<CollaborationPageRecord>();
    public DbSet<CollaborationUpdateRecord> CollaborationUpdates => Set<CollaborationUpdateRecord>();
    public DbSet<CollaborationReceiptRecord> CollaborationReceipts => Set<CollaborationReceiptRecord>();
    public DbSet<CollaborationSnapshotRecord> CollaborationSnapshots => Set<CollaborationSnapshotRecord>();
    public DbSet<CollaborationOutboxEventRecord> CollaborationOutboxEvents => Set<CollaborationOutboxEventRecord>();
    public DbSet<CollaborationMigrationJobRecord> CollaborationMigrationJobs => Set<CollaborationMigrationJobRecord>();

    private static void ConfigureCollaboration(ModelBuilder builder)
    {
        builder.Entity<CollaborationBrainstormDraftRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BackendKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.ResourceOwnerId).HasMaxLength(128).IsRequired();
            e.Property(x => x.AuthorId).HasMaxLength(128).IsRequired();
            e.Property(x => x.PlainText).HasMaxLength(500000).IsRequired();
            e.HasIndex(x => new { x.DocumentId, x.BackendKey, x.CreatedUnixMilliseconds });
        });
        builder.Entity<CollaborationRecoveryStateRecord>(e =>
        {
            e.ToTable("CollaborationRecoveryStates", t => t.HasCheckConstraint("CK_CollaborationRecoveryStates_Singleton", "Id = 'database'"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(16); e.Property(x => x.RequiredEnvironment).HasMaxLength(64).IsRequired(); e.Property(x => x.OperationId).IsConcurrencyToken();
        });
        builder.Entity<CollaborationCommentRecord>(e =>
        {
            e.ToTable("CollaborationComments", t => t.HasCheckConstraint("CK_CollaborationComments_Revision", "Revision > 0")); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PageId, x.Generation }); e.Property(x => x.Revision).IsConcurrencyToken();
            e.Property(x => x.AuthorId).HasMaxLength(128).IsRequired(); e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Start).HasMaxLength(1024); e.Property(x => x.End).HasMaxLength(1024); e.Property(x => x.LegacySource).HasMaxLength(64);
            e.HasOne<CollaborationPageRecord>().WithMany().HasForeignKey(x => new { x.PageId, x.Generation }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CollaborationCommentMessageRecord>(e =>
        {
            e.ToTable("CollaborationCommentMessages"); e.HasKey(x => x.Id); e.HasIndex(x => x.ThreadId);
            e.Property(x => x.AuthorId).HasMaxLength(128).IsRequired(); e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.HasOne<CollaborationCommentRecord>().WithMany().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CollaborationCommentOperationRecord>(e =>
        {
            e.ToTable("CollaborationCommentOperations"); e.HasKey(x => x.Id);
            e.Property(x => x.ActorId).HasMaxLength(128).IsRequired(); e.Property(x => x.RequestSha256).HasMaxLength(64).IsRequired();
            e.HasOne<CollaborationCommentRecord>().WithMany().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CollaborationMemberRecord>(e =>
        {
            e.ToTable("CollaborationMembers", t => t.HasCheckConstraint("CK_CollaborationMembers_RoleRevision", "Role IN (1,2) AND Revision > 0")); e.HasKey(x => new { x.DocumentId, x.UserId });
            e.Property(x => x.UserId).HasMaxLength(128); e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.Revoked });
        });
        builder.Entity<CollaborationInvitationRecord>(e =>
        {
            e.ToTable("CollaborationInvitations", t => t.HasCheckConstraint("CK_CollaborationInvitations_TargetRole", "Role IN (1,2) AND Revision > 0 AND ((RecipientUserId IS NULL AND RecipientEmail IS NOT NULL) OR (RecipientUserId IS NOT NULL AND RecipientEmail IS NULL))")); e.HasKey(x => x.Id); e.Property(x => x.Revision).IsConcurrencyToken();
            e.Property(x => x.RecipientUserId).HasMaxLength(128); e.Property(x => x.RecipientEmail).HasMaxLength(254);
            e.Property(x => x.AcceptedBy).HasMaxLength(128); e.Property(x => x.TokenSha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.RequestSha256).HasMaxLength(64).IsRequired(); e.HasIndex(x => x.DocumentId);
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CollaborationSupportingRecord>(e =>
        {
            e.ToTable("CollaborationSupportingLinks"); e.HasKey(x => new { x.ManuscriptId, x.SupportingDocumentId });
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.ManuscriptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.SupportingDocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CollaborationProjectionRecord>(e =>
        {
            e.ToTable("CollaborationProjections"); e.HasKey(x => new { x.PageId, x.Generation });
            e.HasOne<CollaborationPageRecord>().WithOne().HasForeignKey<CollaborationProjectionRecord>(x => new { x.PageId, x.Generation }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Sequence).IsConcurrencyToken(); e.Property(x => x.StateSha256).HasMaxLength(64).IsRequired();
        });
        builder.Entity<CollaborationManifestRecord>(e =>
        {
            e.ToTable("CollaborationManifests"); e.HasKey(x => x.DocumentId);
            e.HasOne<DocumentRecord>().WithOne().HasForeignKey<CollaborationManifestRecord>(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.StructureRevision).IsConcurrencyToken();
        });
        builder.Entity<CollaborationCommandRecord>(e =>
        {
            e.ToTable("CollaborationCommands"); e.HasKey(x => x.Id);
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.RequestSha256).HasMaxLength(64).IsRequired();
        });
        builder.Entity<CollaborationSessionRecord>(e =>
        {
            e.ToTable("CollaborationSessions"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExpiresAtUnixMilliseconds);
            e.HasOne<CollaborationPageRecord>().WithMany().HasForeignKey(x => new { x.PageId, x.Generation }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OwnerUserId).HasMaxLength(128).IsRequired();
            e.Property(x => x.ResourceOwnerUserId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Environment).HasMaxLength(64).IsRequired();
            e.Property(x => x.SchemaFingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
        });
        builder.Entity<SceneContentRecord>().ToTable("SceneContents", t => { t.HasTrigger("CollaborationFence_SceneContents"); t.UseSqlOutputClause(false); t.UseSqlReturningClause(false); });
        builder.Entity<DocumentOutlineNodeRecord>().ToTable("DocumentOutlineNodes", t => { t.HasTrigger("CollaborationFence_DocumentOutlineNodes"); t.UseSqlOutputClause(false); t.UseSqlReturningClause(false); });
        builder.Entity<DocumentOutlineRecord>().ToTable("DocumentOutlines", t => { t.HasTrigger("CollaborationFence_DocumentOutlines"); t.UseSqlOutputClause(false); t.UseSqlReturningClause(false); });
        builder.Entity<DocumentRecord>(e =>
        {
            e.Property(x => x.CollaborationMode).HasDefaultValue(CollaborationMode.Legacy);
            e.Property(x => x.CollaborationMigrationState).HasDefaultValue(CollaborationMigrationState.None);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Documents_CollaborationMode", "CollaborationMode IN (0,1,2)");
                t.HasCheckConstraint("CK_Documents_CollaborationMigrationState", "CollaborationMigrationState BETWEEN 0 AND 4");
            });
        });
        builder.Entity<CollaborationPageRecord>(e =>
        {
            e.ToTable("CollaborationPages", t => t.HasCheckConstraint("CK_CollaborationPages_Sequences",
                "CommittedSequence >= 0 AND SnapshotSequence >= 0 AND ProjectionSequence >= 0 AND SnapshotSequence <= CommittedSequence AND ProjectionSequence <= CommittedSequence AND LeaseEpoch >= 0"));
            e.HasKey(x => new { x.PageId, x.Generation });
            e.HasIndex(x => new { x.DocumentId, x.PageId, x.Generation }).IsUnique();
            e.HasOne<PageRecord>().WithMany().HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SchemaFingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.LeaseOwner).HasMaxLength(128);
            e.Property(x => x.LeaseEpoch).IsConcurrencyToken();
            e.Property(x => x.CommittedSequence).IsConcurrencyToken();
        });
        builder.Entity<CollaborationUpdateRecord>(e =>
        {
            e.ToTable("CollaborationUpdates", t => t.HasCheckConstraint("CK_CollaborationUpdates_Sequence", "Sequence > 0"));
            e.HasKey(x => new { x.PageId, x.Generation, x.Sequence });
            e.HasIndex(x => new { x.PageId, x.Generation, x.InstallationId, x.UpdateId }).IsUnique();
            e.HasOne<CollaborationPageRecord>().WithMany().HasForeignKey(x => new { x.PageId, x.Generation }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.AuthorUserId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.Update).IsRequired();
        });
        builder.Entity<CollaborationReceiptRecord>(e =>
        {
            e.ToTable("CollaborationReceipts", t => t.HasCheckConstraint("CK_CollaborationReceipts_Sequence", "CommitSequence > 0"));
            e.HasKey(x => new { x.PageId, x.Generation, x.InstallationId, x.UpdateId });
            e.HasIndex(x => new { x.PageId, x.Generation, x.CommitSequence }).IsUnique();
            e.HasOne<CollaborationPageRecord>().WithMany().HasForeignKey(x => new { x.PageId, x.Generation }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        });
        builder.Entity<CollaborationSnapshotRecord>(e =>
        {
            e.ToTable("CollaborationSnapshots", t => t.HasCheckConstraint("CK_CollaborationSnapshots_Sequence", "Sequence >= 0"));
            e.HasKey(x => new { x.PageId, x.Generation, x.Sequence });
            e.HasOne<CollaborationPageRecord>().WithMany().HasForeignKey(x => new { x.PageId, x.Generation }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.State).IsRequired();
        });
        builder.Entity<CollaborationOutboxEventRecord>(e =>
        {
            e.ToTable("CollaborationOutboxEvents"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PageId, x.Generation, x.CommitSequence }).IsUnique();
            e.HasIndex(x => x.CompletedAtUnixMilliseconds);
            e.HasOne<CollaborationPageRecord>().WithMany().HasForeignKey(x => new { x.PageId, x.Generation }).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CollaborationMigrationJobRecord>(e =>
        {
            e.ToTable("CollaborationMigrationJobs"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.DocumentId).IsUnique();
            e.HasOne<DocumentRecord>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SourceVersion).HasMaxLength(128).IsRequired();
            e.Property(x => x.SourceSha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.FailureCode).HasMaxLength(128);
            e.Property(x => x.RecoverySnapshot).IsRequired();
        });
    }
}
