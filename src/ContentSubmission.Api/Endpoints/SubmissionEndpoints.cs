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

        group.MapPost("/", CreateSubmission).DisableAntiforgery();
        group.MapGet("/{id:guid}", GetSubmission);
        group.MapGet("/", GetAllSubmissions);
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

    private static async Task<IResult> GetSubmission(
        Guid id,
        SubmissionService submissionService,
        CancellationToken cancellationToken)
    {
        var submission = await submissionService.GetByIdAsync(id, cancellationToken);

        return submission is null
            ? Results.NotFound()
            : Results.Ok(SubmissionResponse.FromDomain(submission));
    }

    private static async Task<IResult> GetAllSubmissions(
        SubmissionService submissionService,
        CancellationToken cancellationToken)
    {
        var submissions = await submissionService.GetAllAsync(cancellationToken);

        // Maps the list of domain submissions to the list of API responses.
        var response = submissions.Select(SubmissionResponse.FromDomain);

        return Results.Ok(response);
    }
}
