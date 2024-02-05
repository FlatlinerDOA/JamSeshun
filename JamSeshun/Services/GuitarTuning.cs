namespace JamSeshun.Services;

public record GuitarTuning(string Name, string Notes, int Capo)
{
    private string CapoSummary => this.Capo == 0 ? "(None)" : this.Capo.ToString();

    public override string ToString() => $"{this.Name} Tuning: {this.Notes}\nCapo: {this.CapoSummary}";
}
