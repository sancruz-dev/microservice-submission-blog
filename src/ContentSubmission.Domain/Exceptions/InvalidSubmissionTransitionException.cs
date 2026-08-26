namespace ContentSubmission.Domain.Exceptions;

public sealed class InvalidSubmissionTransitionException(SubmissionStatus from, SubmissionStatus to)
    : Exception($"Cannot transition submission from '{from}' to '{to}'.")
{
    public SubmissionStatus From { get; } = from;
    public SubmissionStatus To { get; } = to;
}
