using WorldGate.Monitor.Core.ManagedImages;
using Xunit;

namespace WorldGate.Monitor.Core.Tests;

public sealed class ManagedImageProbeDiagnosticsTests
{
    [Fact]
    public void SummarizeCommandFailure_uses_first_stderr_line()
    {
        const string stderr = """
            /bin/bash: line 1: {{.State}}: command not found
            extra context
            """;

        string summary = ManagedImageProbeDiagnostics.SummarizeCommandFailure(stderr, null);
        Assert.Equal("/bin/bash: line 1: {{.State}}: command not found", summary);
    }

    [Fact]
    public void FormatDockerProbeDetail_describes_successful_steps()
    {
        string detail = ManagedImageProbeDiagnostics.FormatDockerProbeDetail(
            inspectOk: true,
            psOk: true,
            ManagedImagePresence.Present,
            running: 0,
            total: 0);
        Assert.Contains("docker image inspect: ok", detail, StringComparison.Ordinal);
        Assert.Contains("0/0 running", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ProbeFailed_snapshot_keeps_summary_short_and_stores_detail()
    {
        ManagedImageStatusSnapshot snapshot = ManagedImageStatusSnapshot.ProbeFailed(
            "id",
            "worldgate-runner",
            "docker ps failed: permission denied");

        Assert.Equal(DockerCliOutputParser.ProbeFailedSummary, snapshot.Summary);
        Assert.Equal(DockerCliOutputParser.ProbeFailedSummary, snapshot.ErrorMessage);
        Assert.Equal("docker ps failed: permission denied", snapshot.ProbeDiagnosticDetail);
    }
}
