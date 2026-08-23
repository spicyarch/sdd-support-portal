namespace SupportPortal.Application.Common;

public sealed class PortalServiceException : Exception
{
    public PortalServiceException(int statusCode, string title, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
    }

    public int StatusCode { get; }

    public string Title { get; }

    public string Detail { get; }
}