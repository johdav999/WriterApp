using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/admin/scene-content-backfill")]
    [Authorize(Policy = "AdminOnly")]
    public sealed class AdminSceneContentBackfillController : ControllerBase
    {
        private readonly ISceneContentBackfillService _backfill;
        private readonly IConfiguration _configuration;

        public AdminSceneContentBackfillController(
            ISceneContentBackfillService backfill,
            IConfiguration configuration)
        {
            _backfill = backfill ?? throw new ArgumentNullException(nameof(backfill));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost("run")]
        public async Task<ActionResult<SceneContentBackfillResult>> Run(CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            SceneContentBackfillResult result = await _backfill.BackfillAsync(ct);
            return Ok(result);
        }

        private bool IsEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:SceneContentBackfillAdminEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:SceneContentBackfillAdminEnabled")
                ?? false;
        }
    }
}
