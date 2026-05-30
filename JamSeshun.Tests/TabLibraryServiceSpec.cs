using JamSeshun.Services;

namespace JamSeshun.Tests;

public class TabLibraryServiceSpec : IDisposable
{
    private readonly TabLibraryService _svc;
    private readonly string _dbDir;

    public TabLibraryServiceSpec()
    {
        // Use a unique temp directory per test run so tests don't share state.
        _dbDir = Path.Combine(Path.GetTempPath(), $"jamseshun-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_dbDir);
        _svc = new TabLibraryService(_dbDir);
    }

    public void Dispose()
    {
        _svc.Dispose();
        try { Directory.Delete(_dbDir, true); } catch { /* best-effort cleanup */ }
    }

    private void Seed(params (string Artist, string Song)[] entries)
    {
        foreach (var (artist, song) in entries)
        {
            var tab = new SavedTab(artist, song, string.Empty);
            _svc.Save(Guid.NewGuid(), $"{artist} - {song}", tab);
        }
    }

    [Fact]
    public void Search_SingleTerm_MatchesSubstring()
    {
        Seed(("David Gray", "Babylon"), ("Ed Sheeran", "Perfect"));
        var results = _svc.Search("babylon").ToList();
        Assert.Single(results);
        Assert.Contains(results, r => r.Name == "David Gray - Babylon");
    }

    [Fact]
    public void Search_MultipleTerms_RequiresAll()
    {
        Seed(("David Gray", "Babylon"), ("David Bowie", "Babylon"), ("Ed Sheeran", "Perfect"));
        // "gray babylon" should match "David Gray - Babylon" but NOT "David Bowie - Babylon"
        var results = _svc.Search("gray babylon").ToList();
        Assert.Single(results);
        Assert.Equal("David Gray - Babylon", results[0].Name);
    }

    [Fact]
    public void Search_MultipleTerms_NarrowsAsTermsAdded()
    {
        Seed(("David Gray", "Babylon"), ("David Gray", "Sail Away"), ("David Bowie", "Space Oddity"));
        Assert.Equal(2, _svc.Search("david gray").Count());
        Assert.Single(_svc.Search("david gray babylon"));
        Assert.Empty(_svc.Search("david gray babylon bowie")); // no entry has all four
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        Seed(("David Gray", "Babylon"));
        Assert.Single(_svc.Search("BABYLON"));
        Assert.Single(_svc.Search("david GRAY"));
    }

    [Fact]
    public void Search_ExtraWhitespace_IsIgnored()
    {
        Seed(("David Gray", "Babylon"));
        Assert.Single(_svc.Search("  babylon  "));
    }
}
