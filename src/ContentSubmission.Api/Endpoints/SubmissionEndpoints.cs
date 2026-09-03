using ContentSubmission.Api.Contracts;
using ContentSubmission.Application.Exceptions;
using ContentSubmission.Application.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace ContentSubmission.Api.Endpoints;

public static class SubmissionEndpoints
{
    private const long MaxFileSizeBytes = 300 * 1024; // 300 KB - text-only content, images are referenced by path.
    private const string RequiredExtension = ".mdx";

    public static void MapSubmissionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/submissions").WithTags("Submissions");

        // GET /submissions and GET /submissions/{id} were removed (ADR-006):
        // the frontend only ever calls POST, and the list endpoint returned
        // every author's email address to anyone with the URL. Querying
        // submissions now happens directly against the database (see the
        // workspace insights query in docs/), not through the public API.
        group.MapPost("/", CreateSubmission)
            .DisableAntiforgery()
            .RequireRateLimiting(SubmissionRateLimiting.PolicyName);
    }

    private static async Task<IResult> CreateSubmission(
        IFormFile? file,
        [FromForm] string? authorEmail,
        SubmissionService submissionService,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["A .mdx file is required."],
            });
        }

        if (!Path.GetExtension(file.FileName).Equals(RequiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [$"File must have a '{RequiredExtension}' extension."],
            });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [$"File cannot be larger than {MaxFileSizeBytes / 1024} KB."],
            });
        }

        string rawMdx;
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            rawMdx = await reader.ReadToEndAsync(cancellationToken);
        }

        var input = new CreateSubmissionInput(rawMdx, authorEmail);

        try
        {
            var submission = await submissionService.CreateAsync(input, cancellationToken);
            var response = SubmissionResponse.FromDomain(submission);

            return Results.Created($"/submissions/{response.Id}", response);
        }
        catch (InvalidSubmissionContentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["content"] = [.. ex.Errors],
            });
        }
    }

}
