namespace Winning.Playwright.NUnit.EndToEndTests;

/// <summary>
/// End-to-end checks that screencast recordings are written under the correct outcome folder
/// and listed as TRX attachments.
/// </summary>
[TestFixture]
public sealed class ScreencastRecordingTests
{
    private const string ScreencastFileNamePrefix = "Video";

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
        Assert.That(
            Directory.GetFiles(folder, "*.webm").Select(Path.GetFileName),
            Has.Some.StartsWith($"{ScreencastFileNamePrefix} - "),
            $"Expected a readable .webm screencast recording in '{folder}'.");
    }

    private static void AssertScreencastAttachedInTrx(ScenarioRun scenario)
    {
        var trxFile = TrxReport.FindFile(scenario.Runner.TrxDir);
        Assume.That(trxFile, Is.Not.Null, "No TRX file was generated.");
        var attachmentFileNames = TrxReport.ReadAttachmentPaths(trxFile!)
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null);
        Assert.That(
            attachmentFileNames.Any(fileName =>
                fileName!.StartsWith($"{ScreencastFileNamePrefix} - ", StringComparison.Ordinal) &&
                fileName.EndsWith(".webm", StringComparison.Ordinal)),
            Is.True,
            "Expected a screencast attachment named from its description in the TRX report.");
    }
}
