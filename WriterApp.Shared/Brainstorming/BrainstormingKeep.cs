using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace WriterApp.Shared.Brainstorming;

public sealed record BrainstormingWritingSnapshot(WritingOutlineSnapshot Outline, IReadOnlyList<BrainstormingStoryPage> Pages);
public sealed record BrainstormingKeepReview(int Version, Guid OperationId, BrainstormingScope Scope,
    BrainstormingDraft Candidate, BrainstormingTarget Target, BrainstormingWritingSnapshot Before,
    BrainstormingTargetKind Kind, int From, int To, string ReplacedText, string AfterHtml,
    string SectionTitle, Guid NewSectionId, Guid NewPageId, Guid NewNodeId, DateTimeOffset ReviewedAt,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] BrainstormingPlacement? Placement = null);
public sealed record BrainstormingKeepReceipt(int Version, Guid OperationId, BrainstormingScope Scope,
    Guid CandidateId, string ReviewHash, BrainstormingApplicationState State, Guid HistoryId,
    long Sequence, string Message, DateTimeOffset UpdatedAt);
public sealed record BrainstormingKeepJournal(BrainstormingKeepReview Review, BrainstormingKeepReceipt? Receipt);
public sealed record BrainstormingKeepMove(Guid OperationId, long ExpectedSequence, bool Redo);

