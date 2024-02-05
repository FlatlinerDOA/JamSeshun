namespace JamSeshun.Services;

public record Tab(TabReference Name, GuitarTuning Tuning, string WikiTab, IReadOnlyList<Chord> Chords)
{
    public string ChordSummary => this.Chords.Any() ? $"Chords:\n{string.Join('\n', this.Chords)}" : string.Empty;

    public override string ToString() => $"{this.Tuning}\n{this.ChordSummary}\n\n{this.WikiTab}";

    public async Task SaveAsync(string targetFolder)
    {
        Directory.CreateDirectory(targetFolder);
        var fileName = Path.Combine(targetFolder, this.Name.FileName);
        await File.WriteAllTextAsync(fileName, this.ToString());
    }
}