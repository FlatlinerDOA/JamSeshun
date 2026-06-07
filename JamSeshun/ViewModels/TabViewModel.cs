using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.Input;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.ViewModels;

public class TabViewModel : ViewModelBase, IOnShow
{
    private readonly TabLibraryService? library;
    private readonly INavigationService? navigation;

    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand TuneCommand { get; }

    public TabViewModel()
    {
        this.BackCommand = new AsyncRelayCommand(this.OnBack);
        this.EditCommand = new AsyncRelayCommand(this.OnEdit);
        this.TuneCommand = new AsyncRelayCommand(this.OnTune);
    }

    public TabViewModel(TabLibraryService library, INavigationService navigation) : this()
    {
        this.library = library;
        this.navigation = navigation;
    }

    public IDisposable OnShow()
    {
        this.ReloadTab(); // no-op on first show when Id not yet set; reloads on navigate-back
        if (this.library == null)
        {
            return Disposable.Empty;
        }
        return this.library.Changed
            .Where(id => this.Id.HasValue && id == this.Id.Value)
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(_ => this.ReloadTab());
    }

    public Guid? Id { get; set; }

    private SavedTab? tab;
    public SavedTab? Tab
    {
        get => this.tab;
        set
        {
            if (this.tab == value)
            {
                return;
            }
            this.tab = value;
            this.OnPropertyChanged(nameof(TabViewModel.Tab));
            this.OnPropertyChanged(nameof(TabViewModel.Artist));
            this.OnPropertyChanged(nameof(TabViewModel.Song));
            this.OnPropertyChanged(nameof(TabViewModel.TuningDisplay));
            this.OnPropertyChanged(nameof(TabViewModel.HasTuning));
            this.OnPropertyChanged(nameof(TabViewModel.HasParsableTuning));
            this.OnPropertyChanged(nameof(TabViewModel.HasTuningDisplayOnly));
            this.OnPropertyChanged(nameof(TabViewModel.Chords));
            this.OnPropertyChanged(nameof(TabViewModel.HasChords));
            this.OnPropertyChanged(nameof(TabViewModel.Lines));
        }
    }

    public string Artist => this.tab?.Artist ?? string.Empty;
    public string Song   => this.tab?.Song   ?? string.Empty;

    public string TuningDisplay
    {
        get
        {
            if (this.tab == null)
            {
                return string.Empty;
            }
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(this.tab.Tuning))
            {
                parts.Add(this.tab.Tuning);
            }
            if (this.tab.Capo > 0)
            {
                parts.Add($"Capo {this.tab.Capo}");
            }
            return string.Join("  ·  ", parts);
        }
    }

    public bool HasTuning => !string.IsNullOrEmpty(this.TuningDisplay);
    public bool HasParsableTuning => GuitarTuning.TryParse(this.tab?.Tuning) != null;
    public bool HasTuningDisplayOnly => this.HasTuning && !this.HasParsableTuning;

    public IReadOnlyList<Chord> Chords => WikiTabParser.ParseChords(this.tab?.Content);
    public bool HasChords => this.Chords.Count > 0;
    public IReadOnlyList<TabLine> Lines => WikiTabParser.ParseLines(this.tab?.Content);

    public void ReloadTab()
    {
        if (this.Id == null || this.library == null)
        {
            return;
        }
        var savedTab = this.library.Get(this.Id.Value);
        if (savedTab != null)
        {
            this.Tab = savedTab;
        }
    }

    private async Task OnBack()
    {
        if (this.navigation != null)
        {
            await this.navigation.PopAsync();
        }
    }

    private async Task OnEdit()
    {
        if (this.navigation == null || this.tab == null || this.Id == null)
        {
            return;
        }

        var id = this.Id.Value;
        var tab = this.tab;
        await this.navigation.PushAsync<TabEditorViewModel>(vm => vm.LoadForEdit(id, tab));
    }

    private async Task OnTune()
    {
        var tuning = GuitarTuning.TryParse(this.tab?.Tuning);
        if (tuning == null || this.navigation == null)
        {
            return;
        }

        await this.navigation.ActivateAsync<TunerViewModel>(vm => vm.TargetTuning = tuning);
    }
}
