using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

// Early route check improves errors and prevents side effects; storage fences close races.
public sealed class CollaborationWriteFilter(AppDbContext db, IUserIdResolver users) : IAsyncActionFilter, IExceptionFilter, IOrderedFilter
{
    public int Order => int.MinValue + 100;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Uploads authorize the current writer and store under the manuscript's
        // resource owner; they do not replace any document or page snapshot.
        if(context.Controller is DocumentImageAssetsController) { await next(); return; }
        // Brainstorm context and private conversations never write shared manuscript snapshots.
        if (context.Controller is BrainstormingConversationController or BrainstormingSessionsController or BrainstormingKeepsController or BrainstormingPlanningController) { await next(); return; }
        // These sealed controllers use collaboration-aware services rather than legacy snapshot writes.
        if (context.Controller is CollaborationEditorToolsController or WebTranslationsController or WebAiRecoveryController or WebAiHistoryController or AiActionsController or CollaborationSessionsController or InternalCollaborationController or CollaborationManuscriptsController or CollaborationSharingController or CollaborationCommentsController or DocumentExportController or ExportPreviewController or LocalCollaborationTestController) { await next(); return; }
        bool legacyContentRead = context.Controller is PagesController or ProjectSceneContentController;
        if (!legacyContentRead && (HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method))
            || HttpMethods.IsOptions(context.HttpContext.Request.Method) || context.HttpContext.User.Identity?.IsAuthenticated != true)
        { await next(); return; }
        Guid? Id(string name) => context.ActionArguments.TryGetValue(name, out var value) && value is Guid id ? id : null;
        Guid? document = Id("documentId") ?? (context.Controller is DocumentSyncController ? Id("id") : null);
        document ??= context.ActionArguments.Values.Select(v => v?.GetType().GetProperty("DocumentId")?.GetValue(v)).OfType<Guid>().Cast<Guid?>().FirstOrDefault();
        Guid? page = Id("pageId"), section = Id("sectionId"), project = Id("projectId"), scene = Id("sceneNodeId") ?? Id("sceneId");
        if (document is null && page is null && section is null && project is null && scene is null) { await next(); return; }
        string owner = users.ResolveUserId(context.HttpContext.User);
        var mode = await db.Documents.AsNoTracking().Where(d => d.OwnerUserId == owner && d.CollaborationMode != CollaborationMode.Legacy
            && (d.Id == document || d.ProjectId == project
                || db.Pages.Any(p => p.Id == page && p.DocumentId == d.Id)
                || db.Sections.Any(s => s.Id == section && s.DocumentId == d.Id)
                || db.ProjectNodes.IgnoreQueryFilters().Any(n => n.Id == scene && n.ProjectId == d.ProjectId)))
            .Select(d => (CollaborationMode?)d.CollaborationMode).FirstOrDefaultAsync(context.HttpContext.RequestAborted);
        if (mode.HasValue)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.Result = new ConflictObjectResult(CollaborationWriteException.ForMode(mode.Value));
            return;
        }
        await next();
    }

    public void OnException(ExceptionContext context)
    {
        if(context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor descriptor && descriptor.ControllerTypeInfo.AsType()==typeof(CollaborationEditorToolsController) && context.Exception is (InvalidDataException or InvalidOperationException or WriterApp.Application.Documents.DocumentSyncException)) {
            context.Result=new ConflictObjectResult(new {code="collaboration_source_changed",message="Writing or structure changed. Your proposal is retained; review the current manuscript before trying again.",preserveLocalDraft=true});context.ExceptionHandled=true;return;
        }
        if (context.Exception is CollaborationCommitException commit)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.RetryAfter = "1";
            context.Result = new ConflictObjectResult(new { code = commit.Code, preserveLocalDraft = true }); context.ExceptionHandled = true; return;
        }
        if (context.Exception is CollaborationSessionDeniedException)
        {
            context.Result = new ObjectResult(new { code = "collaboration_access_unavailable", preserveLocalDraft = true }) { StatusCode = 403 };
            context.ExceptionHandled = true; return;
        }
        var error = context.Exception as CollaborationWriteException ?? CollaborationWriteException.FromStorage(context.Exception);
        if (error is null || context.HttpContext.Response.HasStarted) return;
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.Result = new ConflictObjectResult(error.Error);
        context.ExceptionHandled = true;
    }
}
