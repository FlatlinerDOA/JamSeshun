using JamSeshun.Services;
using JamSeshun.ViewModels;

namespace JamSeshun.Tests;

public class TabChordDefinitionSpec
{
    private static IReadOnlyList<Chord> Chords(string content) =>
        new TabViewModel { Tab = new SavedTab("Artist", "Song", content) }.Chords;

    private static int[] FretsOf(string content, string name)
    {
        var frets = Chords(content).First(c => c.Name == name).Frets;
        Assert.NotNull(frets);
        return frets;
    }

    // ── Bracket-less "Name   frets" block (David Gray - Late Night Radio V2) ──

    private const string LateNightRadio =
        "Late Night Radio\n" +
        "Capo 4th\n\n" +
        "It is very important that you copy these chords patterns!\n" +
        "Am       x02210\n" +
        "Am7      x02010\n" +
        "Em       022010\n" +
        "Em7      020010\n" +
        "Fmaj7    x33210\n" +
        "Fsus2    x33010\n" +
        "G        320003\n" +
        "Dm       xx0231\n" +
        "C        x32010\n\n" +
        "Intro\n" +
        "Am Am7 Em Em7 Fmaj7 Fsus2 G\n";

    [Fact]
    public void BracketlessBlock_ParsesFretsForEveryDefinition()
    {
        Assert.Equal([-1, 0, 2, 2, 1, 0], FretsOf(LateNightRadio, "Am"));
        Assert.Equal([-1, 0, 2, 0, 1, 0], FretsOf(LateNightRadio, "Am7"));
        Assert.Equal([ 0, 2, 2, 0, 1, 0], FretsOf(LateNightRadio, "Em"));
        Assert.Equal([-1, 3, 3, 2, 1, 0], FretsOf(LateNightRadio, "Fmaj7"));
        Assert.Equal([ 3, 2, 0, 0, 0, 3], FretsOf(LateNightRadio, "G"));
        Assert.Equal([-1, -1, 0, 2, 3, 1], FretsOf(LateNightRadio, "Dm"));
    }

    [Fact]
    public void BracketlessBlock_DefinitionBeatsLibraryFallback()
    {
        // A non-standard G voicing in the file must win over ChordLibrary's open G.
        const string content = "G        355433\n\nVerse\nG  C  D\n";
        Assert.Equal([3, 5, 5, 4, 3, 3], FretsOf(content, "G"));
    }

    // ── Vertical chord diagram (David Gray - Jackdaw V1) ─────────────────────

    private const string Jackdaw =
        "D/F#\n" +
        "e-x\n" +
        "b-3\n" +
        "g-2\n" +
        "d-0\n" +
        "a-x\n" +
        "e-2\n\n" +
        "G\n" +
        "I'm like a jackdaw\n" +
        "C               D\n" +
        "Cawing at your backdoor\n";

    [Fact]
    public void VerticalDiagram_HighToLow_ParsesFrets()
    {
        // Rows e b g d a e (high→low) → [E A D G B e] = [2, x, 0, 2, 3, x]
        Assert.Equal([2, -1, 0, 2, 3, -1], FretsOf(Jackdaw, "D/F#"));
    }

    [Fact]
    public void VerticalDiagram_LowToHigh_ParsesFrets()
    {
        const string emDiagram =
            "Em\n" +
            "e-0\n" +   // low E
            "a-2\n" +
            "d-2\n" +
            "g-0\n" +
            "b-0\n" +
            "e-0\n\n" + // high e
            "Em  G  D\n";
        Assert.Equal([0, 2, 2, 0, 0, 0], FretsOf(emDiagram, "Em"));
    }

    [Fact]
    public void VerticalDiagram_DefinesChordEvenWhenBodyUsesOtherChords()
    {
        // D/F# only appears as the diagram; the body uses G, C, D.
        var names = Chords(Jackdaw).Select(c => c.Name).ToList();
        Assert.Contains("D/F#", names);
        Assert.Contains("G", names);
        Assert.Contains("C", names);
        Assert.Contains("D", names);
        // Diagram string rows must not leak in as chords.
        foreach (var row in new[] { "e", "b", "g", "d", "a" })
            Assert.DoesNotContain(row, names);
    }

    // ── Colon + bracket with no separating space (David Bowie - Space Oddity) ─

    [Fact]
    public void ColonBracket_NoSpace_ParsesFrets()
    {
        const string content =
            "Standard Tuning: E A D G B E\n" +
            "Chords:\n" +
            "Fmaj7:[xx3210]\n" +
            "D7/F#:[200212]\n\n" +
            "[Intro]\n" +
            "Fmaj7  D7/F#\n";
        Assert.Equal([-1, -1, 3, 2, 1, 0], FretsOf(content, "Fmaj7"));
        Assert.Equal([ 2,  0, 0, 2, 1, 2], FretsOf(content, "D7/F#"));
    }

    // ── Integration against the real WikiTab files ───────────────────────────

    private const string WikiTabDir = @"D:\ChizDev\WikiTab\Tabs";

    [Fact]
    public void RealFile_Jackdaw_DiagramDefinesDF()
    {
        var path = Path.Combine(WikiTabDir, "David Gray - Jackdaw V1.Chords.txt");
        if (!File.Exists(path)) return; // not available in CI

        Assert.Equal([2, -1, 0, 2, 3, -1], FretsOf(File.ReadAllText(path), "D/F#"));
    }

    [Fact]
    public void RealFile_LateNightRadio_BracketlessBlockParses()
    {
        var path = Path.Combine(WikiTabDir, "David Gray - Late Night Radio V2.Chords.txt");
        if (!File.Exists(path)) return; // not available in CI

        var content = File.ReadAllText(path);
        Assert.Equal([-1, 0, 2, 2, 1, 0], FretsOf(content, "Am"));
        Assert.Equal([-1, 0, 2, 0, 1, 0], FretsOf(content, "Am7"));
        Assert.Equal([-1, -1, 0, 2, 3, 1], FretsOf(content, "Dm"));
    }
}
