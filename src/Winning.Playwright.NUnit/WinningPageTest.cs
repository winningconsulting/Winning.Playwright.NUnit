using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Winning.Playwright.NUnit.Internal;

namespace Winning.Playwright.NUnit;

/// <summary>
/// Playwright <see cref="Microsoft.Playwright.NUnit.PageTest"/> extension that reads environment-driven options
/// and wires optional test features (screencast, tracing).
/// </summary>
public abstract class WinningPageTest : Microsoft.Playwright.NUnit.PageTest
{
    /// <summary>
    /// Environment variable: when truthy (<c>1</c>, <c>true</c>, <c>yes</c>, case-insensitive), enables per-test WebM screencast with Arrange/Act/Assert overlays.
    /// Subclasses may override <see cref="IsScreencastEnabled"/> to force enable/disable without relying on the environment.
    /// </summary>
    public const string RecordScreencastEnvironmentVariable = "WINNING_RECORD_SCREENCAST";

    /// <summary>
    /// Environment variable: when truthy, enables Playwright <see cref="Microsoft.Playwright.ITracing"/> on the test browser context.
    /// Subclasses may override <see cref="IsTracingEnabled"/>.
    /// </summary>
    public const string RecordTraceEnvironmentVariable = "WINNING_RECORD_TRACE";

    /// <summary>
    /// When truthy together with <see cref="RecordTraceEnvironmentVariable"/>, trace ZIPs are written for every test under a subfolder of <see cref="ArtifactsDirectory"/>
    /// named for the test outcome (NUnit <c>TestStatus.ToString()</c>, e.g. <c>Passed</c>, <c>Failed</c>, <c>Skipped</c>).
    /// When unset or false, only failing tests produce a trace ZIP under the <c>Failed</c> subfolder.
    /// </summary>
    public const string TraceAllTestsEnvironmentVariable = "WINNING_TRACE_ALL_TESTS";

    /// <summary>
    /// Environment variable: directory for artifact output (screencasts, traces). Default: <c>./</c>.
    /// </summary>
    public const string ArtifactsDirectoryEnvironmentVariable = "WINNING_ARTIFACTS_DIR";


    private ScreencastRecorder? screencast;
    private TracingRecorder? tracing;

    /// <summary>
    /// Directory for artifact output (screencasts, traces). Default: <see cref="ArtifactsDirectoryEnvironmentVariable"/> or <c>./</c>.
    /// </summary>
    protected virtual string ArtifactsDirectory =>
        EnvConfigLoader.LoadPath(ArtifactsDirectoryEnvironmentVariable);

    /// <summary>
    /// When <c>true</c>, screencast starts after the Playwright page is ready and stops in tear-down.
    /// Default: truthy <see cref="RecordScreencastEnvironmentVariable"/> unless overridden.
    /// </summary>
    protected virtual bool IsScreencastEnabled =>
        EnvConfigLoader.LoadBool(RecordScreencastEnvironmentVariable);

    /// <summary>
    /// When <c>true</c>, tracing starts after the browser context is ready and stops in tear-down.
    /// Default: truthy <see cref="RecordTraceEnvironmentVariable"/> unless overridden.
    /// </summary>
    protected virtual bool IsTracingEnabled =>
        EnvConfigLoader.LoadBool(RecordTraceEnvironmentVariable);

    /// <summary>
    /// When <c>true</c> and tracing is enabled, persist a trace ZIP for every test under the same per-outcome subfolders (NUnit <c>TestStatus</c> names).
    /// When <c>false</c>, only failed tests get a ZIP under <c>Failed</c>. Default: truthy <see cref="TraceAllTestsEnvironmentVariable"/>.
    /// </summary>
    protected virtual bool TraceAllTests =>
        EnvConfigLoader.LoadBool(TraceAllTestsEnvironmentVariable);

