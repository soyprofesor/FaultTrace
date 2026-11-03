using FaultTrace.AspNetCore.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FaultTrace.AspNetCore.Configuration;

internal sealed class MvcSetup : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options) => options.Filters.Add(new ValidationResultFilter());
}
