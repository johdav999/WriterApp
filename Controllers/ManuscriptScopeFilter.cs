using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers;

// Every tree operation, including legacy routes, selects exactly one manuscript.
public sealed class ManuscriptScopeFilter(AppDbContext db, IUserIdResolver users) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.ActionDescriptor.RouteValues.TryGetValue("action", out var action);
        if (!context.ActionArguments.TryGetValue("projectId", out var value) || value is not Guid projectId
            || !(context.HttpContext.Request.Path.Value?.Contains("/nodes", StringComparison.OrdinalIgnoreCase) == true
                || action is "GetTree" or "GetStats" or "GetIntegrityReport"))
        { await next(); return; }
        var ct = context.HttpContext.RequestAborted;
        var owner = users.ResolveUserId(context.HttpContext.User);
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId && p.OwnerUserId == owner, ct);
        if (project is null) { context.Result = new NotFoundResult(); return; }
        Guid? primary = await ManuscriptOwnership.ResolvePrimaryAsync(db, project, ct);
        var query = context.HttpContext.Request.Query["documentId"].ToString();
        Guid? requested = primary;
        if (query.Length == 0 && context.ActionArguments.TryGetValue("nodeId", out var nodeValue) && nodeValue is Guid nodeId)
            requested = await db.ProjectNodes.IgnoreQueryFilters().Where(n => n.Id == nodeId && n.ProjectId == projectId)
                .Select(n => n.DocumentId).SingleOrDefaultAsync(ct) ?? primary;
        if (query.Length > 0)
        {
            if (!Guid.TryParse(query, out var id) || id == Guid.Empty)
            { context.Result = new BadRequestObjectResult(new { message = "Invalid manuscript identity." }); return; }
            requested = id;
        }
        if (requested is null)
        {
            if (await db.Documents.AnyAsync(d => d.ProjectId == projectId && d.DocumentKind == DocumentKind.Manuscript, ct))
            { context.Result = new NotFoundObjectResult(new { message = "Restore a manuscript before opening its structure." }); return; }
            // Legacy empty projects obtain their initial manuscript through existing creation logic.
            var linking = context.HttpContext.RequestServices.GetRequiredService<IProjectSceneLinkingService>();
            requested = (await linking.GetOrCreateManuscriptDocumentAsync(project, owner, ct))?.Id;
            await db.SaveChangesAsync(ct);
        }
        if (!await db.Documents.AnyAsync(d => d.Id == requested && d.ProjectId == projectId
            && d.OwnerUserId == owner && d.DocumentKind == DocumentKind.Manuscript && d.DeletedAtUtc == null, ct))
        { context.Result = new NotFoundObjectResult(new { message = "Manuscript unavailable in this project." }); return; }
        db.ManuscriptScopeId = requested;
        try { await next(); }
        finally { db.ManuscriptScopeId = null; }
    }
}
