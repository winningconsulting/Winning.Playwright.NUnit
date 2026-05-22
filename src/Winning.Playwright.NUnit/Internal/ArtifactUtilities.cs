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

    /// <summary>
    /// Registers <paramref name="artifactAbsolutePath"/> as a test attachment using a file name derived from
    /// <paramref name="description"/> (plus the artifact extension), so loggers that ignore NUnit's description
    /// (e.g. TRX) still show a short label. The artifact file on disk is unchanged; a symlink or copy
    /// provides the friendly path passed to <see cref="TestContext.AddTestAttachment"/>.
    /// </summary>
    internal static void AddTestAttachmentWithFriendlyFileName(string artifactAbsolutePath, string description)
    {
        var directory = Path.GetDirectoryName(artifactAbsolutePath)
            ?? throw new InvalidOperationException("Artifact path has no directory.");
        var extension = Path.GetExtension(artifactAbsolutePath);
        var aliasFileName = SanitizeFileName(description) + extension;
        var aliasAbsolutePath = Path.GetFullPath(Path.Combine(directory, aliasFileName));

        if (File.Exists(aliasAbsolutePath))
        {
            File.Delete(aliasAbsolutePath);
        }

        CreateAttachmentAlias(artifactAbsolutePath, aliasAbsolutePath);

        var relPath = Path.GetRelativePath(TestContext.CurrentContext.WorkDirectory, aliasAbsolutePath);
        TestContext.AddTestAttachment(relPath, description);
    }

    private static void CreateAttachmentAlias(string sourceAbsolutePath, string aliasAbsolutePath)
    {
        try
        {
            File.CreateSymbolicLink(aliasAbsolutePath, sourceAbsolutePath);
            return;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        File.Copy(sourceAbsolutePath, aliasAbsolutePath, overwrite: true);
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
