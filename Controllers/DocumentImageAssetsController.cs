using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/assets/images")]
    [Authorize]
    public sealed class DocumentImageAssetsController : ControllerBase
    {
        private static readonly IReadOnlyDictionary<string, string> AllowedMimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp"
        };

        private readonly IDocumentRepository _documents;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public DocumentImageAssetsController(
            IDocumentRepository documents,
            IUserIdResolver userIdResolver,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<UploadImageAssetResponse>> Upload(
            Guid documentId,
            [FromForm] IFormFile? file,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (await _documents.GetAsync(documentId, userId, ct) is null)
            {
                return NotFound();
            }

            if (file is null || file.Length <= 0)
            {
                return BadRequest(new { message = "Image file is required." });
            }

            string contentType = file.ContentType?.Trim() ?? string.Empty;
            if (!AllowedMimeTypes.TryGetValue(contentType, out string? extension))
            {
                return BadRequest(new { message = "Unsupported image type. Allowed: PNG, JPEG, GIF, WEBP." });
            }

            long maxBytes = Math.Clamp(_configuration.GetValue<long?>("Images:MaxUploadBytes") ?? (5 * 1024 * 1024), 256 * 1024, 10 * 1024 * 1024);
            if (file.Length > maxBytes)
            {
                return BadRequest(new { message = $"Image is too large. Max allowed is {maxBytes} bytes." });
            }

            await using MemoryStream buffer = new();
            await using (Stream stream = file.OpenReadStream())
            {
                await stream.CopyToAsync(buffer, ct);
            }

            byte[] bytes = buffer.ToArray();
            if (bytes.Length == 0)
            {
                return BadRequest(new { message = "Uploaded image is empty." });
            }

            Guid imageId = Guid.NewGuid();
            string userFolder = ComputeUserFolder(userId);
            string rootPath = ResolveImageRootPath(_environment, _configuration);
            string documentFolder = Path.Combine(rootPath, userFolder, documentId.ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(documentFolder);

            string fileName = $"{imageId:N}{extension}";
            string fullPath = Path.Combine(documentFolder, fileName);
            await System.IO.File.WriteAllBytesAsync(fullPath, bytes, ct);

            string url = $"/api/documents/{documentId:D}/assets/images/{imageId:D}";
            string dataUri = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";

            return Ok(new UploadImageAssetResponse(
                imageId,
                url,
                contentType,
                bytes.Length,
                dataUri));
        }

        [HttpGet("{imageId:guid}")]
        public async Task<IActionResult> Get(
            Guid documentId,
            Guid imageId,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (await _documents.GetAsync(documentId, userId, ct) is null)
            {
                return NotFound();
            }

            string userFolder = ComputeUserFolder(userId);
            string rootPath = ResolveImageRootPath(_environment, _configuration);
            string documentFolder = Path.Combine(rootPath, userFolder, documentId.ToString("N", CultureInfo.InvariantCulture));
            if (!Directory.Exists(documentFolder))
            {
                return NotFound();
            }

            string prefix = imageId.ToString("N", CultureInfo.InvariantCulture);
            string? filePath = Directory.EnumerateFiles(documentFolder, $"{prefix}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (filePath is null)
            {
                return NotFound();
            }

            string extension = Path.GetExtension(filePath);
            string contentType = extension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            byte[] content = await System.IO.File.ReadAllBytesAsync(filePath, ct);
            return File(content, contentType);
        }

        private static string ComputeUserFolder(string userId)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(userId ?? string.Empty);
            byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant();
        }

        private static string ResolveImageRootPath(IWebHostEnvironment environment, IConfiguration configuration)
        {
            string? configured = configuration["Images:StoragePath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.IsPathRooted(configured)
                    ? configured
                    : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
            }

            string? home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrWhiteSpace(home))
            {
                return Path.Combine(home, "data", "writerapp-images");
            }

            return Path.Combine(environment.ContentRootPath, "App_Data", "writerapp-images");
        }

        public sealed record UploadImageAssetResponse(
            Guid ImageId,
            string Url,
            string ContentType,
            int SizeBytes,
            string DataUri);
    }
}
