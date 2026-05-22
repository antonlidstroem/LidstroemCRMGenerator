using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Resources.DTOs;
using Lidstroem.Plugins.Resources.Entities;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Resources.Controllers;

[Route("api/resources")]
[ApiController]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IStorageProvider _storage;

    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "application/pdf", "text/plain", "text/csv",
        "audio/mpeg", "audio/wav", "audio/ogg"
    };

    // FIX #12: Magic byte signatures for reliable MIME detection.
    // The client-supplied Content-Type header cannot be trusted.
    private static readonly Dictionary<string, byte[][]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"]      = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        ["image/png"]       = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        ["image/gif"]       = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        ["image/webp"]      = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // RIFF header
        ["application/pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }, // %PDF
        ["audio/mpeg"]      = new[] { new byte[] { 0xFF, 0xFB }, new byte[] { 0xFF, 0xF3 }, new byte[] { 0x49, 0x44, 0x33 } },
        ["audio/wav"]       = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // RIFF header
        ["audio/ogg"]       = new[] { new byte[] { 0x4F, 0x67, 0x67, 0x53 } }, // OggS
        // text/plain and text/csv have no reliable magic; accepted on Content-Type alone with size cap
    };

    public ResourcesController(DbContext context, IStorageProvider storage)
    {
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    [RequirePermission("Resources.View")]
    public async Task<ActionResult<IEnumerable<ResourceResponseDto>>> GetResources(
        [FromQuery] string targetType, [FromQuery] int targetId)
    {
        var resources = await _context.Set<Resource>()
            .Where(r => r.TargetType == targetType && r.TargetId == targetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var dtos = new List<ResourceResponseDto>();
        foreach (var r in resources) dtos.Add(await ToDto(r));
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    [RequirePermission("Resources.View")]
    public async Task<ActionResult<ResourceResponseDto>> GetResource(int id)
    {
        var resource = await _context.Set<Resource>().FindAsync(id);
        return resource == null ? NotFound() : Ok(await ToDto(resource));
    }

    [HttpPost("upload")]
    [RequirePermission("Resources.Upload")]
    public async Task<ActionResult<ResourceResponseDto>> Upload(
        IFormFile file,
        [FromForm] string targetType,
        [FromForm] int targetId,
        [FromForm] string title,
        [FromForm] string? description)
    {
        if (file.Length == 0) return BadRequest("File is empty.");
        if (file.Length > MaxFileSizeBytes) return BadRequest("File exceeds 50 MB limit.");
        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest($"Content type '{file.ContentType}' is not allowed.");

        // FIX #12: Read magic bytes and verify they match the declared Content-Type.
        // This prevents an attacker from uploading an executable/HTML while claiming "image/jpeg".
        await using var stream = file.OpenReadStream();
        if (MagicBytes.TryGetValue(file.ContentType, out var signatures))
        {
            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
            stream.Seek(0, SeekOrigin.Begin);

            var matched = signatures.Any(sig =>
                bytesRead >= sig.Length && header.Take(sig.Length).SequenceEqual(sig));

            if (!matched)
                return BadRequest($"File content does not match the declared type '{file.ContentType}'.");
        }

        var resourceType = file.ContentType.StartsWith("image/") ? ResourceType.Image
                         : file.ContentType.StartsWith("audio/") ? ResourceType.Audio
                         : ResourceType.Document;

        // stream is already open and seeked to position 0 from the magic byte check above
        var storagePath = await _storage.StoreAsync(stream, file.FileName, file.ContentType);

        var resource = new Resource
        {
            Title = title,
            Description = description,
            Type = resourceType,
            TargetId = targetId,
            TargetType = targetType,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow
        };

        _context.Set<Resource>().Add(resource);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetResource), new { id = resource.Id }, await ToDto(resource));
    }

    [HttpPost("link")]
    [RequirePermission("Resources.Upload")]
    public async Task<ActionResult<ResourceResponseDto>> AddLink(CreateLinkResourceDto dto)
    {
        if (!Uri.TryCreate(dto.ExternalUrl, UriKind.Absolute, out _))
            return BadRequest("Invalid URL.");

        var resource = new Resource
        {
            Title = dto.Title,
            Description = dto.Description,
            Type = ResourceType.Link,
            TargetId = dto.TargetId,
            TargetType = dto.TargetType,
            ExternalUrl = dto.ExternalUrl,
            UploadedAt = DateTime.UtcNow
        };

        _context.Set<Resource>().Add(resource);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetResource), new { id = resource.Id }, await ToDto(resource));
    }

    [HttpGet("{id}/download")]
    [RequirePermission("Resources.View")]
    public async Task<IActionResult> Download(int id)
    {
        var resource = await _context.Set<Resource>().FindAsync(id);
        if (resource == null) return NotFound();
        if (resource.StoragePath == null) return BadRequest("This resource is a link, not a file.");

        var stream = await _storage.RetrieveAsync(resource.StoragePath);
        return File(stream, resource.ContentType ?? "application/octet-stream",
            resource.OriginalFileName ?? "download");
    }

    [HttpDelete("{id}")]
    [RequirePermission("Resources.Delete")]
    public async Task<IActionResult> DeleteResource(int id)
    {
        var resource = await _context.Set<Resource>().FindAsync(id);
        if (resource == null) return NotFound();
        if (resource.StoragePath != null) await _storage.DeleteAsync(resource.StoragePath);
        _context.Set<Resource>().Remove(resource);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ResourceResponseDto> ToDto(Resource r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Description = r.Description,
        Type = r.Type,
        TargetId = r.TargetId,
        TargetType = r.TargetType,
        OriginalFileName = r.OriginalFileName,
        ContentType = r.ContentType,
        FileSizeBytes = r.FileSizeBytes,
        DownloadUrl = r.StoragePath != null ? Url.Action(nameof(Download), new { id = r.Id }) : null,
        ExternalUrl = r.ExternalUrl,
        PublicUrl = r.StoragePath != null ? await _storage.GetPublicUrlAsync(r.StoragePath) : null,
        UploadedAt = r.UploadedAt
    };
}
