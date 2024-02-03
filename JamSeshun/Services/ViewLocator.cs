using Avalonia.Controls.Templates;
using Avalonia.Controls;
using JamSeshun.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace JamSeshun.Services;

public class ViewLocator : IDataTemplate
{
    public bool SupportsRecycling => false;

    public Control Build(object? param)
    {
        var name = param?.GetType().FullName?.Replace("ViewModel", "View");
        if (name != null)
        {
            if (App.ServiceProvider.GetKeyedService<Control>(name) is Control registeredControl)
            {
                return registeredControl;
            }
            else
            {
                // This code is called at Design time or when a service isn't registered.
                var type = Type.GetType(name);
                if (type != null && Activator.CreateInstance(type) is Control unregisteredControl)
                {
                    return unregisteredControl;
                }
            }
        }

        return new TextBlock { Text = $"{name} not found or is not a Control." };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
