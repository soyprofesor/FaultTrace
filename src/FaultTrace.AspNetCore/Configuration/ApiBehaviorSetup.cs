using FaultTrace.AspNetCore.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Configuration;

// Post-configure, so it runs after MVC sets its own defaults and the template wins over them.
internal sealed class ApiBehaviorSetup : IPostConfigureOptions<ApiBehaviorOptions>
{
    private readonly IOptions<FaultTraceAspNetCoreOptions> _options;

    public ApiBehaviorSetup(IOptions<FaultTraceAspNetCoreOptions> options)
    {
        _options = options;
    }

    public void PostConfigure(string? name, ApiBehaviorOptions options)
    {
        if (!_options.Value.HandleValidation)
        {
            return;
        }

        // Otherwise [ApiController] fills an empty 404 or 415 with MVC's own ProblemDetails body.
        options.SuppressMapClientErrors = true;
        options.InvalidModelStateResponseFactory = _ => new ValidationProblemResult(isFromBinding: true);
    }
}
