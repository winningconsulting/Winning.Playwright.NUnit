using Winning.Playwright.NUnit.EndToEndTests.DummyTests;
using DummyFixture = Winning.Playwright.NUnit.EndToEndTests.DummyTests.DummyTests;

namespace Winning.Playwright.NUnit.EndToEndTests;

/// <summary>
/// Runs each dummy-test scenario once for end-to-end tests in this namespace.
/// Dummy tests live under <c>DummyTests</c> (child namespace) so subprocess runs do not re-enter this setup.
/// </summary>
[SetUpFixture]
internal sealed class EndToEndScenarioRuns
{
    public static ScenarioRun WhenTestPasses { get; private set; } = null!;
    public static ScenarioRun WhenArrangeFails { get; private set; } = null!;
    public static ScenarioRun WhenActFails { get; private set; } = null!;
    public static ScenarioRun WhenAssertFails { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        if (IsDummySubprocess())
            return;

        WhenTestPasses = await RunAsync(nameof(DummyFixture.PassesAllStages));
        WhenArrangeFails = await RunAsync(nameof(DummyFixture.FailsDuringArrangeStage));
        WhenActFails = await RunAsync(nameof(DummyFixture.FailsDuringActStage));
        WhenAssertFails = await RunAsync(nameof(DummyFixture.FailsDuringAssertStage));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        WhenTestPasses?.Dispose();
        WhenArrangeFails?.Dispose();
        WhenActFails?.Dispose();
        WhenAssertFails?.Dispose();
    }

    private static async Task<ScenarioRun> RunAsync(string testMethodName)
    {
        var runner = new DummyTestRunner(testMethodName);
        runner.WithEnvironmentVariable("WINNING_RECORD_SCREENCAST", "true")
              .WithEnvironmentVariable("WINNING_RECORD_TRACE", "true")
              .WithEnvironmentVariable("WINNING_TRACE_ALL_TESTS", "true")
              .WithEnvironmentVariable("WINNING_ARTIFACTS_DIR", runner.ArtifactsDir);
        await runner.RunAsync();
        return new ScenarioRun(runner);
    }

    private static bool IsDummySubprocess() =>
        string.Equals(
            Environment.GetEnvironmentVariable("WINNING_E2E_DUMMY_SUBPROCESS"),
            "1",
            StringComparison.Ordinal);
}

/// <summary>Artifacts and TRX output from a single dummy-test subprocess run.</summary>
internal sealed class ScenarioRun(DummyTestRunner runner) : IDisposable
{
    public DummyTestRunner Runner { get; } = runner;

    /// <summary>Root directory where the subprocess wrote screencast and trace artifacts.</summary>
    public string ArtifactsDir => Runner.ArtifactsDir;

    public void Dispose() => Runner.Dispose();
}
