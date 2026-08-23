namespace SupportPortal.Contracts.Common;

public sealed record ProblemDetailsResponse(
    string Type,
    string Title,
    int Status,
    string TraceId,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);