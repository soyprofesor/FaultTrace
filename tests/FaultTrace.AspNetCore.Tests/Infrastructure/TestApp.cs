using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FaultTrace.AspNetCore.Configuration;
using FaultTrace.AspNetCore.DependencyInjection;
using FaultTrace.Oracle.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FaultTrace.AspNetCore.Tests.Infrastructure;

// A real host on TestServer, so every test goes through the whole pipeline on each target framework.
internal sealed class TestApp : IDisposable
{
    private readonly IHost _host;

    private TestApp(IHost host, CapturingLoggerProvider logs)
    {
        _host = host;
        Logs = logs;
        Client = host.GetTestClient();
    }

    public HttpClient Client { get; }

    public CapturingLoggerProvider Logs { get; }

    public IServiceProvider Services => _host.Services;

    public static async Task<TestApp> StartAsync(
        RequestDelegate handler,
        Action<FaultTraceAspNetCoreOptions>? configure = null,
        Action<IServiceCollection>? services = null,
        Action<IApplicationBuilder>? beforeFaultTrace = null,
        Action<IApplicationBuilder>? afterFaultTrace = null,
        string environment = "Production",
        Action<IServiceCollection>? addFaultTrace = null)
    {
        var logs = new CapturingLoggerProvider();
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment(environment)
                .ConfigureServices(collection =>
                {
                    collection.AddSingleton<ILoggerProvider>(logs);
                    if (addFaultTrace != null)
                    {
                        addFaultTrace(collection);
                    }
                    else
                    {
                        collection.AddFaultTrace(configure).AddOracle();
                    }

                    services?.Invoke(collection);
                })
                .Configure(app =>
                {
                    beforeFaultTrace?.Invoke(app);
                    app.UseFaultTrace();
                    afterFaultTrace?.Invoke(app);
                    app.Run(handler);
                }))
            .StartAsync();

        return new TestApp(host, logs);
    }

    public static Task<TestApp> ThrowingAsync(
        Exception exception,
        Action<FaultTraceAspNetCoreOptions>? configure = null,
        string environment = "Production")
    {
        return StartAsync(_ => throw exception, configure, environment: environment);
    }

    public async Task<Problem> GetProblemAsync(string url = "/api/order/add", Action<HttpRequestMessage>? prepare = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        prepare?.Invoke(request);
        using var response = await Client.SendAsync(request);
        return await Problem.ReadAsync(response);
    }

    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
    }
}

internal sealed class Problem
{
    private Problem(int status, string? contentType, IReadOnlyDictionary<string, string> headers, string text, JsonElement json)
    {
        Status = status;
        ContentType = contentType;
        Headers = headers;
        Text = text;
        Json = json;
    }

    public int Status { get; }

    public string? ContentType { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public string Text { get; }

    public JsonElement Json { get; }

    public static async Task<Problem> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        var headers = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(text);
        return new Problem(
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            headers,
            text,
            document.RootElement.Clone());
    }

    public string? Get(string name) => Json.TryGetProperty(name, out var value) ? value.GetString() : null;

    public bool Has(string name) => Json.TryGetProperty(name, out _);
}
