using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FaultTrace.AspNetCore.Response;

// ValidationProblem() and BadRequest(ModelState) build their body directly, without InvalidModelStateResponseFactory.
internal sealed class ValidationResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult result)
        {
            return;
        }

        if (result.Value is ValidationProblemDetails details)
        {
            // ValidationProblem(modelState) can be given a dictionary other than the action's own.
            context.Result = new ValidationProblemResult(context.ModelState.IsValid ? ToModelState(details) : null);
        }
        else if (result.Value is SerializableError && !context.ModelState.IsValid)
        {
            context.Result = new ValidationProblemResult();
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static ModelStateDictionary ToModelState(ValidationProblemDetails details)
    {
        var modelState = new ModelStateDictionary();
        foreach (var field in details.Errors)
        {
            foreach (var message in field.Value)
            {
                modelState.AddModelError(field.Key, message);
            }
        }

        return modelState;
    }
}
