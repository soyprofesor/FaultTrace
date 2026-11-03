using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace FaultTrace.Oracle.Detection;

// ODP.NET is read through reflection on purpose: a package reference would cause version conflicts in consumers.
internal static class OracleExceptionReader
{
    private const string OracleExceptionTypeName = "Oracle.ManagedDataAccess.Client.OracleException";
    private const int MaxDepth = 16;

    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> Properties = new();

    /// <summary>Finds an Oracle exception in the exception or its inner exceptions. Never throws.</summary>
    public static OracleExceptionInfo? Find(Exception? exception) => Find(exception, 0);

    private static OracleExceptionInfo? Find(Exception? exception, int depth)
    {
        if (exception == null || depth > MaxDepth)
        {
            return null;
        }

        if (IsOracleException(exception.GetType()))
        {
            return Read(exception);
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                var found = Find(inner, depth + 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        return Find(exception.InnerException, depth + 1);
    }

    private static bool IsOracleException(Type type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.FullName == OracleExceptionTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static OracleExceptionInfo Read(Exception exception)
    {
        var errors = ReadErrors(exception);
        var number = ReadProperty(exception, "Number") as int?;
        if (!number.HasValue && errors.Count > 0)
        {
            number = errors[0].Number;
        }

        var message = ReadMessage(exception, errors);
        return new OracleExceptionInfo(exception, number, message);
    }

    private static List<(int? Number, string? Message)> ReadErrors(Exception exception)
    {
        var errors = new List<(int? Number, string? Message)>();

        try
        {
            if (ReadProperty(exception, "Errors") is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        errors.Add((ReadProperty(item, "Number") as int?, ReadProperty(item, "Message") as string));
                    }
                }
            }
        }
        catch (Exception)
        {
            // The collection is optional: the exception message is parsed instead.
        }

        return errors;
    }

    private static string ReadMessage(Exception exception, List<(int? Number, string? Message)> errors)
    {
        var message = exception.Message;
        if (message.IndexOf("ORA-", StringComparison.Ordinal) >= 0)
        {
            return message;
        }

        var lines = new List<string>();
        foreach (var (_, text) in errors)
        {
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add(text!);
            }
        }

        return lines.Count > 0 ? string.Join("\n", lines) : message;
    }

    private static object? ReadProperty(object target, string name)
    {
        try
        {
            var type = target.GetType();
            var property = Properties.GetOrAdd((type, name), key => key.Type.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance));
            return property?.GetValue(target);
        }
        catch (Exception)
        {
            // A driver version may differ or its getter may fail: treat the value as missing.
            return null;
        }
    }
}
