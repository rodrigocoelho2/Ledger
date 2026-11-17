using System.Net;
using System.Net.Mime;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Projects.Models.DTOs;
using Ledger.WebApi.Features.Projects.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Projects.Controllers;

/// <summary>
/// The buy-and-sell module (optional). Only the Website calls this (x-api-key).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="projectService">Project business logic.</param>
[Authorize]
[Route("project")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class ProjectController(
    ILogger<ProjectController> logger,
    IProjectService projectService) : InternalControllerBase
{
    private readonly ILogger<ProjectController> _logger = logger;
    private readonly IProjectService _projectService = projectService;

    /// <summary>Every project, worked out from its entries, and what they add up to.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The projects.</returns>
    [HttpGet("list")]
    [ProducesResponseType<ProjectsDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "ProjectList", OperationId = "ProjectList")]
    public async Task<ActionResult<ProjectsDTO>> ProjectListAsync(string? userId) => (await _projectService.ListAsync(userId)).ToActionResult();

    /// <summary>Adds a project, or changes one.</summary>
    /// <param name="item">The project.</param>
    /// <returns>The project's id.</returns>
    [HttpPost]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "ProjectSave", OperationId = "ProjectSave")]
    public async Task<ActionResult<string>> ProjectSaveAsync(ProjectSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("ProjectSave [UserId={UserId}, Id={Id}]", item.UserId, item.Id);

        return (await _projectService.SaveAsync(item)).ToActionResult();
    }

    /// <summary>Deletes a project; its entries stay in the ledger.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="identifier">The project.</param>
    /// <returns>The deleted id.</returns>
    [HttpDelete]
    [ProducesResponseType<string>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "ProjectDelete", OperationId = "ProjectDelete")]
    public async Task<ActionResult<string>> ProjectDeleteAsync(string? userId, string? identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("ProjectDelete [UserId={UserId}, Identifier={Identifier}]", userId, identifier);

        return (await _projectService.DeleteAsync(userId, identifier)).ToActionResult();
    }
}
