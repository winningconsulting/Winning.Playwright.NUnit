using Microsoft.Playwright;

namespace Winning.Playwright.NUnit.EndToEndTests.DummyTests;

/// <summary>
/// Target fixture for the end-to-end tests — each method produces a distinct outcome so that
/// the end-to-end tests can verify recording, tracing, and TRX attachment behaviour per outcome.
/// Invoked one method at a time by <see cref="DummyTestRunner"/> via subprocess.
/// </summary>
[TestFixture, Explicit("Dummy test ignored, only executed via subprocess")]
public sealed class DummyTests : WinningPageTest
{
    /// <summary>Completes all three stages without errors, producing a <c>Passed</c> outcome.</summary>
    [Test]
    public async Task PassesAllStages()
    {
        await this.ArrangeStage("Open blank page", async () => await this.Page.GotoAsync("about:blank"));
        await this.ActStage("Pause briefly", async () => await Task.Delay(500));
        await this.AssertStage("URL starts with about:", () =>
        {
            Assert.That(this.Page.Url, Does.StartWith("about:"));
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Fails inside <c>ArrangeStage</c>, which <see cref="WinningPageTest"/> maps to an
    /// <c>Inconclusive</c> outcome.
    /// </summary>
    [Test]
    public async Task FailsDuringArrangeStage()
    {
        await this.ArrangeStage(
            "Click missing node",
            async () => await this.Page.ClickAsync("#__missing__", new PageClickOptions { Timeout = 50 }));
    }

    /// <summary>Fails inside <c>ActStage</c> (Playwright timeout), producing a <c>Failed</c> outcome.</summary>
    [Test]
    public async Task FailsDuringActStage()
    {
        await this.ArrangeStage("Open blank page", async () => await this.Page.GotoAsync("about:blank"));
        await this.ActStage(
            "Click missing node",
            async () => await this.Page.ClickAsync("#__missing__", new PageClickOptions { Timeout = 50 }));
    }

    /// <summary>Fails inside <c>AssertStage</c> (NUnit assertion), producing a <c>Failed</c> outcome.</summary>
    [Test]
    public async Task FailsDuringAssertStage()
    {
        await this.ArrangeStage("Open blank page", async () => await this.Page.GotoAsync("about:blank"));
        await this.ActStage("Pause briefly", async () => await Task.Delay(500));
        await this.AssertStage("2 + 2 equals 5", () =>
        {
            Assert.That(2 + 2, Is.EqualTo(5));
            return Task.CompletedTask;
        });
    }
}
