using FaultTrace.AspNetCore.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Configuration;

internal sealed class MvcSetup : IConfigureOptions<MvcOptions>
{
    private readonly IOptions<FaultTraceAspNetCoreOptions> _options;

    public MvcSetup(IOptions<FaultTraceAspNetCoreOptions> options)
    {
        _options = options;
    }

    public void Configure(MvcOptions options)
    {
        if (_options.Value.HandleValidation)
        {
            options.Filters.Add(new ValidationResultFilter());
        }
    }
}
