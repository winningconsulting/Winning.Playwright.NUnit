using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Winning.Playwright.NUnit.Tests;

[TestFixture]
public sealed class WinningPageTestScreencastSmokeTests : WinningPageTest
{
    protected override bool IsScreencastEnabled => true;

    protected override bool IsTracingEnabled => true;

    protected override bool TraceAllTests => true;

    [Test]
    public async Task Smoke_Arrange_Act_AssertThat_Succeeds()
    {
        await this.ArrangeStage("Open blank page", OpenBlankPage);
        await this.ActStage("Pause briefly", PauseBriefly);
        await this.AssertStage(
            "URL starts with about:",
            () =>
            {
                Assert.That(this.Page.Url, Does.StartWith("about:blank"));
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Ends inconclusive during <see cref="WinningPageTest.ArrangeStage"/> (assertion in the arrange callback). Run manually to inspect screencast/trace for arrange-stage outcomes.
    /// </summary>
    [Test]
    [Explicit("Intentional inconclusive in arrange; run manually with explicit tests enabled.")]
    public async Task Smoke_Fails_On_Arrange()
    {
        await this.ArrangeStage(
            "Click on an non-existing node",
            ClickMissingNode);
    }

    /// <summary>
    /// Fails during <see cref="WinningPageTest.ActStage"/> (Playwright timeout). Run manually to inspect act-stage failures.
    /// </summary>
    [Test]
    [Explicit("Intentional failure in act; run manually with explicit tests enabled.")]
    public async Task Smoke_Fails_On_Act()
    {
        await this.ArrangeStage("Open blank page", OpenBlankPage);
        await this.ActStage("Click missing node", ClickMissingNode);
    }

    /// <summary>
    /// Fails during <see cref="WinningPageTest.AssertStage"/>. Run manually to inspect assert-stage failures.
    /// </summary>
    [Test]
    [Explicit("Intentional failure in assert; run manually with explicit tests enabled.")]
    public async Task Smoke_assert_fails_on_assertion()
    {
        await this.ArrangeStage("Open blank page", OpenBlankPage);
        await this.ActStage("Pause briefly", PauseBriefly);
        await this.AssertStage(
            "2 + 2 is equal to 5",
            () =>
            {
                Assert.That(2 + 2, Is.EqualTo(5));
                return Task.CompletedTask;
            });
    }

    private async Task ClickMissingNode() => await this.Page.ClickAsync("#__smoke_missing__", new PageClickOptions { Timeout = 50 });

    private async Task OpenBlankPage() => await this.Page.GotoAsync("about:blank");

    private async Task PauseBriefly() => await Task.Delay(1000);

}
