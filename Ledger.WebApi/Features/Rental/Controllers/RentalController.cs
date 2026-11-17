using System.Net;
using System.Net.Mime;
using Ledger.Library.Validation;
using Ledger.WebApi.Extensions.Mvc;
using Ledger.WebApi.Extensions.Services;
using Ledger.WebApi.Features.Rental.Models.DTOs;
using Ledger.WebApi.Features.Rental.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ledger.WebApi.Features.Rental.Controllers;

/// <summary>
/// The short-term rental module (optional). Only the Website calls this (x-api-key).
/// </summary>
/// <param name="rentalService">Rental business logic.</param>
[Authorize]
[Route("rental")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class RentalController(IRentalService rentalService) : InternalControllerBase
{
    private readonly IRentalService _rentalService = rentalService;

    /// <summary>A month of the rental: revenue, costs, margin, the booked nights and the year so far.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="month">The month (yyyy-MM; empty: this month).</param>
    /// <returns>The month.</returns>
    [HttpGet]
    [ProducesResponseType<RentalDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "RentalGet", OperationId = "RentalGet")]
    public async Task<ActionResult<RentalDTO>> RentalGetAsync(string? userId, string? month = null) =>
        (await _rentalService.GetAsync(userId, month)).ToActionResult();
}
