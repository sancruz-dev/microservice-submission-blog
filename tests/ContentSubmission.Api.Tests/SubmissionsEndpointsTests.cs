using System.Net;
using System.Net.Http.Json;
using ContentSubmission.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ContentSubmission.Api.Tests;

public class SubmissionsEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateSubmissionRequest ValidRequest() => new(
        Title: "How RabbitMQ works",
        Description: "An introduction to messaging.",
        AuthorName: "Jane Doe",
        AuthorEmail: "jane@example.com",
        Category: "Backend",
        Level: "Intermediate",
        Slug: "how-rabbitmq-works",
        Tags: ["rabbitmq", "messaging"]);

    [Fact]
    public async Task POST_creates_a_submission_and_returns_201_with_location()
    {
        var response = await _client.PostAsJsonAsync("/submissions", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.NotNull(body);
        Assert.Equal("Received", body!.Status);
        Assert.Equal("how-rabbitmq-works", body.Slug);
    }

    [Fact]
    public async Task GET_returns_a_previously_created_submission()
    {
        var createResponse = await _client.PostAsJsonAsync("/submissions", ValidRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<SubmissionResponse>();

        var getResponse = await _client.GetAsync($"/submissions/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task GET_returns_404_for_unknown_id()
    {
        var response = await _client.GetAsync($"/submissions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_when_title_is_missing()
    {
        var request = ValidRequest() with { Title = null };

        var response = await _client.PostAsJsonAsync("/submissions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_for_an_unsafe_slug()
    {
        var request = ValidRequest() with { Slug = "../../etc/passwd" };

        var response = await _client.PostAsJsonAsync("/submissions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_for_an_invalid_level()
    {
        var request = ValidRequest() with { Level = "Expert" };

        var response = await _client.PostAsJsonAsync("/submissions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
