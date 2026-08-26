using ContentSubmission.Api.Contracts;
using ContentSubmission.Application.Submissions;
using ContentSubmission.Domain;

namespace ContentSubmission.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static void MapSubmissionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/submissions").WithTags("Submissions");

        group.MapPost("/", CreateSubmission);
        group.MapGet("/{id:guid}", GetSubmission);
        group.MapGet("/", GetAllSubmissions);
    }

    private static async Task<IResult> CreateSubmission(
        CreateSubmissionRequest request,
        SubmissionService submissionService,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SubmissionLevel>(request.Level, ignoreCase: true, out var level))
        {
            var allowedLevels = string.Join(", ", Enum.GetNames<SubmissionLevel>());
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Level)] = [$"Level must be one of: {allowedLevels}."],
            });
        }

        var input = new CreateSubmissionInput(
            request.Title,
            request.Description,
            request.AuthorName,
            request.AuthorEmail,
            request.Category,
            level,
            request.Slug,
            request.Tags);

        try
        {
            var submission = await submissionService.CreateAsync(input, cancellationToken);
            var response = SubmissionResponse.FromDomain(submission);

            return Results.Created($"/submissions/{response.Id}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [CleanMessage(ex)],
            });
        }
    }

    /// <summary>
    /// ArgumentException.Message appends "(Parameter 'x')" to whatever message was
    /// passed in. That's redundant here since the field name is already the
    /// dictionary key, so it's stripped for a cleaner API response.
    /// </summary>
    private static string CleanMessage(ArgumentException ex) =>
        ex.Message.Split(" (Parameter ", 2)[0];

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

        // Mapeia a lista de domínios para a lista de respostas da API
        var response = submissions.Select(SubmissionResponse.FromDomain);

        return Results.Ok(response);
    }
}
