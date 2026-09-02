using System.Diagnostics.Metrics;

namespace ContentSubmission.Application.Submissions;

public sealed class SubmissionMetrics : IDisposable
{
    public const string MeterName = "ContentSubmission";

    private readonly Meter _meter;
    private readonly Counter<long> _submissionsReceived;
    private readonly Counter<long> _submissionsRejected;

    public SubmissionMetrics()
    {
        _meter = new Meter(MeterName);
        _submissionsReceived = _meter.CreateCounter<long>(
            "submissions.received",
            description: "Submissões recebidas, antes de qualquer validação.");
        _submissionsRejected = _meter.CreateCounter<long>(
            "submissions.rejected",
            description: "Submissões rejeitadas na validação de conteúdo.");
    }

    public void SubmissionReceived() => _submissionsReceived.Add(1);

    public void SubmissionRejected() => _submissionsRejected.Add(1);

    public void Dispose() => _meter.Dispose();
}