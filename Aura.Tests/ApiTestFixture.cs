using System;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aura.Tests;

/// <summary>
/// Test fixture for API integration tests. Creates an in-memory test server
/// with the Aura.Api application for integration testing.
/// </summary>
public class ApiTestFixture : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpClient Client { get; }

    public ApiTestFixture()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                
                // Override services if needed for testing
                builder.ConfigureServices(services =>
                {
                    // Can add test-specific service configurations here
                });
            });

        Client = _factory.CreateClient();
    }

    public void Dispose()
    {
        Client?.Dispose();
        _factory?.Dispose();
    }
}
