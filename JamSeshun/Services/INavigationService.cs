using JamSeshun.ViewModels;

namespace JamSeshun.Services;

public interface INavigationService
{
    Task PushAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase;
    Task PopAsync();
    Task PopToRootAsync();
    Task ActivateAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase;
}
