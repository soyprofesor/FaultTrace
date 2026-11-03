using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;

namespace FaultTrace.AspNetCore.Handling;

// Every error status can come back without a body: from the framework, an auth handler, a rate limiter or a plain StatusCode(500).
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
            case StatusCodes.Status400BadRequest:
            case StatusCodes.Status415UnsupportedMediaType:
                category = FaultCategory.Validation;
                code = status == StatusCodes.Status400BadRequest ? FaultCodes.Validation : FaultCodes.UnsupportedMediaType;
                return true;
            case StatusCodes.Status401Unauthorized:
                category = FaultCategory.Unauthorized;
                code = FaultCodes.Unauthorized;
                return true;
            case StatusCodes.Status403Forbidden:
                category = FaultCategory.Forbidden;
                code = FaultCodes.Forbidden;
                return true;
            case StatusCodes.Status404NotFound:
            case StatusCodes.Status410Gone:
                category = FaultCategory.NotFound;
                code = FaultCodes.NotFound;
                return true;
            case StatusCodes.Status405MethodNotAllowed:
                category = FaultCategory.NotFound;
                code = FaultCodes.MethodNotAllowed;
                return true;
            case StatusCodes.Status409Conflict:
                category = FaultCategory.Conflict;
                code = FaultCodes.Conflict;
                return true;
            case StatusCodes.Status429TooManyRequests:
                category = FaultCategory.Transient;
                code = FaultCodes.TooManyRequests;
                return true;
            case 499:
                category = FaultCategory.ClientClosed;
                code = FaultCodes.ClientClosed;
                return true;
            case StatusCodes.Status503ServiceUnavailable:
                category = FaultCategory.Transient;
                code = FaultCodes.Unavailable;
                return true;
            case StatusCodes.Status504GatewayTimeout:
                category = FaultCategory.Timeout;
                code = FaultCodes.Timeout;
                return true;
        }

        if (status >= 400 && status < 500)
        {
            category = FaultCategory.Business;
            code = FaultCodes.Business;
            return true;
        }

        if (status >= 500 && status < 600)
        {
            category = FaultCategory.Internal;
            code = FaultCodes.Unexpected;
            return true;
        }

        category = default;
        code = string.Empty;
        return false;
    }
}
