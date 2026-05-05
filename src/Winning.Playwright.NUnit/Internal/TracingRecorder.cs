using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Winning.Playwright.NUnit.Internal;

/// <summary>
/// Per-test Playwright trace capture: start on context, stop with optional ZIP path (failures only or all tests).
/// </summary>
internal sealed class TracingRecorder
{
    private readonly IBrowserContext context;
    private readonly string artifactsDir;
    private readonly bool traceAllTests;
    private bool isRunning;

    public TracingRecorder(IBrowserContext context, string artifactsDir, bool traceAllTests)
    {
        this.context = context;
        this.artifactsDir = artifactsDir;
        this.traceAllTests = traceAllTests;
    }

    public async Task StartAsync()
    {
        Directory.CreateDirectory(this.artifactsDir);

        var testTitle = ArtifactUtilities.HumanizeTestDisplayName(TestContext.CurrentContext.Test.Name);

        await this.context.Tracing.StartAsync(new()
        {
            Title = testTitle,
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        this.isRunning = true;
    }

    public async Task StopAsync(TestStatus currentTestStatus)
    {
        if (!this.isRunning)
        {
            return;
        }

        this.isRunning = false;

        var stopOptions = new TracingStopOptions();

        var testHasAnAcceptableStatus = currentTestStatus is TestStatus.Passed or TestStatus.Skipped;

        if (this.traceAllTests || !testHasAnAcceptableStatus)
        {
            var name = ArtifactUtilities.SanitizeFileName(TestContext.CurrentContext.Test.Name);
            var outcomeFolder = currentTestStatus.ToString();
            var folderPath = Path.Combine(this.artifactsDir, outcomeFolder);
            Directory.CreateDirectory(folderPath);
            stopOptions.Path = Path.Combine(folderPath, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{name}.zip");
        }

        await this.context.Tracing.StopAsync(stopOptions);
    }

    /// <summary>Starts a trace group for a stage.</summary>
    internal Task AnnotateStageBeginningAsync(TestStage stage, string description) => this.context.Tracing.GroupAsync($"{stage.ToChapterTitle()}: {description}");

    /// <summary>Ends the trace group opened by <see cref="AnnotateStageBeginningAsync"/>.</summary>
    internal Task ConcludeStageAsync(TestStage stage, string description) => this.context.Tracing.GroupEndAsync();
}
