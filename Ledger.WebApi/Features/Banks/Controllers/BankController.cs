using System.Net;
using System.Net.Mime;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Banks.Models.DTOs;
using Ledger.WebApi.Features.Banks.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Banks.Controllers;

/// <summary>
/// Bank connections through Enable Banking (open banking, read only). Only the Website calls this (x-api-key).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="bankService">Bank business logic.</param>
[Authorize]
[Route("bank")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class BankController(
    ILogger<BankController> logger,
    IBankService bankService) : InternalControllerBase
{
    private readonly ILogger<BankController> _logger = logger;
    private readonly IBankService _bankService = bankService;

    /// <summary>The person's bank connections.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The status.</returns>
    [HttpGet("status")]
    [ProducesResponseType<BankStatusDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BankStatus", OperationId = "BankStatus")]
    public async Task<ActionResult<BankStatusDTO>> BankStatusAsync(string? userId) => (await _bankService.StatusAsync(userId)).ToActionResult();

    /// <summary>The banks of a country.</summary>
    /// <param name="country">The country (default PT).</param>
    /// <returns>The banks.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<BankChoiceDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "BankList", OperationId = "BankList")]
    public async Task<ActionResult<List<BankChoiceDTO>>> BankListAsync(string? country = null) => (await _bankService.BanksAsync(country)).ToActionResult();

    /// <summary>Starts connecting a bank.</summary>
    /// <param name="item">The bank.</param>
    /// <returns>Where to send the person.</returns>
    [HttpPost("start")]
    [ProducesResponseType<BankStartDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BankStart", OperationId = "BankStart")]
    public async Task<ActionResult<BankStartDTO>> BankStartAsync(BankStartRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BankStart [UserId={UserId}, Bank={Bank}]", item.UserId, item.Bank);

        return (await _bankService.StartAsync(item)).ToActionResult();
    }

    /// <summary>Finishes connecting: keeps the accounts the person authorised.</summary>
    /// <param name="item">The code and state.</param>
    /// <returns>The status.</returns>
    [HttpPost("complete")]
    [ProducesResponseType<BankStatusDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BankComplete", OperationId = "BankComplete")]
    public async Task<ActionResult<BankStatusDTO>> BankCompleteAsync(BankCompleteRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BankComplete [UserId={UserId}]", item.UserId);

        return (await _bankService.CompleteAsync(item)).ToActionResult();
    }

    /// <summary>Says which Ledger account a bank account goes into, and brings its transactions.</summary>
    /// <param name="item">The link and the account.</param>
    /// <returns>What the first sync did.</returns>
    [HttpPost("assign")]
    [ProducesResponseType<BankSyncDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "BankAssign", OperationId = "BankAssign")]
    public async Task<ActionResult<BankSyncDTO>> BankAssignAsync(BankAssignRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BankAssign [UserId={UserId}, LinkId={LinkId}, AccountId={AccountId}]", item.UserId, item.LinkId, item.AccountId);

        return (await _bankService.AssignAsync(item)).ToActionResult();
    }

    /// <summary>Brings a connected account's latest transactions now.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The link.</param>
    /// <returns>What the sync did.</returns>
    [HttpPost("sync")]
    [ProducesResponseType<BankSyncDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "BankSync", OperationId = "BankSync")]
    public async Task<ActionResult<BankSyncDTO>> BankSyncAsync(string? userId, string? identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BankSync [UserId={UserId}, Identifier={Identifier}]", userId, identifier);

        return (await _bankService.SyncAsync(userId, identifier)).ToActionResult();
    }

    /// <summary>Makes Ledger's balance agree with the bank's by changing the opening balance.</summary>
    /// <param name="item">The link.</param>
    /// <returns>The link.</returns>
    [HttpPost("align")]
    [ProducesResponseType<BankLinkDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "BankAlign", OperationId = "BankAlign")]
    public async Task<ActionResult<BankLinkDTO>> BankAlignAsync(BankAlignRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BankAlign [UserId={UserId}, LinkId={LinkId}]", item.UserId, item.LinkId);

        return (await _bankService.AlignAsync(item)).ToActionResult();
    }

    /// <summary>Disconnects a bank account (its entries stay).</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The link.</param>
    /// <returns>The deleted id.</returns>
    [HttpDelete]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BankDelete", OperationId = "BankDelete")]
    public async Task<ActionResult<string>> BankDeleteAsync(string? userId, string? identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BankDelete [UserId={UserId}, Identifier={Identifier}]", userId, identifier);

        return (await _bankService.DeleteAsync(userId, identifier)).ToActionResult();
    }
}
