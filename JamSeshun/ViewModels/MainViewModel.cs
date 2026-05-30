namespace JamSeshun.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        TunerVM = new TunerViewModel();
        TabListVM = new TabListViewModel();
        PerformanceVM = new PerformanceViewModel();
    }

    public MainViewModel(
        TunerViewModel tunerVm,
        TabListViewModel tabListVm,
        PerformanceViewModel performanceVm)
    {
        TunerVM = tunerVm;
        TabListVM = tabListVm;
        PerformanceVM = performanceVm;
    }

    public TunerViewModel TunerVM { get; }
    public TabListViewModel TabListVM { get; }
    public PerformanceViewModel PerformanceVM { get; }
}
