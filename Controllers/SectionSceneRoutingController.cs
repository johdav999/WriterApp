using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/sections")]
    [Authorize]
    public sealed class SectionSceneRoutingController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;

        public SectionSceneRoutingController(AppDbContext dbContext, IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet("{sectionId:guid}/scene-target")]
        public async Task<ActionResult<ProjectSceneOpenTargetDto>> GetSceneTarget(Guid sectionId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            ProjectNodeRecord? scene = await (
                    from node in _dbContext.ProjectNodes
                    join project in _dbContext.Projects on node.ProjectId equals project.Id
                    where node.NodeType == ProjectNodeType.Scene
                          && node.LinkedSectionId == sectionId
                          && project.OwnerUserId == userId
                    orderby node.UpdatedUtc descending
                    select node)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (scene is null)
            {
                return NotFound();
            }

            Guid? documentId = await _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.Id == sectionId)
                .Select(section => (Guid?)section.DocumentId)
                .FirstOrDefaultAsync(ct);

            return Ok(new ProjectSceneOpenTargetDto(scene.ProjectId, scene.Id, documentId, sectionId, scene.Title));
        }
    }
}
