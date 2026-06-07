using Avalonia.Controls;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider serviceProvider;
    private NavigationPage? navigationPage;
    private TabbedPage? tabbedPage;

    public NavigationService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public void Register(NavigationPage navigationPage, TabbedPage tabbedPage)
    {
        this.navigationPage = navigationPage;
        this.tabbedPage = tabbedPage;
    }

    public async Task PushAsync<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : ViewModelBase
    {
        if (this.navigationPage == null)
        {
            return;
        }

        var vm = this.serviceProvider.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        var view = new ViewLocator().Build(vm);
        view.DataContext = vm;

        var page = new ContentPage { Content = view };
        NavigationPage.SetHasNavigationBar(page, false);

        await this.navigationPage.PushAsync(page);
    }

    public async Task PopAsync()
    {
        if (this.navigationPage != null)
        {
            await this.navigationPage.PopAsync();
        }
    }

    public async Task PopToRootAsync()
    {
        if (this.navigationPage != null)
        {
            await this.navigationPage.PopToRootAsync();
        }
    }

    public async Task ActivateAsync<TViewModel>(Action<TViewModel>? configure = null)
        where TViewModel : ViewModelBase
    {
        if (this.tabbedPage?.Pages == null)
        {
            return;
        }

        var pages = this.tabbedPage.Pages.ToList();
        for (int i = 0; i < pages.Count; i++)
        {
            var rootPage = pages[i] is NavigationPage nav
                ? (object?)nav.NavigationStack.FirstOrDefault()
                : pages[i];

            if (GetContent(rootPage)?.DataContext is TViewModel vm)
            {
                configure?.Invoke(vm);
                this.tabbedPage.SelectedIndex = i;
                return;
            }
        }
    }

    private static Control? GetContent(object? page) =>
        page?.GetType().GetProperty("Content")?.GetValue(page) as Control;
}
