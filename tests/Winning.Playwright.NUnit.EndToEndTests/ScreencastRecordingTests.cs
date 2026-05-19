namespace Winning.Playwright.NUnit.EndToEndTests;

/// <summary>
/// End-to-end checks that screencast recordings are written under the correct outcome folder
/// and listed as TRX attachments.
/// </summary>
[TestFixture]
public sealed class ScreencastRecordingTests
{
    [Test]
    public void Recording_IsSaved_InPassedFolder_WhenTestPasses() =>
        AssertScreencastInOutcomeFolder(EndToEndScenarioRuns.WhenTestPasses, "Passed");

    [Test]
    public void Recording_IsSaved_InInconclusiveFolder_WhenArrangeFails() =>
        AssertScreencastInOutcomeFolder(EndToEndScenarioRuns.WhenArrangeFails, "Inconclusive");

    [Test]
    public void Recording_IsSaved_InFailedFolder_WhenActFails() =>
        AssertScreencastInOutcomeFolder(EndToEndScenarioRuns.WhenActFails, "Failed");

    [Test]
    public void Recording_IsSaved_InFailedFolder_WhenAssertFails() =>
        AssertScreencastInOutcomeFolder(EndToEndScenarioRuns.WhenAssertFails, "Failed");

    [Test]
    public void IsAttachedInTrxReport_WhenTestPasses() =>
        AssertScreencastAttachedInTrx(EndToEndScenarioRuns.WhenTestPasses);

    [Test]
    public void IsAttachedInTrxReport_WhenArrangeFails() =>
        AssertScreencastAttachedInTrx(EndToEndScenarioRuns.WhenArrangeFails);

    [Test]
    public void IsAttachedInTrxReport_WhenActFails() =>
        AssertScreencastAttachedInTrx(EndToEndScenarioRuns.WhenActFails);

    [Test]
    public void IsAttachedInTrxReport_WhenAssertFails() =>
        AssertScreencastAttachedInTrx(EndToEndScenarioRuns.WhenAssertFails);

    private static void AssertScreencastInOutcomeFolder(ScenarioRun scenario, string expectedOutcomeSubfolder)
    {
        var folder = Path.Combine(scenario.ArtifactsDir, expectedOutcomeSubfolder);
        Assert.That(Directory.Exists(folder), Is.True,
            $"Expected '{expectedOutcomeSubfolder}' folder at '{folder}'.");
        Assert.That(Directory.GetFiles(folder, "*.webm"), Has.Length.GreaterThan(0),
            $"Expected a .webm screencast recording in '{folder}'.");
    }

    private static void AssertScreencastAttachedInTrx(ScenarioRun scenario)
    {
        var trxFile = TrxReport.FindFile(scenario.Runner.TrxDir);
        Assume.That(trxFile, Is.Not.Null, "No TRX file was generated.");
        Assert.That(TrxReport.ReadAttachmentPaths(trxFile!), Has.Some.EndsWith(".webm"),
            "Expected a .webm screencast to be listed as an attachment in the TRX report.");
    }
}
