using System.Net;
using System.Net.Mime;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Accounts.Models.DTOs;
using Ledger.WebApi.Features.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Accounts.Controllers;

/// <summary>
/// The accounts: where the money sits. Only the Website calls this (x-api-key).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="accountService">Account business logic.</param>
[Authorize]
[Route("account")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class AccountController(
    ILogger<AccountController> logger,
    IAccountService accountService) : InternalControllerBase
{
    private readonly ILogger<AccountController> _logger = logger;
    private readonly IAccountService _accountService = accountService;

    /// <summary>Lists a user's accounts with their balances.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The accounts.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<AccountDTO>>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "AccountList", OperationId = "AccountList")]
    public async Task<ActionResult<List<AccountDTO>>> AccountListAsync(string? userId) => (await _accountService.ListAsync(userId)).ToActionResult();

    /// <summary>Adds an account, or changes one.</summary>
    /// <param name="item">The account.</param>
    /// <returns>The account.</returns>
    [HttpPost]
    [ProducesResponseType<AccountDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "AccountSave", OperationId = "AccountSave")]
    public async Task<ActionResult<AccountDTO>> AccountSaveAsync(AccountSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("AccountSave [UserId={UserId}, Id={Id}, Kind={Kind}]", item.UserId, item.Id, item.Kind);

        return (await _accountService.SaveAsync(item)).ToActionResult();
    }

    /// <summary>Keeps the order the accounts were dragged into.</summary>
    /// <param name="item">The order.</param>
    /// <returns>The accounts in their new order.</returns>
    [HttpPost("order")]
    [ProducesResponseType<List<AccountDTO>>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "AccountOrder", OperationId = "AccountOrder")]
    public async Task<ActionResult<List<AccountDTO>>> AccountOrderAsync(AccountOrderRequest item) => (await _accountService.ReorderAsync(item)).ToActionResult();

    /// <summary>Deletes an account; its entries stay in the ledger.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The account.</param>
    /// <returns>The deleted id.</returns>
    [HttpDelete]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "AccountDelete", OperationId = "AccountDelete")]
    public async Task<ActionResult<string>> AccountDeleteAsync(string? userId, string? identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("AccountDelete [UserId={UserId}, Identifier={Identifier}]", userId, identifier);

        return (await _accountService.DeleteAsync(userId, identifier)).ToActionResult();
    }
}
