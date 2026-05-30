namespace JamSeshun.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        TunerVM = new TunerViewModel();
        TabListVM = new TabListViewModel();
        PerformanceVM = new PerformanceViewModel();
        TabEditorVM = new TabEditorViewModel();
    }

    public MainViewModel(
        TunerViewModel tunerVm,
        TabListViewModel tabListVm,
        PerformanceViewModel performanceVm,
        TabEditorViewModel tabEditorVm)
    {
        TunerVM = tunerVm;
        TabListVM = tabListVm;
        PerformanceVM = performanceVm;
        TabEditorVM = tabEditorVm;
    }

    public TunerViewModel TunerVM { get; }
    public TabListViewModel TabListVM { get; }
    public PerformanceViewModel PerformanceVM { get; }
    public TabEditorViewModel TabEditorVM { get; }
}
