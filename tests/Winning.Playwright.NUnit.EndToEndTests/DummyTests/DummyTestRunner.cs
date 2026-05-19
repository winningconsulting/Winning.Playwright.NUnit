using System.Diagnostics;

namespace Winning.Playwright.NUnit.EndToEndTests.DummyTests;

/// <summary>
/// Starts a <c>dotnet test</c> subprocess that runs <see cref="DummyTests"/> — either a specific
/// test method or all of them — with a dedicated artifacts directory and TRX output directory.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="WithEnvironmentVariable"/> to set any environment variables (e.g.
/// <c>WINNING_*</c>) before calling <see cref="RunAsync"/>.
/// </para>
/// <para>Dispose to delete the temporary directories created for the run.</para>
/// </remarks>
internal sealed class DummyTestRunner : IDisposable
{
    private readonly string? _testMethodName;
    private readonly Dictionary<string, string?> _envOverrides = [];
    private bool _disposed;

    /// <param name="testMethodName">
    /// The name of the single test method to run, or <see langword="null"/> to run all
    /// <see cref="DummyTests"/> methods.
    /// </param>
    public DummyTestRunner(string? testMethodName = null)
    {
        _testMethodName = testMethodName;
        ArtifactsDir = Path.Combine(Path.GetTempPath(), $"DummyArtifacts_{Guid.NewGuid():N}");
        TrxDir = Path.Combine(Path.GetTempPath(), $"DummyTrx_{Guid.NewGuid():N}");
    }

    /// <summary>Directory where the dummy test writes its screencast and trace artifacts.</summary>
    public string ArtifactsDir { get; }

    /// <summary>Directory where <c>dotnet test --logger trx</c> writes the TRX output file.</summary>
    public string TrxDir { get; }

    /// <summary>
    /// Overrides or adds an environment variable for the subprocess.
    /// Call before <see cref="RunAsync"/>.
    /// </summary>
    public DummyTestRunner WithEnvironmentVariable(string key, string? value)
    {
        _envOverrides[key] = value;
        return this;
    }

    /// <summary>
    /// Runs the dummy test(s) in a subprocess and returns the <c>dotnet test</c> exit code.
    /// </summary>
    /// <returns>
    /// 0 when all tests passed or were inconclusive; 1 when at least one failed; 2 when no tests were found.
    /// </returns>
    /// <exception cref="TimeoutException">The subprocess exceeded the 3-minute timeout.</exception>
    public async Task<int> RunAsync(bool suppressSubprocessOutput = true)
    {
        Directory.CreateDirectory(ArtifactsDir);
        Directory.CreateDirectory(TrxDir);

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = suppressSubprocessOutput,
            RedirectStandardError = suppressSubprocessOutput,
            UseShellExecute = false, // avoid interference from OS shell context
        };
        psi.ArgumentList.Add("test");
        psi.ArgumentList.Add(typeof(DummyTestRunner).Assembly.Location);
        psi.ArgumentList.Add("--filter");
        psi.ArgumentList.Add(BuildFilter());
        psi.ArgumentList.Add("--logger");
        psi.ArgumentList.Add("trx");
        psi.ArgumentList.Add("--results-directory");
        psi.ArgumentList.Add(TrxDir);
        psi.ArgumentList.Add("--no-build");

        psi.Environment["WINNING_E2E_DUMMY_SUBPROCESS"] = "1";

        foreach (var (key, value) in _envOverrides)
            psi.Environment[key] = value;

        using var process = Process.Start(psi)!;
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The dummy test subprocess exceeded the 3-minute timeout.");
        }

        return process.ExitCode;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        TryDeleteDirectory(ArtifactsDir);
        TryDeleteDirectory(TrxDir);
    }

    private string BuildFilter()
    {
        var fixtureFullName = typeof(DummyTests).FullName!;
        return _testMethodName is not null
            ? $"FullyQualifiedName={fixtureFullName}.{_testMethodName}"
            : $"FullyQualifiedName~{fixtureFullName}";
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try { Directory.Delete(path, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
