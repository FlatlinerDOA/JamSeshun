using JamSeshun.Services;

namespace JamSeshun.Tests;

public class TabLibraryServiceSpec : IDisposable
{
    private readonly TabLibraryService svc;
    private readonly string dbDir;

    public TabLibraryServiceSpec()
    {
        // Use a unique temp directory per test run so tests don't share state.
        this.dbDir = Path.Combine(Path.GetTempPath(), $"jamseshun-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(this.dbDir);
        this.svc = new TabLibraryService(this.dbDir);
    }

    public void Dispose()
    {
        this.svc.Dispose();
        try { Directory.Delete(this.dbDir, true); } catch { /* best-effort cleanup */ }
    }

    private void Seed(params (string Artist, string Song)[] entries)
    {
        foreach (var (artist, song) in entries)
        {
            var tab = new SavedTab(artist, song, string.Empty);
            this.svc.Save(Guid.NewGuid(), $"{artist} - {song}", tab);
        }
    }

    [Fact]
    public void Search_SingleTerm_MatchesSubstring()
    {
        this.Seed(("David Gray", "Babylon"), ("Ed Sheeran", "Perfect"));
        var results = this.svc.Search("babylon").ToList();
        Assert.Single(results);
        Assert.Contains(results, r => r.Name == "David Gray - Babylon");
    }

    [Fact]
    public void Search_MultipleTerms_RequiresAll()
    {
        this.Seed(("David Gray", "Babylon"), ("David Bowie", "Babylon"), ("Ed Sheeran", "Perfect"));
        // "gray babylon" should match "David Gray - Babylon" but NOT "David Bowie - Babylon"
        var results = this.svc.Search("gray babylon").ToList();
        Assert.Single(results);
        Assert.Equal("David Gray - Babylon", results[0].Name);
    }

    [Fact]
    public void Search_MultipleTerms_NarrowsAsTermsAdded()
    {
        this.Seed(("David Gray", "Babylon"), ("David Gray", "Sail Away"), ("David Bowie", "Space Oddity"));
        Assert.Equal(2, this.svc.Search("david gray").Count());
        Assert.Single(this.svc.Search("david gray babylon"));
        Assert.Empty(this.svc.Search("david gray babylon bowie")); // no entry has all four
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        this.Seed(("David Gray", "Babylon"));
        Assert.Single(this.svc.Search("BABYLON"));
        Assert.Single(this.svc.Search("david GRAY"));
    }

    [Fact]
    public void Search_ExtraWhitespace_IsIgnored()
    {
        this.Seed(("David Gray", "Babylon"));
        Assert.Single(this.svc.Search("  babylon  "));
    }
}
