using System;
using System.IO;

namespace FairShare.Tests.Web;

/// <summary>
/// The Blazor app ships a copy of the locked design tokens (the web Docker build's
/// context excludes design-system/, so a physical copy is the only shape that works
/// everywhere). A copy invites drift; this test makes drift a failing build: the two
/// files must stay byte-identical, and a token change lands in the design system first
/// (where the WCAG contrast test gates it), then gets re-copied here.
/// </summary>
public class WarmCounselTokenSyncTests
{
    [Fact]
    public void AppTokensFile_IsByteIdenticalToTheDesignSystemTokens()
    {
        string root = FindRepoRoot();
        string designTokens = Path.Combine(root, "design-system", "src", "styles", "tokens.css");
        string appTokens = Path.Combine(root, "src", "FairShare.Web", "wwwroot", "css", "warm-counsel-tokens.css");

        Assert.True(File.Exists(designTokens), $"Missing {designTokens}");
        Assert.True(File.Exists(appTokens), $"Missing {appTokens}");

        Assert.Equal(File.ReadAllBytes(designTokens), File.ReadAllBytes(appTokens));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FairShare.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("FairShare.sln not found above the test directory.");
    }
}
