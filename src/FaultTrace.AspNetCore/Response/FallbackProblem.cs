using System.Text;
using FaultTrace.AspNetCore.Configuration;

namespace FaultTrace.AspNetCore.Response;

// The last resort when FaultTrace itself fails, so it depends on nothing that could fail too:
// no options, no localization, no serializer. An error ID contains only letters, digits and a dash.
internal static class FallbackProblem
{
    public static byte[] Create(string errorId)
    {
        return Encoding.UTF8.GetBytes(
            "{\"type\":\"" + FaultTraceAspNetCoreOptions.DefaultTypeBaseUri + "unexpected\"," +
            "\"title\":\"Unexpected error\"," +
            "\"status\":500," +
            "\"detail\":\"An unexpected error occurred. Please contact support and provide the error ID.\"," +
            "\"code\":\"unexpected\"," +
            "\"errorId\":\"" + errorId + "\"}");
    }
}
