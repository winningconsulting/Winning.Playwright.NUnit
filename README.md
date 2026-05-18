# Winning.Playwright.NUnit

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Helper library for [Playwright](https://playwright.dev/dotnet/) tests on [NUnit](https://nunit.org/).

Features:

* **Formal Test Stages**: Allows you to define and document formally each step of a test as Arrange, Act or Assert stages
* **Tracing**: Automatic Playwright trace ZIPs grouping and annotating each stage.
* **Screencast**: Automatic WebM screencasts with documenting overlays for each stage.
* **Inconclusive Detection**: Automatic detection of inconclusive tests for failures in an arrange stage.

## Install

NuGet packages are not published yet. For now, consume this library by adding it as a **Git submodule**, then add a **project reference** from your test project.

### 1. Add this repository as a submodule

From your repository root, choose a directory for the submodule (the examples use `externals/Winning.Playwright.NUnit`; adjust if you prefer another path).

```bash
git submodule add https://github.com/winningconsulting/Winning.Playwright.NUnit.git externals/Winning.Playwright.NUnit
```

Afterwards, and also anytime someone clones your repository, it must initialize the submodules:

```bash
git submodule update --init --recursive
```

### 2. Reference this library from your test project

In your test `.csproj`, add a `ProjectReference` whose path matches where you put the submodule relative to that project:

```xml
<ItemGroup>
  <ProjectReference Include="..\externals\Winning.Playwright.NUnit\src\Winning.Playwright.NUnit\Winning.Playwright.NUnit.csproj" />
</ItemGroup>
```

Restore and build as usual.

## Usage

Create a fixture (or use the base directly) by inheriting **`WinningPageTest`**:

```csharp
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using Winning.Playwright.NUnit;

namespace MyApp.E2E;

[TestFixture]
public sealed class LoginTests : WinningPageTest
{
    [Test]
    public async Task User_can_log_in()
    {
        await ArrangeStage("Open sign-in", async () => await Page.GotoAsync("https://example.com/login"));
        await ActStage("Submit credentials", async () =>
        {
            await Page.GetByLabel("Email").FillAsync("user@example.com");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        });
        await AssertStage("Redirected to home", async () =>
            Assert.That(Page.Url, Does.Contain("/home")));
    }
}
```

### Environment

| Variable     | Purpose |
|-------------|---------|
| **`WINNING_RECORD_SCREENCAST`** | When set to **`1`**, **`true`**, or **`yes`** (case-insensitive), enables WebM screencast, chapter overlays, and end-of-test result overlay. If unset or any other value, screencast is off ( **`Arrange` / `Act` / `AssertThat` still run the steps** with no overlays). Override **`IsScreencastEnabled`** on **`WinningPageTest`** if you want to force behavior from code. |
| **`WINNING_RECORD_TRACE`** | When truthy, starts **`Context.Tracing`** for each test with screenshots, DOM snapshots, and sources. On tear-down, a trace ZIP is written only for **failed** tests unless **`WINNING_TRACE_ALL_TESTS`** is set. Override **`IsTracingEnabled`**. |
| **`WINNING_TRACE_ALL_TESTS`** | When truthy (and tracing is enabled), every test produces a trace ZIP under **`WINNING_ARTIFACTS_DIR`**, in a subfolder named for that test's NUnit outcome (**`Passed`**, **`Failed`**, **`Skipped`**, **`Warning`**, etc.). When unset/false, only failures produce a ZIP under **`WINNING_ARTIFACTS_DIR`/`Failed`**. Override **`TraceAllTests`**. |
| **`WINNING_ARTIFACTS_DIR`** | Directory for screencast WebM files and trace ZIPs when those features are enabled. Default: **`./`**. Outcome subfolders use NUnit **`TestStatus`** names (**`Passed`**, **`Failed`**, **`Skipped`**, etc.) as needed. |

Screencast files are named `yyyyMMdd_HHmmss_fff_<sanitized test name>.webm` and moved into the outcome subfolder when recording stops. Trace ZIPs use the same timestamp pattern under the outcome folder rules above.

### Warning: Microsoft.Playwright 1.59.0 and full screencast overlays

Version 1.59.0 of .NET bindings for Playwright has bugs in the screencast API bindings (see [microsoft/playwright-dotnet#3302](https://github.com/microsoft/playwright-dotnet/issues/3302)).

If such bug occurs, this library degrades gracefully: base **WebM** capture still runs, overlays may be skipped, and tests may emit a **one-time** warning.

For a complete experience, you can:

1. Upgrade to a newer/fixed version as soon as it is published.
2. **Or** use a local build of latest `playwright-dotnet` which already has the fix:

   - Clone and build **`playwright-dotnet`** according to **[its repository instructions](https://github.com/microsoft/playwright-dotnet)**.
   - In **`src/Winning.Playwright.NUnit/Winning.Playwright.NUnit.csproj`**, switch the **`PackageReference`** of **`Microsoft.Playwright`** **1.59.0** with a **`ProjectReference`** to the **`Playwright`** project in your checkout (the repo ships a commented example you can uncomment and tune).

## API (helpers)

All extension points below are on **`WinningPageTest`** unless noted.

| Member | Description |
|--------|-------------|
| **`ArrangeStage(description, action)`** | With screencast: **ARRANGE** chapter, then `action`. On exception: records context for an **inconclusive** ending and calls **`Assert.Inconclusive`**. Without screencast: runs `action` only (same inconclusive behavior on error). |
| **`ActStage(description, action)`** | With screencast: **ACT** chapter, then `action`. Otherwise runs `action` only. |
| **`AssertStage(description, assertion)`** | With screencast: **ASSERT** chapter, then `assertion`. Otherwise runs `assertion` only. |
| **`IsScreencastEnabled`** | Override to force screencast on/off; default uses **`WINNING_RECORD_SCREENCAST`** or `false`. |
| **`IsTracingEnabled`** | Override to force tracing on/off; default uses **`WINNING_RECORD_TRACE`** or `false`. |
| **`TraceAllTests`** | Override to persist traces for every test vs failures only; default uses **`WINNING_TRACE_ALL_TESTS`** or `false`. |
| **`ArtifactsDirectory`** | Override to change artifact directory; default uses **`WINNING_ARTIFACTS_DIR`** or **`./`**. |

Failure messages in overlays are trimmed and newline-normalized; very long messages are truncated (internal max **1800** characters) before display.

## Build, test, pack

```bash
dotnet build
dotnet test
```

The fixture **`WinningPageTestScreencastSmokeTests`** turns screencast and tracing on in code (**`IsScreencastEnabled`**, **`IsTracingEnabled`**, **`TraceAllTests`**) so a successful run writes under **`Passed/`**. In a normal **`dotnet test`**, **only `Smoke_Arrange_Act_AssertThat_Succeeds`** executes (the non-explicit, passing smoke test). Three other tests whose names start with **`Smoke_`** are marked **`[Explicit]`** and **fail or go inconclusive on purpose** (one per stage: arrange, act, assert). They are **skipped by default** so typical CI or local runs stay green.

To run those **explicit** **`Smoke_*`** tests—only when you choose to—use a name filter and **`NUnit.RunExplicitTests=true`**. That runs all four **`Smoke_*`** methods; the three explicit demos **are expected to fail or inconclude**, so the run usually exits with failures unless you narrow the filter. You need browsers installed and a Chromium that supports the screencast overlay APIs:

```bash
dotnet test --filter "Name=~Smoke"
```

To package the library for NuGet:

```powershell
dotnet pack .\src\Winning.Playwright.NUnit\Winning.Playwright.NUnit.csproj -c Release
```
