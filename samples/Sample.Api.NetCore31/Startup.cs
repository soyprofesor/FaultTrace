using System.Collections.Generic;
using FaultTrace.AspNetCore.DependencyInjection;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.Api.NetCore31;

public sealed class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();

        services
            .AddFaultTrace(_configuration.GetSection("FaultTrace"), options => options
                .MapException<KeyNotFoundException>(_ => new FaultProblem(404, FaultCodes.NotFound)))
            .AddOracle(oracle => oracle
                .MapColumn("NAME", new Dictionary<string, string> { ["en"] = "Customer name", ["uz-Latn"] = "Mijoz nomi" }));
    }

    public void Configure(IApplicationBuilder app)
    {
        // First in the pipeline, so its response covers everything after it.
        app.UseFaultTrace();
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
