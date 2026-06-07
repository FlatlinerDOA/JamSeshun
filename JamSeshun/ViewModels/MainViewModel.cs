namespace JamSeshun.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        this.TunerVm = new TunerViewModel();
        this.TabListVm = new TabListViewModel();
        this.PerformanceVm = new PerformanceViewModel();
    }

    public MainViewModel(
        TunerViewModel tunerVm,
        TabListViewModel tabListVm,
        PerformanceViewModel performanceVm)
    {
        this.TunerVm = tunerVm;
        this.TabListVm = tabListVm;
        this.PerformanceVm = performanceVm;
    }

    public TunerViewModel TunerVm { get; }
    public TabListViewModel TabListVm { get; }
    public PerformanceViewModel PerformanceVm { get; }
}