    [SetUp]
    protected async Task StartTracingAndScreencastIfEnabled()
    {
        if (this.IsTracingEnabled)
        {
            this.tracing = new TracingRecorder(this.Context, this.ArtifactsDirectory, this.TraceAllTests);
            await this.tracing.StartAsync();
        }

        if (this.IsScreencastEnabled)
        {
            this.screencast = new ScreencastRecorder(this.Page, this.ArtifactsDirectory);
            await this.screencast.StartAsync();
        }
    }

    [TearDown]
    protected async Task SaveTracingAndScreencastIfEnabled()
    {
        var status = TestContext.CurrentContext.Result.Outcome.Status;

        if (this.screencast is not null)
        {
            try
            {
                await this.screencast.StopAsync(status);
            }
            finally
            {
                this.screencast = null;
            }
        }

        if (this.tracing is not null)
        {
            try
            {
                await this.tracing.StopAsync(status);
            }
            finally
            {
                this.tracing = null;
            }
        }
    }

    /// <summary>
    /// Runs an arrange step: when screencast is enabled, shows a chapter titled <see cref="TestStage.Arrange"/> then runs <paramref name="action"/>.
    /// When tracing is enabled, wraps the step in a trace group titled <see cref="TestStage.Arrange"/>: ….
    /// Any exception from <paramref name="action"/> marks the test inconclusive and, when screencast is enabled, records an inconclusive arrange overlay.
    /// </summary>
    protected async Task ArrangeStage(string description, Func<Task> action) => await this.RunAnnotatedStage(TestStage.Arrange, description, action);

    /// <summary>
    /// Runs an act step: with screencast enabled, shows an overlay titled <see cref="TestStage.Act"/> then runs <paramref name="action"/>.
    /// With tracing enabled, wraps the step in trace groups titled <see cref="TestStage.Act"/>: ….
    /// </summary>
    protected async Task ActStage(string description, Func<Task> action) => await this.RunAnnotatedStage(TestStage.Act, description, action);

    /// <summary>
    /// Runs an assert step: with screencast enabled, shows an overlay titled <see cref="TestStage.Assert"/> then runs <paramref name="assertion"/>.
    /// With tracing enabled, wraps the step in trace groups titled <see cref="TestStage.Assert"/>: ….
    /// </summary>
    protected async Task AssertStage(string description, Func<Task> assertion) => await this.RunAnnotatedStage(TestStage.Assert, description, assertion);

    private async Task RunAnnotatedStage(TestStage stage, string description, Func<Task> action)
    {
        try
        {
            await this.AnnotateStageBeginning(stage, description);
            await action();
        }
        catch (Exception exception)
        {
            this.screencast?.StoreFailureContextForFinalOverlay(stage, description, exception);

            // Special rule for arrange, when it fails, we want to mark the test as inconclusive
            if (stage == TestStage.Arrange)
            {
                Assert.Inconclusive($"Arrange aborted: {exception.Message}");
            }
            throw;
        }
        finally
        {
            await this.ConcludeAnnotatedStage(stage, description);
        }
    }

    /// <summary>
    /// Starts annotations for a stage: trace group (when tracing is on) and screencast chapter (when screencast is on).
    /// Pair with <see cref="ConcludeAnnotatedStage"/> in a <c>finally</c> block. Do not call again before concluding; stages are not stacked.
    /// </summary>
    protected async Task AnnotateStageBeginning(TestStage stage, string description)
    {
        if (this.tracing is not null)
        {
            await this.tracing.AnnotateStageBeginningAsync(stage, description);
        }

        if (this.screencast is not null)
        {
            await this.screencast.AnnotateStageBeginningAsync(stage, description);
        }
    }

    /// <summary>Ends the trace group opened by <see cref="AnnotateStageBeginning"/> (no-op if tracing is off).</summary>
    protected async Task ConcludeAnnotatedStage(TestStage stage, string description)
    {
        if (this.tracing is not null)
        {
            await this.tracing.ConcludeStageAsync(stage, description);
        }

        if (this.screencast is not null)
        {
            await this.screencast.ConcludeStageAsync(stage, description);
        }
    }

}
