using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Shared.Dtos;
using Nutrition.Web.Controllers;

namespace Nutrition.Web.Tests;

public sealed class NutritionControllerTests
{
    [Fact]
    public async Task SearchNutritionFactsAsync_ReturnsBadRequest_WhenQueryIsBlank()
    {
        var controller = new NutritionController();
        var service = new RecordingChatQueryService();

        var result = await controller.SearchNutritionFactsAsync("   ", service, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Query must not be empty.", badRequest.Value);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task SearchNutritionFactsAsync_ReturnsOk_WithServiceResult()
    {
        var controller = new NutritionController();
        var expected = new NutritionChatSearchResponseDto { Query = "milk" };
        var service = new RecordingChatQueryService(expected);

        var result = await controller.SearchNutritionFactsAsync("milk", service, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        Assert.Equal(1, service.Calls);
        Assert.Equal("milk", service.LastQuery);
    }

    private sealed class RecordingChatQueryService : INutritionChatQueryService
    {
        private readonly NutritionChatSearchResponseDto _result;

        public RecordingChatQueryService(NutritionChatSearchResponseDto? result = null)
        {
            _result = result ?? new NutritionChatSearchResponseDto { Query = "default" };
        }

        public int Calls { get; private set; }
        public string? LastQuery { get; private set; }

        public Task<NutritionChatSearchResponseDto> SearchAsync(string userInput, CancellationToken cancellationToken)
        {
            Calls++;
            LastQuery = userInput;
            return Task.FromResult(_result);
        }
    }
}