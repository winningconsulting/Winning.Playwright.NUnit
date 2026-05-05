using System;

namespace Winning.Playwright.NUnit;

/// <summary>
/// Arrange / Act / Assert stage discriminator for annotations and failure context.
/// </summary>
public enum TestStage
{
    Arrange,
    Act,
    Assert,
}

/// <summary>
/// Maps <see cref="TestStage"/> to labels used for screencast chapters and trace group titles.
/// </summary>
public static class TestStageExtensions
{
    /// <summary>Uppercase title used for screencast chapters and as the prefix in trace groups (<c>STAGE: description</c>).</summary>
    public static string ToChapterTitle(this TestStage stage) =>
        stage switch
        {
            TestStage.Arrange => "ARRANGE",
            TestStage.Act => "ACT",
            TestStage.Assert => "ASSERT",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
        };
}
