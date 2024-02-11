using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;
namespace JamSeshun;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; internal set; } 

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        if (BindingPlugins.DataValidators.Count != 0)
        {
            BindingPlugins.DataValidators.RemoveAt(0);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
