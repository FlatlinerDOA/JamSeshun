using System.Text;

namespace JamSeshun.Services;

public record TabReference(string Artist, string Song, int Version, string Type, int Votes, decimal Rating, string Url)
{
    public decimal Score => (decimal)(Math.Sqrt((double)this.Votes) * (double)this.Rating);
    public string FileName => SafeFileName($"{this.Artist} - {this.Song} V{this.Version}.{this.Type}.txt");

    public bool Exists(string targetFolder) => File.Exists(Path.Combine(targetFolder, this.FileName));

    private string SafeFileName(string fileName)
    {
        var s = new StringBuilder(fileName);
        foreach (var c in Path.GetInvalidPathChars())
        {
            s.Replace(c, '_');
        }

        return s.ToString();
    }

    public override string ToString() => $"{this.Artist} - {this.Song} V{this.Version}";
}
