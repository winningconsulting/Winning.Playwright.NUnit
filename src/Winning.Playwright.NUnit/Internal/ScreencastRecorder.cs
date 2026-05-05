using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Winning.Playwright.NUnit.Internal;

/// <summary>
/// Per-test screencast capture, optional timed test-title banner, ending outcome overlays (pass/fail/inconclusive), and chapter overlays during steps.
/// </summary>
internal sealed class ScreencastRecorder
{
    /// <summary>Failure outcome card subtitle when the exception occurred during the act stage.</summary>
    private const string FailureOverlayExplanationActStage =
        "The test could not use the feature or interaction it targets.";

    /// <summary>Failure outcome card subtitle when the exception occurred during the assert stage.</summary>
    private const string FailureOverlayExplanationAssertStage =
        "The assertion failed: what the test checked did not match what was expected.";

    /// <summary>How long the humanized test-title banner stays on screen before Playwright removes the overlay (<c>SetTimeout</c> in driver; no CSS fade).</summary>
    private const float TitleOverlayDurationMs = 1500;

    /// <summary>Pass / fail / inconclusive cards at end of recording (recording stops there).</summary>
    private const float OutcomeOverlayDurationMs = 2000;

    /// <summary>Known Playwright .NET bug: screencast <c>ShowOverlay</c>/<c>ShowChapter</c> can throw until a fixed package is published.</summary>
    private const string KnownScreencastParamsSchemeBugIssue =
        "https://github.com/microsoft/playwright-dotnet/issues/3302";

    private readonly IPage page;
    private readonly string artifactsDir;
    private string? recordingAbsolutePath;
    private bool isRunning;
    private ScreencastFailureContext? failureContext;
    private bool warnedKnownScreencastParamsSchemeBug = false;

    public ScreencastRecorder(IPage page, string artifactsDir)
    {
        this.page = page;
        this.artifactsDir = artifactsDir;
    }

    public async Task StartAsync()
    {
        Directory.CreateDirectory(this.artifactsDir);

        var testName = ArtifactUtilities.SanitizeFileName(TestContext.CurrentContext.Test.Name);
        var relativePath = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{testName}.webm";
        var screencastPath = Path.Combine(this.artifactsDir, relativePath);
        this.recordingAbsolutePath = Path.GetFullPath(screencastPath);

        await this.page.Screencast.StartAsync(new()
        {
            Path = this.recordingAbsolutePath
        });

        this.StartTimedTitleBannerIgnoringErrors();

        this.isRunning = true;
    }

    private void StartTimedTitleBannerIgnoringErrors()
    {
        _ = TimedTitleBannerIgnoringErrorsAsync();

        async Task TimedTitleBannerIgnoringErrorsAsync()
        {
            try
            {
                await this.ShowTitleOverlayAsync();
            }
            catch (Exception exception) when (exception is PlaywrightException playwrightException && !IsKnownScreencastParamsSchemeBug(playwrightException))
            {
                TestContext.WriteLine($"Failed to show screencast title banner: {exception.Message}");
            }
        }
    }

    /// <summary>
    /// Playwright (e.g. v1.59.0) can throw <see cref="PlaywrightException"/> with
    /// "Unknown scheme for Params" for screencast overlays/chapters; see issue #3302.
    /// </summary>
    private static bool IsKnownScreencastParamsSchemeBug(PlaywrightException exception)
    {
        return exception.Message.Contains(
            "Unknown scheme for Params",
            StringComparison.Ordinal);
    }

    private void WarnKnownScreencastParamsSchemeBugIfFirstInThisRecording()
    {
        if (this.warnedKnownScreencastParamsSchemeBug)
        {
            // No need to warn more than once per recording
            return;
        }

        TestContext.WriteLine(
            "Warning: screencast overlays or chapters are unavailable because this Microsoft.Playwright version " +
            "has a known bug (screencast bad API bindings). " +
            $"See {KnownScreencastParamsSchemeBugIssue}. " +
            "Upgrade Microsoft.Playwright to a release that includes the fix as soon as it is published.");

        this.warnedKnownScreencastParamsSchemeBug = true;
    }

