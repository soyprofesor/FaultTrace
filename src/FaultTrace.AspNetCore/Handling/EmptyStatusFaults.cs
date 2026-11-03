using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;

namespace FaultTrace.AspNetCore.Handling;

// The statuses that the framework and auth handlers commonly return without a body.
internal static class EmptyStatusFaults
{
    // Same test as the framework's status code pages. A content type means the response belongs to someone else.
    public static bool IsEmpty(HttpResponse response) =>
        !response.HasStarted
        && (response.ContentLength == null || response.ContentLength == 0)
        && string.IsNullOrEmpty(response.ContentType);

    public static bool TryGet(int status, out FaultCategory category, out string code)
    {
        switch (status)
        {
            case StatusCodes.Status401Unauthorized:
                category = FaultCategory.Unauthorized;
                code = FaultCodes.Unauthorized;
                return true;
            case StatusCodes.Status403Forbidden:
                category = FaultCategory.Forbidden;
                code = FaultCodes.Forbidden;
                return true;
            case StatusCodes.Status404NotFound:
                category = FaultCategory.NotFound;
                code = FaultCodes.NotFound;
                return true;
            case StatusCodes.Status405MethodNotAllowed:
                category = FaultCategory.NotFound;
                code = FaultCodes.MethodNotAllowed;
                return true;
            case StatusCodes.Status415UnsupportedMediaType:
                category = FaultCategory.Validation;
                code = FaultCodes.UnsupportedMediaType;
                return true;
            default:
                category = default;
                code = string.Empty;
                return false;
        }
    }
}
