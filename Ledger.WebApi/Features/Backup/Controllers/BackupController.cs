using System.Net;
using System.Net.Mime;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Backup.Models.DTOs;
using Ledger.WebApi.Features.Backup.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Backup.Controllers;

/// <summary>
/// Export, import and start over. Only the Website calls this (x-api-key).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="backupService">Backup business logic.</param>
[Authorize]
[Route("backup")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class BackupController(
    ILogger<BackupController> logger,
    IBackupService backupService) : InternalControllerBase
{
    private readonly ILogger<BackupController> _logger = logger;
    private readonly IBackupService _backupService = backupService;

    /// <summary>Every entry as a CSV file.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The file.</returns>
    [HttpGet("csv")]
    [ProducesResponseType<ExportDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BackupCsv", OperationId = "BackupCsv")]
    public async Task<ActionResult<ExportDTO>> BackupCsvAsync(string? userId) => (await _backupService.CsvAsync(userId)).ToActionResult();

    /// <summary>The whole ledger as a JSON file.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The file.</returns>
    [HttpGet("json")]
    [ProducesResponseType<ExportDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BackupJson", OperationId = "BackupJson")]
    public async Task<ActionResult<ExportDTO>> BackupJsonAsync(string? userId) => (await _backupService.JsonAsync(userId)).ToActionResult();

    /// <summary>Reads a CSV or a JSON backup into the ledger.</summary>
    /// <param name="item">The file, and whether it replaces what is there.</param>
    /// <returns>What was added.</returns>
    [HttpPost("import")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType<ImportResultDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BackupImport", OperationId = "BackupImport")]
    public async Task<ActionResult<ImportResultDTO>> BackupImportAsync(ImportRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BackupImport [UserId={UserId}, Length={Length}, Replace={Replace}]", item.UserId, item.Content?.Length ?? 0, item.Replace);

        return (await _backupService.ImportAsync(item)).ToActionResult();
    }

    /// <summary>Start over: erases the ledger and puts the four starting accounts back.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The user's id.</returns>
    [HttpDelete]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "BackupReset", OperationId = "BackupReset")]
    public async Task<ActionResult<string>> BackupResetAsync(string? userId)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BackupReset [UserId={UserId}]", userId);

        return (await _backupService.ResetAsync(userId)).ToActionResult();
    }
}
