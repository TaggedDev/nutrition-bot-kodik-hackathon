using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Shared.Dtos;

namespace Nutrition.Web.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/v1/kbju")]
public sealed class NutritionController : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductNutritionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ProductNutritionDto>>> SearchNutritionFactsAsync(
        [FromQuery] string query,
        [FromServices] INutritionFactsLookupService lookupService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query must not be empty.");
        }

        var response = await lookupService.SearchAsync(query, cancellationToken);
        return Ok(response);
    }
}
