using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ContentSubmission.Api.Tests;

public class CorrelationIdMiddlewareTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Response_includes_a_generated_correlation_id_when_none_is_sent()
    {
        var response = await _client.GetAsync("/rota-que-nao-existe");

        Assert.True(response.Headers.TryGetValues(HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task Response_echoes_back_a_correlation_id_sent_by_the_caller()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/rota-que-nao-existe");
        request.Headers.Add(HeaderName, "meu-id-de-teste");

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(HeaderName, out var values));
        Assert.Equal("meu-id-de-teste", values!.Single());
    }

    [Fact]
    public async Task Correlation_id_is_attached_as_a_logger_scope()
    {
        var capturingProvider = new CapturingLoggerProvider();

        using var customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<ILoggerProvider>(capturingProvider)));

        using var client = customFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/rota-que-nao-existe");
        request.Headers.Add(HeaderName, "log-test-id");

        await client.SendAsync(request);

        var correlationIdInAnyScope = capturingProvider.CapturedScopes
            .OfType<IReadOnlyCollection<KeyValuePair<string, object>>>()
            .SelectMany(scope => scope)
            .Any(entry => entry.Key == "CorrelationId" && Equals(entry.Value, "log-test-id"));

        Assert.True(correlationIdInAnyScope);
    }
}