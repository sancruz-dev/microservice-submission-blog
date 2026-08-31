using System.Net;
using System.Net.Http.Json;
using System.Text;
using ContentSubmission.Api.Contracts;

namespace ContentSubmission.Api.Tests;

public class SubmissionsEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private const string ValidMdx = """
        ---
        title: How RabbitMQ works
        description: An introduction to messaging.
        slug: how-rabbitmq-works
        author: Jane Doe
        category: Backend
        level: Intermediate
        tags:
          - rabbitmq
          - messaging
        ---

        RabbitMQ is a message broker.
        """;

    private static MultipartFormDataContent BuildRequest(
        string mdx = ValidMdx,
        string authorEmail = "jane@example.com",
        string fileName = "how-rabbitmq-works.mdx")
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(authorEmail), "authorEmail" },
        };

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(mdx));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", fileName);

        return content;
    }

    [Fact]
    public async Task POST_creates_a_submission_and_returns_201_with_location()
    {
        var response = await _client.PostAsync("/submissions", BuildRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.NotNull(body);
        // Content validation, the (fake) GitHub Issue creation and the UnderReview
        // transition all happen synchronously within CreateAsync - see ADR-003 and
        // SubmissionService.CreateAsync.
        Assert.Equal("UnderReview", body!.Status);
        Assert.NotNull(body.GitHubIssueNumber);
        Assert.Equal("how-rabbitmq-works", body.Slug);
        Assert.Equal("Jane Doe", body.AuthorName);
        Assert.Equal("jane@example.com", body.AuthorEmail);
        Assert.Equal(["rabbitmq", "messaging"], body.Tags);
    }

    [Fact]
    public async Task GET_returns_a_previously_created_submission()
    {
        var createResponse = await _client.PostAsync("/submissions", BuildRequest());
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
    public async Task POST_returns_400_when_no_file_is_sent()
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent("jane@example.com"), "authorEmail" },
        };

        var response = await _client.PostAsync("/submissions", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_for_a_non_mdx_extension()
    {
        var response = await _client.PostAsync("/submissions", BuildRequest(fileName: "post.txt"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_for_a_file_over_the_size_limit()
    {
        var oversizedBody = "---\ntitle: x\ndescription: y\nslug: x\nauthor: a\ncategory: b\nlevel: Beginner\n---\n\n"
            + new string('a', 400 * 1024);

        var response = await _client.PostAsync("/submissions", BuildRequest(oversizedBody));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_when_frontmatter_is_missing()
    {
        var response = await _client.PostAsync("/submissions", BuildRequest("# No frontmatter here"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_for_an_invalid_slug_in_frontmatter()
    {
        const string mdx = """
            ---
            title: x
            description: y
            slug: ../../etc/passwd
            author: a
            category: b
            level: Beginner
            ---

            Body.
            """;

        var response = await _client.PostAsync("/submissions", BuildRequest(mdx));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_returns_400_for_content_with_an_import_statement()
    {
        const string mdx = """
            ---
            title: x
            description: y
            slug: x
            author: a
            category: b
            level: Beginner
            ---

            import Foo from './foo';

            Body.
            """;

        var response = await _client.PostAsync("/submissions", BuildRequest(mdx));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_reports_multiple_errors_at_once()
    {
        const string mdx = """
            ---
            slug: ../bad
            level: Expert
            ---

            <script>alert(1)</script>
            """;

        var response = await _client.PostAsync("/submissions", BuildRequest(mdx));
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (System.Text.Json.JsonElement)problem!["errors"];
        var contentErrors = errors.GetProperty("content").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.True(contentErrors.Count > 1, "Expected multiple validation errors in a single response.");
    }
}
