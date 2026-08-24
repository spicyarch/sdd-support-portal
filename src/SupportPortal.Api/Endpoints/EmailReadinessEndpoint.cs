using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Common;
using SupportPortal.Application.Notifications;
using OperationsEmailReadinessRequest = SupportPortal.Contracts.Operations.EmailReadinessRequest;
using OperationsEmailReadinessResult = SupportPortal.Contracts.Operations.EmailReadinessResult;

namespace SupportPortal.Api.Endpoints;

public sealed class EmailReadinessEndpoint(
    EntraClaimsPrincipalFactory identityFactory,
    EmailReadinessService readiness)
{
    [Function("CheckEmailReadiness")]
    public async Task<IActionResult> Check([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/operations/email/readiness")] HttpRequest request)
    {
        try
        {
            var input = await request.ReadFromJsonAsync<OperationsEmailReadinessRequest>() ??
                throw new PortalServiceException(400, "Invalid readiness request", "A readiness request body is required.");
            var principal = identityFactory.Resolve(request);
            var correlationId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var result = await readiness.CheckAsync(principal, input, correlationId, request.HttpContext.RequestAborted);
            var response = new OperationsEmailReadinessResult(
                result.Mode.ToString(),
                result.Outcome.ToString(),
                result.Stage,
                result.ProviderHttpStatus,
                result.FailureCategory,
                result.CheckedAt,
                result.CorrelationId,
                result.DeliveryMeaning,
                result.InvalidSettingNames);
            var statusCode = result.Outcome is EmailReadinessOutcome.Ready or EmailReadinessOutcome.Accepted
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable;
            return ApiResponse.Json(request, response, statusCode);
        }
        catch (JsonException)
        {
            return ApiResponse.HandleException(
                request,
                new PortalServiceException(400, "Invalid readiness request", "The readiness request body is invalid."));
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }
}