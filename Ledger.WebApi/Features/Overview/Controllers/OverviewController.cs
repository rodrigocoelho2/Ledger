using System.Net;
using System.Net.Mime;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Overview.Models.DTOs;
using Ledger.WebApi.Features.Overview.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Overview.Controllers;

/// <summary>
/// The overview of a month and its spending cap. Only the Website calls this (x-api-key).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="overviewService">Overview business logic.</param>
[Authorize]
[Route("overview")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class OverviewController(
    ILogger<OverviewController> logger,
    IOverviewService overviewService) : InternalControllerBase
{
    private readonly ILogger<OverviewController> _logger = logger;
    private readonly IOverviewService _overviewService = overviewService;

    /// <summary>The overview of a month of the person's own money.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <returns>The overview.</returns>
    [HttpGet]
    [ProducesResponseType<OverviewDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "OverviewGet", OperationId = "OverviewGet")]
    public async Task<ActionResult<OverviewDTO>> OverviewGetAsync(string? userId, string? month = null) =>
        (await _overviewService.GetAsync(userId, month)).ToActionResult();

    /// <summary>Sets the spending cap of a month, or removes it.</summary>
    /// <param name="item">The month and the cap.</param>
    /// <returns>The month.</returns>
    [HttpPost("cap")]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "OverviewCapSave", OperationId = "OverviewCapSave")]
    public async Task<ActionResult<string>> OverviewCapSaveAsync(CapSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("OverviewCapSave [UserId={UserId}, Month={Month}]", item.UserId, item.Month);

        return (await _overviewService.SaveCapAsync(item)).ToActionResult();
    }
}
