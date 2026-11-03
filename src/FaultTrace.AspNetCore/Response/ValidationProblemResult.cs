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
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Response;

internal sealed class ValidationProblemResult : IActionResult
{
    private readonly ModelStateDictionary? _modelState;
    private readonly bool _isFromBinding;

    // Without a dictionary, the errors come from the action's own model state. From binding means the action never ran.
    public ValidationProblemResult(ModelStateDictionary? modelState = null, bool isFromBinding = false)
    {
        _modelState = modelState;
        _isFromBinding = isFromBinding;
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
            GetErrors(_modelState ?? context.ModelState, context, generic));

        await services.GetRequiredService<ProblemResponseWriter>().WriteAsync(http, fault, null, culture).ConfigureAwait(false);
    }

    private Dictionary<string, IReadOnlyList<string>> GetErrors(ModelStateDictionary modelState, ActionContext context, string generic)
    {
        var missingBody = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<MvcOptions>>().Value
            .ModelBindingMessageProvider.MissingRequestBodyRequiredValueAccessor();
        var bodyParameters = _isFromBinding || HasUnreadBody(modelState, missingBody)
            ? GetBodyParameters(context)
            : [];

        var errors = new Dictionary<string, List<string>>();
        foreach (var entry in modelState)
        {
            // A parameter name means nothing to the client: it only says that the body wasn't read.
            if (bodyParameters.Contains(entry.Key))
            {
                continue;
            }

            var field = GetField(entry.Key);

            // An error about the whole body has no field to go with, and the detail text already covers it.
            var failures = entry.Value.Errors
                .Where(error => field.Length > 0 || !IsBodyFailure(entry.Key, error, missingBody))
                .Select(error => GetMessage(entry.Key, error, generic))
                .ToList();
            if (failures.Count == 0)
            {
                continue;
            }

            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors[field] = messages;
            }

            messages.AddRange(failures);
        }

        return errors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }

    private static bool HasUnreadBody(ModelStateDictionary modelState, string missingBody) =>
        modelState.Any(entry => entry.Value?.Errors.Any(error => IsBodyFailure(entry.Key, error, missingBody)) == true);

    private static bool IsBodyFailure(string key, ModelError error, string missingBody) =>
        string.IsNullOrEmpty(error.ErrorMessage) || error.ErrorMessage == missingBody || IsParserMessage(key, error.ErrorMessage);

    private static HashSet<string> GetBodyParameters(ActionContext context)
    {
        return new HashSet<string>(
            context.ActionDescriptor.Parameters
                .Where(parameter => parameter.BindingInfo?.BindingSource == BindingSource.Body)
                .Select(parameter => parameter.BindingInfo?.BinderModelName ?? parameter.Name),
            StringComparer.OrdinalIgnoreCase);
    }

    // A raw parser message can name .NET types, and an error with no text at all holds only an exception.
    // Neither may reach the user.
    private static string GetMessage(string key, ModelError error, string generic) =>
        string.IsNullOrEmpty(error.ErrorMessage) || IsParserMessage(key, error.ErrorMessage)
            ? generic
            : error.ErrorMessage;

    // System.Text.Json reports under a JSON path. Newtonsoft keeps the plain field name, and its messages always end
    // with the position: "... Path 'amount', line 1, position 15."
    private static bool IsParserMessage(string key, string message) =>
        key.StartsWith('$')
        || (message.Contains(" Path '") && message.Contains("', line ") && message.Contains(", position "));

    // "Items[0].Name" and "$.items[0].name" both become "items[0].name", the name the client sent.
    private static string GetField(string key)
    {
        var path = key == "$" ? string.Empty : key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return string.Join(".", path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
