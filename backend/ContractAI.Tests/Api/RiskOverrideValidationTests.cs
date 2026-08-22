using ContractAI.API.Auth;
using ContractAI.API.Contracts;
using ContractAI.API.Controllers;
using ContractAI.Core.Enums;
using ContractAI.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ContractAI.Tests.Api;

// OverrideRisk validates the request against ModelState and short-circuits with a
// ValidationProblem before it ever touches the database. These tests exercise only
// that gate: the DbContext is built from bare options with no provider, so any code
// path that reaches a query throws — which is exactly how "validation passed, we
// proceeded" is told apart from "validation rejected the request".
public class RiskOverrideValidationTests
{
    private const string SeverityKey = "severity";
    private const string ExplanationKey = "explanation";

    [Fact]
    public async Task OverrideRisk_UnknownSeverity_ReturnsValidationProblemWithSeverityError()
    {
        var controller = CreateController();
        var request = new RiskOverrideRequest(RiskLevel.Unknown, "Reviewed by counsel.");

        var result = await controller.OverrideRisk(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(SeverityKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OverrideRisk_MissingExplanation_ReturnsValidationProblemWithExplanationError(string? explanation)
    {
        var controller = CreateController();
        var request = new RiskOverrideRequest(RiskLevel.High, explanation!);

        var result = await controller.OverrideRisk(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(ExplanationKey));
    }

    [Fact]
    public async Task OverrideRisk_ExplanationOverLimit_ReturnsValidationProblemWithExplanationError()
    {
        var controller = CreateController();
        var request = new RiskOverrideRequest(
            RiskLevel.High,
            new string('x', RiskOverrideRequest.MaxExplanationLength + 1));

        var result = await controller.OverrideRisk(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(ExplanationKey));
    }

    // The ceiling is inclusive, so an explanation of exactly MaxExplanationLength must
    // pass the gate. Passing is observable as the method proceeding to the data layer,
    // where the providerless context throws; a rejected request would have returned a
    // ValidationProblem without ever querying.
    [Fact]
    public async Task OverrideRisk_ExplanationAtLimit_PassesValidationAndReachesDataLayer()
    {
        var controller = CreateController();
        var request = new RiskOverrideRequest(
            RiskLevel.High,
            new string('x', RiskOverrideRequest.MaxExplanationLength));

        var exception = await Record.ExceptionAsync(
            () => controller.OverrideRisk(Guid.NewGuid(), request, CancellationToken.None));

        Assert.NotNull(exception);
        Assert.True(controller.ModelState.IsValid);
    }

    private static ClausesController CreateController()
    {
        var options = new DbContextOptionsBuilder<ContractDbContext>().Options;
        var db = new ContractDbContext(options);

        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        tenant.SetupGet(t => t.UserId).Returns((Guid?)null);

        return new ClausesController(db, tenant.Object)
        {
            ControllerContext = new ControllerContext(),
            ProblemDetailsFactory = new StubProblemDetailsFactory(),
        };
    }

    // ValidationProblem(ModelState) delegates to ProblemDetailsFactory, which is null
    // outside the DI container. This stub stamps Status 400 so ControllerBase returns a
    // BadRequestObjectResult, matching the runtime shape without needing a host.
    private sealed class StubProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) =>
            new() { Status = statusCode ?? StatusCodes.Status500InternalServerError };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) =>
            new(modelStateDictionary) { Status = statusCode ?? StatusCodes.Status400BadRequest };
    }
}
