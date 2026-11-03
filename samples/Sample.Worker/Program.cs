using FaultTrace.Core.DependencyInjection;
using FaultTrace.Oracle.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sample.Worker;

var builder = Host.CreateApplicationBuilder(args);

// No ASP.NET Core here: the translator works the same in any host.
builder.Services.AddFaultTraceCore().AddOracle();
builder.Services.AddHostedService<OrderWorker>();

builder.Build().Run();
