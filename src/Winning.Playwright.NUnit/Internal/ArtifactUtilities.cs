using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Winning.Playwright.NUnit.Internal;

internal static partial class ArtifactUtilities
{
    internal const int MaxFailureMessageLength = 1800;

    internal static string NormalizeFailureDetails(string? failureMessage)
    {

        if (string.IsNullOrWhiteSpace(failureMessage))
        {
            return "No failure message was available.";
        }

        var normalizedFailureMessage = failureMessage.Trim()
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        if (normalizedFailureMessage.Length <= MaxFailureMessageLength)
        {
            return normalizedFailureMessage;
        }

        return $"{normalizedFailureMessage[..MaxFailureMessageLength]}\n...";
    }

    internal static string SanitizeFileName(string name)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidCharacter, '_');
        }

        return name;
    }

    /// <summary>
    /// Turns typical NUnit / C# test names into readable phrases (underscores → spaces; PascalCase breaks).
    /// </summary>
    internal static string HumanizeTestDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var spaced = UnderscoresOrDotsOrHyphens().Replace(name, " ");
        spaced = LowerThenUpper().Replace(spaced, " ");
        spaced = UpperUpperLower().Replace(spaced, " ");
        return CollapseSpaces().Replace(spaced.Trim(), " ");
    }

    internal static string BuildArtifactFileName(string fileNamePrefix, string extension)
    {
        var currentTest = TestContext.CurrentContext.Test;
        var testId = SanitizeFileName(currentTest.ID);
        var testName = SanitizeFileName(currentTest.Name);
        return $"{SanitizeFileName(fileNamePrefix)} - {DateTime.Now:yyyyMMdd_HHmmss_fff}_{testId}_{testName}{extension}";
    }

    /// <summary>
    /// Registers <paramref name="artifactAbsolutePath"/> as a test attachment using a path relative to
    /// NUnit's work directory.
    /// </summary>
    internal static void AddTestAttachment(string artifactAbsolutePath, string description)
    {
        var relPath = Path.GetRelativePath(TestContext.CurrentContext.WorkDirectory, artifactAbsolutePath);
        TestContext.AddTestAttachment(relPath, description);
    }

    [GeneratedRegex(@"[_\-.,]+")]
    private static partial Regex UnderscoresOrDotsOrHyphens();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex LowerThenUpper();

    [GeneratedRegex("(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex UpperUpperLower();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseSpaces();

}
