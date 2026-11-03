using FaultTrace.AspNetCore.DependencyInjection;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.DependencyInjection;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddFaultTrace(builder.Configuration.GetSection("FaultTrace"), options => options
        .MapException<KeyNotFoundException>(_ => new FaultProblem(404, FaultCodes.NotFound)))
    .AddOracle(oracle => oracle
        .MapColumn("NAME", new Dictionary<string, string> { ["en"] = "Customer name", ["uz-Latn"] = "Mijoz nomi" }));

builder.Services.AddRateLimiter(options =>
{
    // The framework rejects with 503 unless told otherwise.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("demo", limiter =>
    {
        limiter.PermitLimit = 1;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

// First in the pipeline, so its response covers everything after it.
app.UseFaultTrace();
app.UseRouting();
app.UseRateLimiter();
app.MapControllers();

app.Run();
