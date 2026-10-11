using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WriterApp.Application.Continuity;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Domain.Documents;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Canon;
using WriterApp.Shared.Editor;

namespace WriterApp.Application.AI;

public sealed class BrainstormingServerProjector : IBrainstormingTextProjector
{
    public Task<BrainstormingProjectedPage> ProjectAsync(BrainstormingStoryPage page,CancellationToken ct) {
        ct.ThrowIfCancellationRequested(); if(page.Format!="Html")throw new InvalidDataException("Save supported HTML in the editor before server context review.");
        return Task.FromResult(new BrainstormingProjectedPage(page.Id,BrainstormingValidation.Fingerprint(page.Content),BrainstormingDraftContent.PlainText(page.Content)));
    }
}
public sealed class BrainstormingGenerationContext(AppDbContext db,IBibleStore bibles,IEntitlementService entitlements,string owner)
{
    public async Task<BrainstormingServerContext> PrepareAsync(BrainstormingServerContextRequest request,CancellationToken ct) {
        if(request.Version!=1 || request.Scope.Space!=BrainstormingIdentitySpace.Server || request.Query is null || request.Choices is null || request.Choices.Count>100
            || request.SupportingDocuments is null || request.SupportingDocuments.Count>10 || request.SupportingDocuments.Distinct().Count()!=request.SupportingDocuments.Count)throw new InvalidDataException("Invalid server context review.");
        var reader=new BrainstormingStorySourceReader(db,owner); var source=await reader.ReadAsync(request.Scope.DocumentId,request.Scope.ProjectId,ct);
        var documents=new List<BrainstormingStorySnapshot>{source};var refs=source.References.ToList(); var projector=new BrainstormingServerProjector();
        foreach(var kind in Enum.GetValues<CanonKind>()) {
            var type=Enum.Parse<BrainstormingSourceKind>(kind.ToString());var id=BrainstormingStorySources.Identity(source.Outline.DocumentId,type.ToString());
            if(!request.Choices.Any(c=>c.SourceId==id && c.Enabled))continue;
            var access=await entitlements.GetEntitlementsAsync(owner);
            if(!FeatureRegistry.IsFeatureAllowed(FeatureKey.StoryCanon,entitlements.GetUserTier(access)))throw new InvalidDataException("Story canon is unavailable for this account.");
            var stored=await bibles.GetSnapshotAsync(source.Outline.DocumentId,(BibleType)kind,ct);
            var sections=source.Outline.Sections.Select(s=>new Section { SectionId=s.Id,Order=s.Order,Title=s.Title,Content=new SectionContent { Format="html", Value=string.Join("\n\n",source.Pages.Where(p=>p.SectionId==s.Id).OrderBy(p=>p.Order).ThenBy(p=>p.Id).Select(p=>p.Content)) }}).ToList();
            var hash=BibleRefreshService.SourceHash(new WriterApp.Domain.Documents.Document { DocumentId=source.Outline.DocumentId, Chapters=[new Chapter { Sections=sections }] });
            var snapshot=stored is null?null:new DeviceBibleSnapshot(1,source.Outline.DocumentId,kind,WriterApp.Controllers.DocumentBiblesController.SnapshotToken(stored),
                stored.LastRefreshSourceHash==hash?source.Outline.DocumentVersion:null,source.Outline.DocumentVersion,stored.LastRefreshSourceHash,stored.LastRefreshUtc,
                BiblePatchApplier.NormalizeContent((BibleType)kind,stored.ContentJson),stored.LastRefreshSourceHash==hash?0:1,true);
            refs.RemoveAll(r=>r.Id==id);refs.Add(BrainstormingStorySources.Canon(source.Outline.DocumentId,type,snapshot,source.Outline.DocumentId,source.Outline.DocumentVersion,false));
        }
        foreach(var id in request.SupportingDocuments) {
            if(id==request.Scope.DocumentId)throw new InvalidDataException("Supporting source duplicates the active manuscript.");
            var support=await reader.ReadAsync(id,ct:ct);documents.Add(support); refs.Add(BrainstormingStorySources.Supporting(support,await BrainstormingStorySources.ProjectAsync(support,projector,ct)));
        }
        source=BrainstormingStorySources.WithReferences(source,refs);
        var prepared=BrainstormingContextPolicy.Prepare(request.Scope,source,await BrainstormingStorySources.ProjectAsync(source,projector,ct),request.Query,request.Choices,request.Budget);
        // Capture again after all selected sources and canon have been read.
        foreach(var document in documents)if((await reader.ReadAsync(document.Outline.DocumentId,ct:ct)).Stamp!=document.Stamp)throw new InvalidDataException("A story source changed during review.");
        return new(prepared,documents);
    }
    public async Task RequireAsync(BrainstormingContext context,BrainstormingTarget target,CancellationToken ct) {
        BrainstormingContextPolicy.GenerationData(context);
        var query=new BrainstormingTargetQuery(ChapterId:target.ChapterNodeId,SectionId:target.Kind==BrainstormingTargetKind.CreateSection?null:target.SectionId,SceneId:target.SceneNodeId,NewFirstSection:target.Kind==BrainstormingTargetKind.CreateSection);
        var choices=context.Sources.Select(s=>new BrainstormingContextChoice(s.Id,s.Included,s.IsSummary?s.Text:null,s.IsSummary?s.Source.ContentRevision:null,s.IsSummary?s.Source.Fingerprint:null,!s.Included&&!s.Mandatory)).ToArray();
        var fresh=await PrepareAsync(new(1,context.Scope,query,choices,context.Sources.Where(s=>s.Kind==BrainstormingSourceKind.SupportingDocument).Select(s=>s.DocumentId).Distinct().ToArray(),context.Policy!.CharacterBudget),ct);
        if(fresh.Preparation.Resolution.Target is not { } destination || destination with { Id=target.Id } != target || fresh.Preparation.Context is not { } manifest)throw new InvalidDataException("The checked target changed. Refresh context explicitly.");
        foreach(var source in context.Sources.Where(s=>s.Included)) {
            var actual=manifest.Sources.SingleOrDefault(s=>s.Id==source.Id);
            if(actual is null || !actual.Included || actual.Source!=source.Source || actual.Text!=source.Text || actual.Title!=source.Title || actual.Kind!=source.Kind || actual.Nature!=source.Nature
                || BrainstormingPersistence.Json(actual.Parts)!=BrainstormingPersistence.Json(source.Parts))throw new InvalidDataException("An enabled story source changed or does not match the checked manuscript. Refresh context.");
        }
        if(manifest.Sources.Any(s=>s.Mandatory&&!context.Sources.Any(old=>old.Id==s.Id)))throw new InvalidDataException("Required context is missing. Refresh context.");
    }
}
