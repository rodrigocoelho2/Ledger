using System.Net;
using System.Net.Mime;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Settings.Models.DTOs;
using Ledger.WebApi.Features.Settings.Services;
using Ledger.WebApi.Features.Users.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Settings.Controllers;

/// <summary>
/// The language and the optional modules. Only the Website calls this (x-api-key).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="settingService">Settings business logic.</param>
[Authorize]
[Route("setting")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class SettingController(
    ILogger<SettingController> logger,
    ISettingService settingService) : InternalControllerBase
{
    private readonly ILogger<SettingController> _logger = logger;
    private readonly ISettingService _settingService = settingService;

    /// <summary>Keeps the language and which modules are on.</summary>
    /// <param name="item">The choices.</param>
    /// <returns>The user, with the choices as they are now.</returns>
    [HttpPost]
    [ProducesResponseType<UserDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "SettingSave", OperationId = "SettingSave")]
    public async Task<ActionResult<UserDTO>> SettingSaveAsync(SettingSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "SettingSave [UserId={UserId}, Language={Language}, Rental={Rental}, Flips={Flips}]",
                item.UserId, item.Language, item.RentalEnabled, item.FlipsEnabled);

        return (await _settingService.SaveAsync(item)).ToActionResult();
    }
}
