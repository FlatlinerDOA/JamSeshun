using JamSeshun.Services;

namespace JamSeshun.ViewModels;

public class TabViewModel : ViewModelBase
{
    public TabViewModel() { }

    public TabViewModel(Tab tab)
    {
        Tab = tab;
    }

    public Tab? Tab { get; }
    public string Artist => Tab?.Name.Artist ?? string.Empty;
    public string Song => Tab?.Name.Song ?? string.Empty;
    public string? WikiTab => Tab?.WikiTab;
    public GuitarTuning? Tuning => Tab?.Tuning;
    public string TuningDisplay => Tuning != null ? $"{Tuning.Name}  Capo: {(Tuning.Capo == 0 ? "None" : Tuning.Capo.ToString())}" : string.Empty;
}
