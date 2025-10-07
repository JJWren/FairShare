using FairShare.Interfaces;
using FairShare.Models;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.Controllers;

[Route("api/parents")]
[ApiController]
public class ParentsController(IParentProfileService service, ILogger<ParentsController> logger) : ControllerBase
{
    private readonly IParentProfileService _service = service;
    private readonly ILogger<ParentsController> _logger = logger;

    public record ParentCreateRequest(
        string? DisplayName,
        int MonthlyGrossIncome,
        int PreexistingChildSupport,
        int PreexistingAlimony,
        int WorkRelatedChildcareCosts,
        int HealthcareCoverageCosts,
        bool HasPrimaryCustody,
        bool Deduplicate = true);

    public record ParentUpdateRequest(
        string DisplayName,
        int MonthlyGrossIncome,
        int PreexistingChildSupport,
        int PreexistingAlimony,
        int WorkRelatedChildcareCosts,
        int HealthcareCoverageCosts,
        bool HasPrimaryCustody,
        byte[]? RowVersion);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken ct)
        => Ok((await _service.ListAsync(q, ct)).Select(p => new
        {
            p.Id,
            p.DisplayName,
            p.MonthlyGrossIncome,
            p.PreexistingChildSupport,
            p.PreexistingAlimony,
            p.WorkRelatedChildcareCosts,
            p.HealthcareCoverageCosts,
            p.HasPrimaryCustody
        }));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        ParentProfile? p = await _service.GetAsync(id, ct);

        if (p is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            p.Id,
            p.DisplayName,
            p.MonthlyGrossIncome,
            p.PreexistingChildSupport,
            p.PreexistingAlimony,
            p.WorkRelatedChildcareCosts,
            p.HealthcareCoverageCosts,
            p.HasPrimaryCustody,
            p.RowVersion
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ParentCreateRequest request, CancellationToken ct)
    {
        ParentData data = new()
        {
            MonthlyGrossIncome = request.MonthlyGrossIncome,
            PreexistingChildSupport = request.PreexistingChildSupport,
            PreexistingAlimony = request.PreexistingAlimony,
            WorkRelatedChildcareCosts = request.WorkRelatedChildcareCosts,
            HealthcareCoverageCosts = request.HealthcareCoverageCosts,
            HasPrimaryCustody = request.HasPrimaryCustody
        };

        ParentProfile profile = request.Deduplicate
            ? await _service.GetOrCreateAsync(data, request.DisplayName, ct)
            : await _service.CreateAsync(new ParentProfile
            {
                Id = Guid.NewGuid(),
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? $"Parent {DateTime.UtcNow:yyyyMMdd-HHmmss}"
                    : request.DisplayName.Trim(),
                MonthlyGrossIncome = data.MonthlyGrossIncome,
                PreexistingChildSupport = data.PreexistingChildSupport,
                PreexistingAlimony = data.PreexistingAlimony,
                WorkRelatedChildcareCosts = data.WorkRelatedChildcareCosts,
                HealthcareCoverageCosts = data.HealthcareCoverageCosts,
                HasPrimaryCustody = data.HasPrimaryCustody
            }, ct);

        return CreatedAtAction(nameof(Get), new { id = profile.Id }, new { profile.Id, profile.DisplayName });
    }

    /// <summary>
    /// Creates multiple parent profiles in a single request (up to 10).
    /// </summary>
    /// <param name="requests">The requests to create parents.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The results of the creation.</returns>
    [HttpPost("batch")]
    public async Task<IActionResult> CreateBatch([FromBody] ParentCreateRequest[] requests, CancellationToken ct)
    {
        if (requests.Length == 0 || requests.Length > 10)
        {
            return BadRequest("Provide 1-10 items.");
        }

        List<object> results = [];

        foreach (ParentCreateRequest r in requests)
        {
            ParentData data = new()
            {
                MonthlyGrossIncome = r.MonthlyGrossIncome,
                PreexistingChildSupport = r.PreexistingChildSupport,
                PreexistingAlimony = r.PreexistingAlimony,
                WorkRelatedChildcareCosts = r.WorkRelatedChildcareCosts,
                HealthcareCoverageCosts = r.HealthcareCoverageCosts,
                HasPrimaryCustody = r.HasPrimaryCustody
            };
            ParentProfile p = r.Deduplicate
                ? await _service.GetOrCreateAsync(data, r.DisplayName, ct)
                : await _service.CreateAsync(new ParentProfile
                {
                    Id = Guid.NewGuid(),
                    DisplayName = string.IsNullOrWhiteSpace(r.DisplayName)
                        ? $"Parent {DateTime.UtcNow:yyyyMMdd-HHmmss}"
                        : r.DisplayName.Trim(),
                    MonthlyGrossIncome = data.MonthlyGrossIncome,
                    PreexistingChildSupport = data.PreexistingChildSupport,
                    PreexistingAlimony = data.PreexistingAlimony,
                    WorkRelatedChildcareCosts = data.WorkRelatedChildcareCosts,
                    HealthcareCoverageCosts = data.HealthcareCoverageCosts,
                    HasPrimaryCustody = data.HasPrimaryCustody
                }, ct);

            results.Add(new { p.Id, p.DisplayName });
        }

        return Ok(results);
    }

    /// <summary>
    /// Updates an existing parent profile.
    /// </summary>
    /// <param name="id">The parent identifier.</param>
    /// <param name="request">The request details to update the parent with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The results of the update.</returns>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ParentUpdateRequest request, CancellationToken ct)
    {
        ParentProfile? existing = await _service.GetAsync(id, ct);

        if (existing is null)
        {
            return NotFound();
        }

        existing.DisplayName = request.DisplayName.Trim();
        existing.MonthlyGrossIncome = request.MonthlyGrossIncome;
        existing.PreexistingChildSupport = request.PreexistingChildSupport;
        existing.PreexistingAlimony = request.PreexistingAlimony;
        existing.WorkRelatedChildcareCosts = request.WorkRelatedChildcareCosts;
        existing.HealthcareCoverageCosts = request.HealthcareCoverageCosts;
        existing.HasPrimaryCustody = request.HasPrimaryCustody;

        // Concurrency: if RowVersion supplied, compare
        if (request.RowVersion is not null && existing.RowVersion is not null &&
            !existing.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Conflict("Concurrency conflict. Reload and retry.");
        }

        bool ok = await _service.UpdateAsync(existing, ct);

        if (!ok)
        {
            return Conflict("Update failed (possibly concurrency).");
        }

        return NoContent();
    }

    /// <summary>
    /// Archives the resource identified by the specified ID.
    /// </summary>
    /// <remarks>This operation is idempotent. If the resource is already archived or does not exist,  the
    /// method will return <see cref="NotFoundResult"/>.</remarks>
    /// <param name="id">The unique identifier of the resource to archive.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="NoContentResult"/> if the resource was successfully archived;  otherwise, a <see
    /// cref="NotFoundResult"/> if the resource could not be found.</returns>
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        bool ok = await _service.ArchiveAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
