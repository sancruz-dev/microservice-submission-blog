namespace ContentSubmission.Domain;

public enum SubmissionStatus
{
    Received,
    Validating,
    Validated,
    UnderReview,
    Approved,
    Publishing,
    Published,
    Rejected,
}