    /// <summary>
    /// Shows a top banner with the humanized test name for <see cref="TitleOverlayDurationMs"/> then lets Playwright remove it (instant removal after the timer).
    /// </summary>
    internal async Task ShowTitleOverlayAsync()
    {
        var title = ArtifactUtilities.HumanizeTestDisplayName(TestContext.CurrentContext.Test.Name);

        var overlayHtml = $$"""
            <style>
                #test-title-bar {
                    position: fixed;
                    top: 0;
                    left: 0;
                    right: 0;
                    box-sizing: border-box;
                    background: linear-gradient(180deg, #0f172a 0%, #020617 100%);
                    border-bottom: solid 4px #fbbf24;
                    box-shadow:
                        inset 0 1px 0 rgba(255, 255, 255, 0.12),
                        0 6px 0 rgba(0, 0, 0, 0.55),
                        0 12px 28px rgba(0, 0, 0, 0.45);
                    color: #fff;
                    font-family: system-ui, -apple-system, Segoe UI, sans-serif;
                    font-size: 20px;
                    font-weight: 800;
                    letter-spacing: 0.02em;
                    line-height: 1.3;
                    padding: 14px 22px 16px;
                    overflow: hidden;
                    text-overflow: ellipsis;
                    white-space: nowrap;
                    text-shadow:
                        0 2px 0 rgba(0, 0, 0, 0.85),
                        0 0 22px rgba(0, 0, 0, 0.9);
                    z-index: 2147483647;
                }
            </style>
            <div id="test-title-bar">{{WebUtility.HtmlEncode(title)}}</div>
            """;

        await this.ShowOverlayAsync(overlayHtml, new() { Duration = TitleOverlayDurationMs });
    }

    public async Task StopAsync(TestStatus testStatus)
    {
        if (!this.isRunning)
        {
            return;
        }

        this.isRunning = false;

        switch (testStatus)
        {
            case TestStatus.Passed:
                await this.ShowPassedOverlayAsync();
                break;
            case TestStatus.Failed:
                await this.ShowFailureOverlayAsync();
                break;
            default:
                await this.ShowInconclusiveOverlayAsync(testStatus);
                break;
        }

        await this.page.Screencast.StopAsync();

        TryMoveRecordingToOutcomeSubfolder(testStatus);
    }

