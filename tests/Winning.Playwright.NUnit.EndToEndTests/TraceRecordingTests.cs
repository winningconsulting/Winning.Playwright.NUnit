namespace Winning.Playwright.NUnit.EndToEndTests;

/// <summary>
/// End-to-end checks that Playwright traces are written under the correct outcome folder
/// and listed as TRX attachments.
/// </summary>
[TestFixture]
public sealed class TraceRecordingTests
{
    [Test]
    public void Trace_IsSaved_InPassedFolder_WhenTestPasses() =>
        AssertTraceInOutcomeFolder(EndToEndScenarioRuns.WhenTestPasses, "Passed");

    [Test]
    public void Trace_IsSaved_InInconclusiveFolder_WhenArrangeFails() =>
        AssertTraceInOutcomeFolder(EndToEndScenarioRuns.WhenArrangeFails, "Inconclusive");

    [Test]
    public void Trace_IsSaved_InFailedFolder_WhenActFails() =>
        AssertTraceInOutcomeFolder(EndToEndScenarioRuns.WhenActFails, "Failed");

    [Test]
    public void Trace_IsSaved_InFailedFolder_WhenAssertFails() =>
        AssertTraceInOutcomeFolder(EndToEndScenarioRuns.WhenAssertFails, "Failed");

    [Test]
    public void IsAttachedInTrxReport_WhenTestPasses() =>
        AssertTraceAttachedInTrx(EndToEndScenarioRuns.WhenTestPasses);

    [Test]
    public void IsAttachedInTrxReport_WhenArrangeFails() =>
        AssertTraceAttachedInTrx(EndToEndScenarioRuns.WhenArrangeFails);

    [Test]
    public void IsAttachedInTrxReport_WhenActFails() =>
        AssertTraceAttachedInTrx(EndToEndScenarioRuns.WhenActFails);

    [Test]
    public void IsAttachedInTrxReport_WhenAssertFails() =>
        AssertTraceAttachedInTrx(EndToEndScenarioRuns.WhenAssertFails);

    private static void AssertTraceInOutcomeFolder(ScenarioRun scenario, string expectedOutcomeSubfolder)
    {
        var folder = Path.Combine(scenario.ArtifactsDir, expectedOutcomeSubfolder);
        Assert.That(Directory.Exists(folder), Is.True,
            $"Expected '{expectedOutcomeSubfolder}' folder at '{folder}'.");
        Assert.That(Directory.GetFiles(folder, "*.zip"), Has.Length.GreaterThan(0),
            $"Expected a .zip trace file in '{folder}'.");
    }

    private static void AssertTraceAttachedInTrx(ScenarioRun scenario)
    {
        var trxFile = TrxReport.FindFile(scenario.Runner.TrxDir);
        Assume.That(trxFile, Is.Not.Null, "No TRX file was generated.");
        Assert.That(
            TrxReport.ReadAttachmentPaths(trxFile!),
            Has.Some.EndsWith("Playwright Trace.zip"),
            "Expected a trace attachment named from its description in the TRX report.");
    }
}