// This journal belongs to a session, independently of conversation synchronization. A local
// writing receipt must never be represented as confirmation of a server manuscript mutation.
public static class BrainstormingKeepContract
{
    public static BrainstormingWritingSnapshot Writing(BrainstormingStorySnapshot source) => new(source.Outline, source.Pages);
    public static string Hash(BrainstormingKeepReview review) => BrainstormingPersistence.Hash(review);
    public static string WritingHash(BrainstormingWritingSnapshot source) => BrainstormingPersistence.Hash(source);
    public static void Validate(BrainstormingWritingSnapshot source)
    {
        if(source is null || source.Outline is null || source.Pages is null || source.Pages.Any(p=>p is null || p.Content is null))throw new InvalidDataException("Incomplete writing snapshot.");
        WritingOutline.Validate(source.Outline);
        if(source.Pages.Count > 1000 || source.Pages.Select(p=>p.Id).Distinct().Count()!=source.Pages.Count
            || source.Pages.Any(p=>p.Id==Guid.Empty || p.Order<0 || p.Format!="Html" || p.Content.Length>500_000
                || !source.Outline.Sections.Any(s=>s.Id==p.SectionId))
            || source.Pages.Sum(p=>(long)p.Content.Length)>1_000_000)
            throw new InvalidDataException("This writing snapshot is unsupported or too large for checked application. Originals are retained.");
    }
    public static BrainstormingKeepReview Review(BrainstormingScope scope, BrainstormingDraft candidate,
        BrainstormingTarget target, BrainstormingWritingSnapshot before, int? from=null, int? to=null, string title="New section")
    {
        var kind=target.Kind; if(from is not null || to is not null)kind=BrainstormingTargetKind.ReplaceRange;
        int start=from??target.From??0,end=to??target.To??0;
        var html=candidate.EditorHtml??BrainstormingDraftContent.Html(candidate.PlainText);
        var page=kind==BrainstormingTargetKind.CreateSection?null:before.Pages.Single(p=>p.Id==target.PageId && p.SectionId==target.SectionId);
        var plain=page is null?"":BrainstormingDraftContent.PlainText(page.Content);
        string quote=kind==BrainstormingTargetKind.ReplaceRange && start>=0 && end<=plain.Length && start<end?plain[start..end]:"";
        var review=new BrainstormingKeepReview(1,Guid.NewGuid(),scope,candidate,target,before,kind,start,end,quote,
            kind==BrainstormingTargetKind.CreateSection?html:Transform(page!.Content,html,kind,start,end,quote),
            title.Trim(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow);
        Validate(review); return review;
    }
    public static void Validate(BrainstormingKeepReview r)
    {
        if(r is null || r.Scope is null || r.Candidate is null || r.Target is null || r.Before is null || r.SectionTitle is null || r.ReplacedText is null || r.AfterHtml is null || r.Candidate.PlainText is null)throw new InvalidDataException("Incomplete Keep review.");
        BrainstormingValidation.Validate(r.Scope); BrainstormingValidation.Validate(r.Target,r.Scope.DocumentId);
        BrainstormingValidation.RequireScope(r.Candidate.Scope,r.Scope); Validate(r.Before);
        var html=r.Candidate.EditorHtml??BrainstormingDraftContent.Html(r.Candidate.PlainText);
        BrainstormingDraftContent.RequireHtml(html);
        if(r.Version!=1 || r.OperationId==Guid.Empty || r.Candidate.Id==Guid.Empty || r.Candidate.TargetId!=r.Target.Id
            || r.Before.Outline.DocumentId!=r.Scope.DocumentId || r.Before.Outline.ProjectId!=r.Scope.ProjectId
            || r.NewSectionId==Guid.Empty || r.NewPageId==Guid.Empty || r.NewNodeId==Guid.Empty
            || r.ReviewedAt==default || r.SectionTitle.Length is <1 or >200
            || BrainstormingDraftContent.PlainText(html)!=r.Candidate.PlainText
            || r.Placement is null && r.Target.Kind==BrainstormingTargetKind.CreateSection && r.Kind!=r.Target.Kind
            || r.Placement is null && r.Target.Kind!=BrainstormingTargetKind.CreateSection && r.Kind==BrainstormingTargetKind.CreateSection)
            throw new InvalidDataException("Keep review differs from its exact candidate or original destination.");
        var destination=BrainstormingPlacements.Destination(r);
        BrainstormingValidation.Validate(destination,r.Scope.DocumentId);
        if(r.Placement is not null && (r.Kind!=destination.Kind || r.From!=0 || r.To!=0 || r.ReplacedText.Length!=0))throw new InvalidDataException("The reviewed placement changed.");
        var expected=r.Kind==BrainstormingTargetKind.CreateSection?html:Transform(r.Before.Pages.Single(p=>p.Id==destination.PageId && p.SectionId==destination.SectionId).Content,html,r.Kind,r.From,r.To,r.ReplacedText,r.Placement is null?null:BrainstormingPlacements.Boundary(r));
        if(expected!=r.AfterHtml)throw new InvalidDataException("The approved text mapping changed. Nothing was applied.");
        if(r.Kind==BrainstormingTargetKind.CreateSection) _=AfterOutline(r);
        if(System.Text.Encoding.UTF8.GetByteCount(BrainstormingPersistence.Json(r))>2_000_000)throw new InvalidDataException("Application recovery snapshot is too large.");
    }
    public static void RequireReceipt(BrainstormingKeepReview r,BrainstormingKeepReceipt receipt)
    {
        if(receipt.Version!=1 || receipt.Scope!=r.Scope || receipt.OperationId!=r.OperationId || receipt.CandidateId!=r.Candidate.Id
            || receipt.ReviewHash!=Hash(r) || receipt.HistoryId==Guid.Empty || receipt.Sequence<0 || !Enum.IsDefined(receipt.State))
            throw new InvalidDataException("Unconfirmed application result. Inspect this operation; do not repeat insertion.");
    }
    public static WritingOutlineSnapshot AfterOutline(BrainstormingKeepReview r)
    {
        var old=r.Before.Outline; if(r.Kind!=BrainstormingTargetKind.CreateSection)return old;
        if(r.Placement is {Mode:BrainstormingPlacementMode.NewSectionAfter} placement) {
            var sections=old.Sections.OrderBy(s=>s.Order).ThenBy(s=>s.Id).ToList();
            var afterIndex=sections.FindIndex(s=>s.Id==placement.SectionId);
            if(afterIndex<0)throw new InvalidDataException("The section insertion position is unavailable.");
            sections.Insert(afterIndex+1,new(r.NewSectionId,0,r.SectionTitle));
            var placedNodes=old.Nodes.ToList();var anchor=placedNodes.SingleOrDefault(n=>n.SectionId==placement.SectionId);
            if(anchor is not null) {
                var placedSiblings=placedNodes.Where(n=>n.ParentId==anchor.ParentId).OrderBy(n=>n.Order).ThenBy(n=>n.Id).ToList();
                placedSiblings.Insert(placedSiblings.FindIndex(n=>n.Id==anchor.Id)+1,new(r.NewNodeId,anchor.ParentId,0,"scene",r.SectionTitle,r.NewSectionId));
                var changed=placedSiblings.Select((n,i)=>n with {Order=i}).ToDictionary(n=>n.Id);
                placedNodes=placedNodes.Select(n=>changed.GetValueOrDefault(n.Id,n)).Append(changed[r.NewNodeId]).ToList();
            }
            return WritingOutline.Create(old.DocumentId,old.ProjectId,old.DocumentVersion,sections.Select((s,i)=>s with {Order=i}),placedNodes);
        }
        if(r.Target.ParentNodeId is not { } parent || !old.Nodes.Any(n=>n.Id==parent && n.Type=="chapter")
            || r.Target.BeforeNodeId is { } before && !old.Nodes.Any(n=>n.Id==before && n.ParentId==parent)
            || r.Target.BeforeSectionId is { } section && !old.Sections.Any(s=>s.Id==section))
            throw new InvalidDataException("The reviewed chapter insertion position is unavailable.");
        var siblings=old.Nodes.Where(n=>n.ParentId==parent).OrderBy(n=>n.Order).ToList();
        int index=r.Target.BeforeNodeId is { } node?siblings.FindIndex(n=>n.Id==node):siblings.Count;
        if(index!=0)throw new InvalidDataException("Review the chapter's new first section position again.");
        siblings.Insert(index,new(r.NewNodeId,parent,index,"scene",r.SectionTitle,r.NewSectionId));
        var updated=siblings.Select((n,i)=>n with {Order=i}).ToDictionary(n=>n.Id);
        var nodes=old.Nodes.Select(n=>updated.GetValueOrDefault(n.Id,n)).Append(updated[r.NewNodeId]).ToArray();
        var temporary=WritingOutline.Create(old.DocumentId,old.ProjectId,old.DocumentVersion,
            old.Sections.Append(new(r.NewSectionId,old.Sections.Count,r.SectionTitle)),nodes);
        var ordered=BrainstormingTargetResolver.ReadingNodes(temporary).Where(n=>n.SectionId is not null).Select(n=>n.SectionId!.Value)
            .Concat(old.Sections.OrderBy(s=>s.Order).Select(s=>s.Id)).Distinct().ToArray();
        int added=Array.IndexOf(ordered,r.NewSectionId);
        if(r.Target.BeforeSectionId is { } first && (added+1>=ordered.Length || ordered[added+1]!=first))
            throw new InvalidDataException("The chapter's first section or reading order changed.");
        return WritingOutline.Create(old.DocumentId,old.ProjectId,old.DocumentVersion,
            ordered.Select((id,i)=>temporary.Sections.Single(s=>s.Id==id) with {Order=i}),nodes);
    }
    // Checked DOM mapping uses the same UTF-16 quality projection as the shipped editors.
    // Insert owns new blocks at an explicitly reviewed top-level boundary. Passage replacement supports one ordinary top-level
    // text block; nested/list/table/atomic ranges refuse instead of flattening their source.
    public static string Transform(string source,string proposal,BrainstormingTargetKind kind,int from,int to,string quote,int? boundaryIndex=null)
    {
        if(source.Length==0)source="<p></p>";
        BrainstormingDraftContent.RequireHtml(source);BrainstormingDraftContent.RequireHtml(proposal);
        var parser=new HtmlParser();var body=parser.ParseDocument(source).Body!;
        var candidate=parser.ParseDocument(proposal).Body!;
        if(body.QuerySelectorAll("img").Any(i=>i.ParentElement!=body) || candidate.QuerySelectorAll("img").Any(i=>i.ParentElement!=candidate))throw new InvalidDataException("Nested images are not supported by the checked editor mapping. Original rich content is retained.");
        if(kind==BrainstormingTargetKind.InsertInPage) {
            if(from!=0 || to!=0)throw new InvalidDataException("Review a paragraph boundary for insertion.");
            int boundary=boundaryIndex??0;
            if(boundary<0 || boundary>body.ChildElementCount || body.ChildNodes.Any(n=>n is IText t && (boundaryIndex is null?!string.IsNullOrWhiteSpace(t.Data):t.Data.Length>0)))throw new InvalidDataException("The paragraph position cannot be mapped safely.");
            if(string.IsNullOrEmpty(BrainstormingDraftContent.PlainText(source)) && body.ChildElementCount==1 && body.Children[0].LocalName=="p")return proposal;
            if(boundary==0)return proposal+source;
            if(boundary==body.ChildElementCount)return source+proposal;
            var next=body.Children[boundary];foreach(var child in candidate.ChildNodes.ToArray())body.InsertBefore(child,next);
            return body.InnerHtml;
        }
        var text=BrainstormingDraftContent.PlainText(source);
        if(kind!=BrainstormingTargetKind.ReplaceRange || from<0 || to<=from || to>text.Length || text[from..to]!=quote
            || from>0 && char.IsLowSurrogate(text[from]) || to<text.Length && char.IsLowSurrogate(text[to]))
            throw new InvalidDataException("The checked replacement passage no longer matches.");
        int offset=0;IElement? block=null;
        foreach(var element in body.Children) {
            var length=BrainstormingDraftContent.PlainText(element.OuterHtml).Length;
            if(element.LocalName is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" && from>=offset && to<=offset+length){block=element;break;}
            offset+=length+2;
        }
        if(block is null || body.ChildNodes.Any(n=>n is IText t && t.Data.Length>0) || block.QuerySelector("img,br,code") is not null)
            throw new InvalidDataException("Select a supported passage inside one paragraph or heading. Original rich nodes are retained.");
        int start=from-offset,end=to-offset;
        if(candidate.ChildElementCount!=1 || candidate.Children[0].LocalName!="p") {
            if(start!=0 || end!=block.TextContent.Length || block.LocalName!="p")throw new InvalidDataException("Multiple proposal blocks require a complete paragraph replacement.");
            foreach(var child in candidate.ChildNodes.ToArray())body.InsertBefore(child,block);
            block.Remove();
        } else {
            var original=block.Clone(true);int position=0;
            INode? SliceNode(INode node,int begin,int finish) {
                if(node is IText t){int index=position;position+=t.Data.Length;int a=Math.Max(begin-index,0),b=Math.Min(finish-index,t.Data.Length);return b>a?body.Owner!.CreateTextNode(t.Data[a..b]):null;}
                var clone=node.Clone(false);foreach(var child in node.ChildNodes){var part=SliceNode(child,begin,finish);if(part is not null)clone.AppendChild(part);}return clone.HasChildNodes?clone:null;
            }
            var left=SliceNode(original,0,start);position=0;var right=SliceNode(original,end,original.TextContent!.Length);
            block.InnerHtml="";
            foreach(var child in left?.ChildNodes.ToArray()??[])block.AppendChild(child);
            foreach(var child in candidate.Children[0].ChildNodes.ToArray())block.AppendChild(child);
            foreach(var child in right?.ChildNodes.ToArray()??[])block.AppendChild(child);
        }
        var result=body.InnerHtml;BrainstormingDraftContent.RequireHtml(result);
        if(BrainstormingDraftContent.PlainText(result)!=text[..from]+BrainstormingDraftContent.PlainText(proposal)+text[to..])
            throw new InvalidDataException("The replacement could not preserve the source mapping.");
        return result;
    }
}
