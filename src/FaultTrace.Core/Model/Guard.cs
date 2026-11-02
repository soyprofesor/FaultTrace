using System;

namespace FaultTrace.Core.Model;

internal static class Guard
{
    public static string NotEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }
}
