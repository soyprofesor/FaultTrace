using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Culture;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace FaultTrace.AspNetCore.Response;

internal sealed class ValidationProblemResult : IActionResult
{
    private readonly ModelStateDictionary? _modelState;

    // Without a dictionary, the errors come from the action's own model state.
    public ValidationProblemResult(ModelStateDictionary? modelState = null)
    {
        _modelState = modelState;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;

        var culture = services.GetRequiredService<FaultCultureResolver>().Resolve(http);
        var messages = services.GetRequiredService<IFaultMessageProvider>();
        var generic = messages.Get(FaultCodes.Validation, messages.ResolveCulture(culture)).Detail;

        var fault = services.GetRequiredService<IFaultFactory>().Create(
            FaultCategory.Validation,
            FaultCodes.Validation,
            StatusCodes.Status400BadRequest,
            culture,
            GetErrors(_modelState ?? context.ModelState, generic));

        await services.GetRequiredService<ProblemResponseWriter>().WriteAsync(http, fault, null, culture).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> GetErrors(ModelStateDictionary modelState, string generic)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var entry in modelState)
        {
            if (entry.Value.Errors.Count == 0)
            {
                continue;
            }

            var field = GetField(entry.Key);
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = new List<string>();
                errors[field] = messages;
            }

            messages.AddRange(entry.Value.Errors.Select(error => GetMessage(entry.Key, error, generic)));
        }

        return errors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }

    // A key that is a JSON path comes from the input formatter, and its text is a raw parser message with .NET type
    // names. An error with no text at all holds only an exception. Neither may reach the user.
    private static string GetMessage(string key, ModelError error, string generic) =>
        key.StartsWith("$", StringComparison.Ordinal) || string.IsNullOrEmpty(error.ErrorMessage)
            ? generic
            : error.ErrorMessage;

    // "Items[0].Name" and "$.items[0].name" both become "items[0].name", the name the client sent.
    private static string GetField(string key)
    {
        var path = key == "$" ? string.Empty : key.StartsWith("$.", StringComparison.Ordinal) ? key.Substring(2) : key;
        return string.Join(".", path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
