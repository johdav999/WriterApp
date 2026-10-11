using AngleSharp.Html.Parser;

namespace WriterApp.Shared.Brainstorming;

public enum BrainstormingPlacementMode { ContinueSection, NewSectionAfter, PageBoundary }
// Placement is author-reviewed data. It never changes the draft's generation target.
public sealed record BrainstormingPlacement(BrainstormingPlacementMode Mode, Guid SectionId, Guid? PageId = null, int BoundaryIndex = 0);
public sealed record BrainstormingPlacementBlock(int Index, string Text);

public static class BrainstormingPlacements
{
    public static string SectionLabel(BrainstormingWritingSnapshot source, Guid sectionId)
    {
        var section=source.Outline.Sections.Single(s=>s.Id==sectionId);
        var ancestors=new List<string>();var node=source.Outline.Nodes.FirstOrDefault(n=>n.SectionId==sectionId);
        while(node?.ParentId is { } parent) { node=source.Outline.Nodes.Single(n=>n.Id==parent);ancestors.Insert(0,node.Title); }
        return string.Join(" → ",ancestors.Append(section.Title));
    }
    public static IReadOnlyList<BrainstormingPlacementBlock> Blocks(string html)
    {
        if(string.IsNullOrEmpty(html))html="<p></p>";
        BrainstormingDraftContent.RequireHtml(html);
        var body = new HtmlParser().ParseDocument(string.IsNullOrEmpty(html) ? "<p></p>" : html).Body!;
        return body.Children.Select((b, i) => new BrainstormingPlacementBlock(i, BrainstormingDraftContent.PlainText(b.OuterHtml))).ToArray();
    }

    public static BrainstormingPlacement Default(BrainstormingKeepReview review)
    {
        var section = review.Target.SectionId ?? review.Target.BeforeSectionId
            ?? review.Before.Outline.Sections.FirstOrDefault()?.Id
            ?? throw new InvalidDataException("Create a manuscript section before choosing a destination.");
        return new(BrainstormingPlacementMode.ContinueSection, section);
    }

    public static BrainstormingTarget Destination(BrainstormingKeepReview review)
    {
        if (review.Placement is not { } placement) return review.Target;
        if (!Enum.IsDefined(placement.Mode) || placement.SectionId == Guid.Empty || placement.BoundaryIndex < 0
            || !review.Before.Outline.Sections.Any(s => s.Id == placement.SectionId))
            throw new InvalidDataException("Choose an available manuscript section.");
        var original = review.Target;
        var nodes = review.Before.Outline.Nodes;
        if (placement.Mode == BrainstormingPlacementMode.NewSectionAfter)
        {
            if (placement.PageId is not null || placement.BoundaryIndex != 0) throw new InvalidDataException("A new section cannot also target a paragraph.");
            var linked = nodes.Where(n => n.SectionId == placement.SectionId).ToArray();
            if (linked.Length > 1) throw new InvalidDataException("This section has multiple outline links. Choose a section with one outline position.");
            var anchor = linked.SingleOrDefault();
            var nextNode = anchor is null ? null : nodes.Where(n => n.ParentId == anchor.ParentId && n.Order > anchor.Order).OrderBy(n => n.Order).ThenBy(n => n.Id).FirstOrDefault();
            return original with { Kind = BrainstormingTargetKind.CreateSection, SectionId = null, PageId = null,
                ChapterNodeId = null, SceneNodeId = null, ParentNodeId = anchor?.ParentId,
                BeforeNodeId = nextNode?.Id, BeforeSectionId = null, From = null, To = null, TextMapping = null };
        }
        var pages = review.Before.Pages.Where(p => p.SectionId == placement.SectionId).OrderBy(p => p.Order).ThenBy(p => p.Id).ToArray();
        var page = placement.Mode == BrainstormingPlacementMode.ContinueSection ? pages.LastOrDefault()
            : pages.SingleOrDefault(p => p.Id == placement.PageId);
        if (page is null) throw new InvalidDataException("This section has no available page. Create a page in the manuscript first.");
        if (placement.Mode == BrainstormingPlacementMode.ContinueSection && (placement.PageId is not null || placement.BoundaryIndex != 0)
            || placement.Mode == BrainstormingPlacementMode.PageBoundary && placement.BoundaryIndex > Blocks(page.Content).Count)
            throw new InvalidDataException("The reviewed paragraph position is unavailable.");
        return original with { Kind = BrainstormingTargetKind.InsertInPage, SectionId = page.SectionId, PageId = page.Id,
            ChapterNodeId = null, SceneNodeId = null, ParentNodeId = null, BeforeNodeId = null, BeforeSectionId = null,
            From = 0, To = 0, TextMapping = "quality-v1" };
    }

    public static int Boundary(BrainstormingKeepReview review) => review.Placement?.Mode == BrainstormingPlacementMode.ContinueSection
        ? Blocks(review.Before.Pages.Single(p => p.Id == Destination(review).PageId).Content).Count
        : review.Placement?.BoundaryIndex ?? 0;

    public static BrainstormingKeepReview ReviewAt(BrainstormingKeepReview source, BrainstormingPlacement placement, string title)
    {
        var review = source with { OperationId = Guid.NewGuid(), Placement = placement, SectionTitle = title.Trim(), From = 0, To = 0,
            ReplacedText = "", ReviewedAt = DateTimeOffset.UtcNow };
        var destination = Destination(review);
        review = review with { Kind = destination.Kind, AfterHtml = destination.Kind == BrainstormingTargetKind.CreateSection
            ? review.Candidate.EditorHtml ?? BrainstormingDraftContent.Html(review.Candidate.PlainText)
            : BrainstormingKeepContract.Transform(review.Before.Pages.Single(p => p.Id == destination.PageId).Content,
                review.Candidate.EditorHtml ?? BrainstormingDraftContent.Html(review.Candidate.PlainText), destination.Kind, 0, 0, "", Boundary(review)) };
        BrainstormingKeepContract.Validate(review);
        return review;
    }
}
