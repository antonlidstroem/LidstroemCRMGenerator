using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Donations.DTOs;
using Lidstroem.Plugins.Donations.Entities;
using Lidstroem.Shared.Attributes;
using Lidstroem.Shared.Controllers.Base;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Donations.Controllers;

[Route("api/donations")]
[ApiController]
[Authorize]
public class DonationsController : BaseLidstroemController<Donation>
{
    public DonationsController(
        DbContext context,
        IEnumerable<IEntityExtensionProvider> extenders,
        IPublisher publisher,
        IRealtimeNotifier realtime,
        ITenantContext tenantContext)
        : base(context, extenders, publisher, realtime, tenantContext) { }

    protected override Task MapDtoToEntity(object dto, Donation entity)
    {
        var d = (DonationDto)dto;
        entity.Amount = d.Amount;
        entity.Currency = d.Currency;
        // BUG FIX #17: DonationDate was present in DonationDto and listed in the schema
        // as a required field, but was never assigned here. Every donation silently used
        // the entity's default (DateTime.UtcNow at construction time) regardless of what
        // the client submitted, making backdated donations impossible.
        entity.DonationDate = d.DonationDate ?? entity.DonationDate;
        entity.DonorId = d.DonorId;
        entity.DonorType = d.DonorType;
        entity.TargetId = d.TargetId;
        entity.TargetType = d.TargetType;
        return Task.CompletedTask;
    }

    [HttpGet]
    [RequirePermission("Donations.View")]
    public async Task<ActionResult<IEnumerable<Donation>>> GetDonations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        return Ok(await _context.Set<Donation>()
            .OrderByDescending(d => d.DonationDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync());
    }

    [HttpGet("{id}")]
    [RequirePermission("Donations.View")]
    public async Task<ActionResult<object>> GetDonation(int id)
    {
        var donation = await _context.Set<Donation>().FindAsync(id);
        if (donation == null) return NotFound();
        return Ok(await OkWithExtensions(donation, id));
    }

    [HttpPost]
    [RequirePermission("Donations.Create")]
    public async Task<ActionResult<Donation>> PostDonation(DonationDto dto) =>
        await PostGeneric(dto);

    [HttpPut("{id}")]
    [RequirePermission("Donations.Edit")]
    public async Task<IActionResult> PutDonation(int id, DonationDto dto)
    {
        if (id != dto.Id) return BadRequest();
        return await PutGeneric(id, dto);
    }

    [HttpDelete("{id}")]
    [RequirePermission("Donations.Delete")]
    public async Task<IActionResult> DeleteDonation(int id) =>
        await DeleteGeneric(id);
}
