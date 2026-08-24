using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Common;
using SupportPortal.Domain.Common;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Api.Middleware;

public static class ApiResponse
{
    public static ObjectResult Json(HttpRequest request, object value, int statusCode = StatusCodes.Status200OK, string? etag = null)
    {
        AddCorsHeaders(request);
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.HttpContext.Response.Headers.ETag = $"\"{etag}\"";
        }

        return new ObjectResult(value)
        {
            StatusCode = statusCode,
            DeclaredType = value.GetType()
        };
    }

    public static StatusCodeResult NotModified(HttpRequest request, string? etag = null)
    {
        AddCorsHeaders(request);
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.HttpContext.Response.Headers.ETag = $"\"{etag}\"";
        }

        return new StatusCodeResult(StatusCodes.Status304NotModified);
    }

    public static IActionResult HandleException(HttpRequest request, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            PortalServiceException serviceException => (serviceException.StatusCode, serviceException.Title, serviceException.Detail),
            DomainException domainException => (StatusCodes.Status400BadRequest, "Validation failed", domainException.Message),
            _ => (StatusCodes.Status500InternalServerError, "Request failed", "The request could not be completed.")
        };

        AddCorsHeaders(request);
        var problem = new ProblemDetailsResponse(
            $"https://support-portal.invalid/problems/{statusCode}",
            title,
            statusCode,
            Activity.Current?.Id ?? request.HttpContext.TraceIdentifier,
            detail);
        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = ["application/problem+json"]
        };
    }

    public static void AddCorsHeaders(HttpRequest request)
    {
        var response = request.HttpContext.Response;
        var origin = request.Headers.Origin.ToString();
        var options = request.HttpContext.RequestServices.GetService<AzureOptions>();
        if (!string.IsNullOrWhiteSpace(origin) && options?.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) == true)
        {
            response.Headers.AccessControlAllowOrigin = origin;
        }

        var allowDevelopmentIdentity = StringComparer.OrdinalIgnoreCase.Equals(
            request.HttpContext.RequestServices.GetService<IConfiguration>()?["ASPNETCORE_ENVIRONMENT"],
            "Development");
        response.Headers.AccessControlAllowHeaders = allowDevelopmentIdentity
            ? "Authorization, Content-Type, If-Match, If-None-Match, Idempotency-Key, X-Development-Identity"
            : "Authorization, Content-Type, If-Match, If-None-Match, Idempotency-Key";
        response.Headers.AccessControlAllowMethods = "GET, POST, PATCH, OPTIONS";
        response.Headers.Vary = "Origin";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.XFrameOptions = "DENY";
    }
}