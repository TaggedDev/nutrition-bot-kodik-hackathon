using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Abstractions.UseCases;
using Nutrition.Shared.Dtos;

namespace Nutrition.Web.Controllers;

[ApiController]
[Route("api/v1/kbju")]
public sealed class KbjuController : ControllerBase
{
    [HttpGet("meals/{mealEntryId:guid}")]
    public async Task<ActionResult<GetMealKbjuResponseDto>> GetMealKbjuAsync(
        Guid mealEntryId,
        [FromQuery] Guid userId,
        [FromServices] IGetMealKbjuUseCase useCase,
        CancellationToken cancellationToken)
    {
        var response = await useCase.ExecuteAsync(
            new GetMealKbjuRequestDto
            {
                UserId = userId,
                MealEntryId = mealEntryId
            },
            cancellationToken);

        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }

    [HttpPut("meals/{mealEntryId:guid}")]
    public async Task<ActionResult<UpdateMealKbjuResponseDto>> UpdateMealKbjuAsync(
        Guid mealEntryId,
        [FromBody] UpdateMealKbjuRequestDto request,
        [FromServices] IUpdateMealKbjuUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (mealEntryId != request.MealEntryId)
        {
            return BadRequest("Route mealEntryId must match request mealEntryId.");
        }

        var response = await useCase.ExecuteAsync(request, cancellationToken);
        if (response is null)
        {
            return BadRequest("Invalid request payload.");
        }

        return Ok(response);
    }
}
