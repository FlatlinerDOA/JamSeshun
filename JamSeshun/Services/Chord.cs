namespace JamSeshun.Services;

public record Chord(string Name, string Id, string Type, int[] Frets, int[] Fingers)
{
    public override string ToString() => (this.Name + ":").PadRight(6) + $"[{this.Id}]";
}
