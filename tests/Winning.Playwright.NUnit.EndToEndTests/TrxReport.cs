using System.Xml.Linq;

namespace Winning.Playwright.NUnit.EndToEndTests;

/// <summary>
/// Utility helpers for reading data from a TRX report file produced by <c>dotnet test --logger trx</c>.
/// </summary>
internal static class TrxReport
{
    private static readonly XNamespace Ns =
        XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

    /// <summary>
    /// Returns the first <c>.trx</c> file found in <paramref name="trxDir"/>,
    /// or <see langword="null"/> if the directory contains none.
    /// </summary>
    public static string? FindFile(string trxDir) =>
        Directory.GetFiles(trxDir, "*.trx").FirstOrDefault();

    /// <summary>
    /// Returns all <c>ResultFile path</c> values from the TRX file (the attachment paths
    /// registered via <c>TestContext.AddTestAttachment</c>).
    /// </summary>
    public static IReadOnlyList<string> ReadAttachmentPaths(string trxPath) =>
        XDocument.Load(trxPath)
                 .Descendants(Ns + "ResultFile")
                 .Select(e => e.Attribute("path")?.Value ?? string.Empty)
                 .Where(p => !string.IsNullOrEmpty(p))
                 .ToList()
                 .AsReadOnly();
}
