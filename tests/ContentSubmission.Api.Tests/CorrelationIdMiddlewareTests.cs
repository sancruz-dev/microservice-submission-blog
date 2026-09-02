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
}