    private void TryMoveRecordingToOutcomeSubfolder(TestStatus testStatus)
    {
        if (string.IsNullOrEmpty(this.recordingAbsolutePath))
        {
            return;
        }

        var subfolderName = testStatus.ToString();

        try
        {
            if (!File.Exists(this.recordingAbsolutePath))
            {
                return;
            }

            var destinationDir = Path.Combine(this.artifactsDir, subfolderName);
            Directory.CreateDirectory(destinationDir);
            var destinationPath = Path.Combine(destinationDir, Path.GetFileName(this.recordingAbsolutePath));
            File.Move(this.recordingAbsolutePath, destinationPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TestContext.WriteLine($"Failed to move screencast file into '{subfolderName}': {exception.Message}");
        }
    }

    /// <summary>Chapter overlay at the start of a test stage (same role as opening a trace group).</summary>
    internal async Task AnnotateStageBeginningAsync(TestStage stage, string description)
    {
        await this.ShowChapterAsync(stage.ToChapterTitle(), new() { Description = description, Duration = 2000 });
    }

    internal Task ConcludeStageAsync(TestStage stage, string description) => Task.CompletedTask;

    /// <summary>After a failing stage body, records context for the screencast outcome overlay (failure or arrange inconclusive).</summary>
    internal void StoreFailureContextForFinalOverlay(TestStage stage, string description, Exception exception)
    {
        this.failureContext = new ScreencastFailureContext(stage, description, exception.Message);
    }

    private string HumanizedTestTitleHtml() =>
        WebUtility.HtmlEncode(ArtifactUtilities.HumanizeTestDisplayName(TestContext.CurrentContext.Test.Name));

    private async Task ShowFailureOverlayAsync()
    {
        string? subtitlePlain =
            this.failureContext?.Stage switch
            {
                TestStage.Act => FailureOverlayExplanationActStage,
                TestStage.Assert => FailureOverlayExplanationAssertStage,
                _ => null,
            };

        var details =
            ArtifactUtilities.NormalizeFailureDetails(
                this.failureContext?.Details ?? TestContext.CurrentContext.Result.Message);

        await this.ShowOutcomeOverlayAsync(
            new OutcomeView(
                ThemeFailure,
                TestStatus.Failed.ToString().ToUpper(),
                subtitlePlain,
                this.failureContext?.Description,
                details));
    }

    private async Task ShowPassedOverlayAsync()
    {
        await this.ShowOutcomeOverlayAsync(
            new OutcomeView(
                ThemePassed,
                TestStatus.Passed.ToString().ToUpper(),
                null,
                null,
                string.Empty));
    }

    private async Task ShowInconclusiveOverlayAsync(TestStatus outcomeStatus)
    {
        var isArrangeInconclusive = this.failureContext?.Stage == TestStage.Arrange;

        string? subtitlePlain = null;
        string? descriptionPlain = null;
        string? detailsSource;

        if (isArrangeInconclusive)
        {
            subtitlePlain =
                "The failure occurred while preparing the scenario, so this recording does not support conclusions about the feature this test targets.";
            descriptionPlain = this.failureContext!.Description;
            detailsSource =
                this.failureContext.Details ?? TestContext.CurrentContext.Result.Message;
        }
        else
        {
            detailsSource = TestContext.CurrentContext.Result.Message;
        }

        var details = ArtifactUtilities.NormalizeFailureDetails(detailsSource);

        await this.ShowOutcomeOverlayAsync(
            new OutcomeView(
                ThemeInconclusive,
                outcomeStatus.ToString().ToUpper(),
                subtitlePlain,
                descriptionPlain,
                details));
    }

    private async Task ShowOutcomeOverlayAsync(OutcomeView view)
    {
        var humanTitleHtml = this.HumanizedTestTitleHtml();
        var subtitleHtml = WrapOptionalPlainLineAsSubtitle(view.SubtitlePlain);
        var descriptionHtml = WrapOptionalPlainLineAsDescription(view.DescriptionPlain);

        var overlayHtml = BuildOutcomeOverlayHtml(
            view.Theme,
            humanTitleHtml,
            WebUtility.HtmlEncode(view.BadgePlain),
            subtitleHtml,
            descriptionHtml,
            view.DetailsPlain);

        await this.ShowOverlayAsync(overlayHtml, new() { Duration = OutcomeOverlayDurationMs });
    }

    private async Task ShowOverlayAsync(string overlayHtml, ScreencastShowOverlayOptions options)
    {
        try
        {
            await this.page.Screencast.ShowOverlayAsync(overlayHtml, options);
        }
        catch (PlaywrightException exception) when (IsKnownScreencastParamsSchemeBug(exception))
        {
            this.WarnKnownScreencastParamsSchemeBugIfFirstInThisRecording();
        }
    }

    private async Task ShowChapterAsync(string chapterTitle, ScreencastShowChapterOptions options)
    {
        try
        {
            await this.page.Screencast.ShowChapterAsync(chapterTitle, options);
        }
        catch (PlaywrightException exception) when (IsKnownScreencastParamsSchemeBug(exception))
        {
            this.WarnKnownScreencastParamsSchemeBugIfFirstInThisRecording();
        }
    }

    private sealed record OutcomeView(
        OutcomeChrome Theme,
        string BadgePlain,
        string? SubtitlePlain,
        string? DescriptionPlain,
        string DetailsPlain);

    private static string WrapOptionalPlainLineAsSubtitle(string? plain) =>
        string.IsNullOrWhiteSpace(plain)
            ? string.Empty
            : $"<div id=\"subtitle\">{WebUtility.HtmlEncode(plain)}</div>";

    private static string WrapOptionalPlainLineAsDescription(string? plain) =>
        string.IsNullOrWhiteSpace(plain)
            ? string.Empty
            : $"<div id=\"description\">{WebUtility.HtmlEncode(plain)}</div>";

    private record OutcomeChrome(string ContentBackground, string ContentBorderColor, string SubtitleColorClass);

    private static readonly OutcomeChrome ThemeFailure = new (
        ContentBackground: "rgba(64, 0, 0, 0.88)",
        ContentBorderColor: "rgba(255, 255, 255, 0.22)",
        SubtitleColorClass: "#fff");

    private static readonly OutcomeChrome ThemePassed = new (
        ContentBackground: "rgba(0, 64, 40, 0.9)",
        ContentBorderColor: "rgba(200, 255, 230, 0.28)",
        SubtitleColorClass: "rgba(255, 255, 255, 0.94)");

    private static readonly OutcomeChrome ThemeInconclusive = new (
        ContentBackground: "rgba(120, 53, 15, 0.92)",
        ContentBorderColor: "rgba(253, 186, 116, 0.45)",
        SubtitleColorClass: "rgba(255, 237, 213, 0.96)");

    /// <summary>Merges fragments into the outcome card markup. <paramref name="humanTitleEscaped"/> and <paramref name="badgeHtml"/> must already be HTML-safe. <paramref name="detailsPlain"/> is plain text and is HTML-encoded when emitted. <paramref name="subtitleHtml"/> and <paramref name="descriptionHtml"/> are inserted without further encoding.</summary>
    private static string BuildOutcomeOverlayHtml(
        OutcomeChrome theme,
        string humanTitleEscaped,
        string badgeHtml,
        string subtitleHtml,
        string descriptionHtml,
        string detailsPlain)
    {
        var detailsBlock = string.IsNullOrWhiteSpace(detailsPlain)
            ? string.Empty
            : $"<pre id=\"details\">{WebUtility.HtmlEncode(detailsPlain)}</pre>";

        return $$"""
            <style>
                #background {
                    position: absolute;
                    inset: 0;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    backdrop-filter: blur(2px);
                }

                #content {
                    background: {{theme.ContentBackground}};
                    border: 1px solid {{theme.ContentBorderColor}};
                    border-radius: 16px;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.45);
                    color: white;
                    font-family: system-ui, -apple-system, sans-serif;
                    max-width: 760px;
                    padding: 32px 40px;
                }

                #human {
                    font-size: 21px;
                    font-weight: 600;
                    line-height: 1.35;
                    margin-bottom: 10px;
                    text-align: center;
                    color: rgba(255, 255, 255, 0.96);
                }

                #badge {
                    font-size: 30px;
                    font-weight: 800;
                    letter-spacing: 0.04em;
                    line-height: 1.25;
                    margin-bottom: 14px;
                    text-align: center;
                    color: rgba(255, 255, 255, 0.99);
                }

                #subtitle {
                    font-size: 17px;
                    font-weight: 600;
                    line-height: 1.35;
                    margin-bottom: 10px;
                    text-align: center;
                    color: {{theme.SubtitleColorClass}};
                    opacity: 0.93;
                }

                #description {
                    color: rgba(255, 255, 255, 0.88);
                    font-size: 16px;
                    font-weight: 500;
                    line-height: 1.45;
                    margin-bottom: 16px;
                    text-align: center;
                }

                #details {
                    background: rgba(0, 0, 0, 0.34);
                    border-radius: 10px;
                    font-family: Consolas, "SFMono-Regular", monospace;
                    font-size: 13px;
                    line-height: 1.45;
                    margin: 0;
                    max-height: 340px;
                    overflow: hidden;
                    overflow-wrap: anywhere;
                    padding: 16px;
                    white-space: pre-wrap;
                }
            </style>
            <div id="background">
                <div id="content">
                    <div id="human">{{humanTitleEscaped}}</div>
                    <div id="badge">{{badgeHtml}}</div>
                    {{subtitleHtml}}
                    {{descriptionHtml}}
                    {{detailsBlock}}
                </div>
            </div>
            """;
    }

    private sealed record ScreencastFailureContext(TestStage Stage, string Description, string Details);
}
