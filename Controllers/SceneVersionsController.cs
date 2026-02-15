using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/scenes/{sceneNodeId:guid}/versions")]
    [Authorize]
    public sealed class SceneVersionsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;

        public SceneVersionsController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SceneVersionListItemDto>>> List(Guid sceneNodeId, CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            if (!await IsOwnedSceneAsync(sceneNodeId, userId, ct))
            {
                return NotFound();
            }

            List<SceneVersionListItemDto> versions = await _dbContext.SceneVersions
                .AsNoTracking()
                .Where(item => item.SceneNodeId == sceneNodeId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => new SceneVersionListItemDto(
                    item.Id,
                    item.SceneNodeId,
                    item.CreatedAt,
                    item.Reason,
                    item.WordCount,
                    item.SizeBytes))
                .ToListAsync(ct);

            return Ok(versions);
        }

        [HttpPost]
        public async Task<ActionResult<SceneVersionListItemDto>> Create(
            Guid sceneNodeId,
            [FromBody] SceneVersionCreateRequest request,
            CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            if (!await IsOwnedSceneAsync(sceneNodeId, userId, ct))
            {
                return NotFound();
            }

            string content = request.ContentJson ?? string.Empty;
            if (string.IsNullOrWhiteSpace(request.ContentJson))
            {
                SceneContentRecord? sceneContent = await _dbContext.SceneContents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId, ct);
                content = sceneContent?.ContentJson ?? string.Empty;
            }

            byte[] compressed = Compress(content);
            string plain = PlainTextMapper.ToPlainText(content);
            SceneVersionRecord version = new()
            {
                Id = Guid.NewGuid(),
                SceneNodeId = sceneNodeId,
                CreatedAt = DateTimeOffset.UtcNow,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "manual" : request.Reason.Trim(),
                ContentCompressed = compressed,
                ContentTextHash = ComputeHash(content),
                SizeBytes = compressed.Length,
                WordCount = CountWords(plain)
            };

            _dbContext.SceneVersions.Add(version);
            await _dbContext.SaveChangesAsync(ct);

            return Ok(new SceneVersionListItemDto(
                version.Id,
                version.SceneNodeId,
                version.CreatedAt,
                version.Reason,
                version.WordCount,
                version.SizeBytes));
        }

        private async Task<bool> IsOwnedSceneAsync(Guid sceneNodeId, string userId, CancellationToken ct)
        {
            return await _dbContext.ProjectNodes
                .Join(
                    _dbContext.Projects,
                    node => node.ProjectId,
                    project => project.Id,
                    (node, project) => new { node, project })
                .AnyAsync(pair =>
                    pair.project.OwnerUserId == userId
                    && pair.node.Id == sceneNodeId
                    && pair.node.NodeType == ProjectNodeType.Scene,
                    ct);
        }

        private static byte[] Compress(string content)
        {
            byte[] input = Encoding.UTF8.GetBytes(content ?? string.Empty);
            using MemoryStream output = new();
            using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(input, 0, input.Length);
            }
            return output.ToArray();
        }

        private static string ComputeHash(string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            int count = 0;
            bool inWord = false;
            foreach (char ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (!inWord)
                    {
                        inWord = true;
                        count++;
                    }
                }
                else
                {
                    inWord = false;
                }
            }

            return count;
        }
    }
}
