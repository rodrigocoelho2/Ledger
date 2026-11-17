using System.Net;
using System.Net.Mime;
using Ledger.Library.Enums;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Entries.Models.DTOs;
using Ledger.WebApi.Features.Entries.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Entries.Controllers;

/// <summary>
/// The lines of the ledger. Only the Website calls this (x-api-key).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="entryService">Entry business logic.</param>
[Authorize]
[Route("entry")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class EntryController(
    ILogger<EntryController> logger,
    IEntryService entryService) : InternalControllerBase
{
    private readonly ILogger<EntryController> _logger = logger;
    private readonly IEntryService _entryService = entryService;

    /// <summary>The entries of a month.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <param name="scope">The part of life (empty: all).</param>
    /// <param name="category">A category key, or "transfers" (empty: all).</param>
    /// <returns>The entries.</returns>
    [HttpGet("month")]
    [ProducesResponseType<EntryMonthDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "EntryMonth", OperationId = "EntryMonth")]
    public async Task<ActionResult<EntryMonthDTO>> EntryMonthAsync(string? userId, string? month = null, EntryScope? scope = null, string? category = null) =>
        (await _entryService.MonthAsync(userId, month, scope, category)).ToActionResult();

    /// <summary>Gets one entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The entry.</param>
    /// <returns>The entry.</returns>
    [HttpGet]
    [ProducesResponseType<EntryDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "EntryGet", OperationId = "EntryGet")]
    public async Task<ActionResult<EntryDTO>> EntryGetAsync(string? userId, string? identifier) =>
        (await _entryService.GetAsync(userId, identifier)).ToActionResult();

    /// <summary>Reads a quick-add line ("9.80 lunch") and fills in the rest.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="text">The line.</param>
    /// <returns>What was understood.</returns>
    [HttpGet("quick")]
    [ProducesResponseType<QuickAddDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "EntryQuick", OperationId = "EntryQuick")]
    public async Task<ActionResult<QuickAddDTO>> EntryQuickAsync(string? userId, string? text) =>
        (await _entryService.QuickAsync(userId, text)).ToActionResult();

    /// <summary>Adds an entry, or changes one.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The entry.</returns>
    [HttpPost]
    [ProducesResponseType<EntryDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "EntrySave", OperationId = "EntrySave")]
    public async Task<ActionResult<EntryDTO>> EntrySaveAsync(EntrySaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("EntrySave [UserId={UserId}, Id={Id}, Kind={Kind}, Scope={Scope}]", item.UserId, item.Id, item.Kind, item.Scope);

        return (await _entryService.SaveAsync(item)).ToActionResult();
    }

    /// <summary>Deletes an entry.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The entry.</param>
    /// <returns>The deleted id.</returns>
    [HttpDelete]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "EntryDelete", OperationId = "EntryDelete")]
    public async Task<ActionResult<string>> EntryDeleteAsync(string? userId, string? identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("EntryDelete [UserId={UserId}, Identifier={Identifier}]", userId, identifier);

        return (await _entryService.DeleteAsync(userId, identifier)).ToActionResult();
    }
}
