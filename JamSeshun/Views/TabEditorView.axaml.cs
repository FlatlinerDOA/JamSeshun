using Avalonia.Controls;
using Avalonia.Platform.Storage;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class TabEditorView : UserControl
{
    public TabEditorView()
    {
        InitializeComponent();
        ImportButton.Click += OnImportClicked;
    }

    private static readonly FilePickerFileType TxtFiles = new("Tab files")
    {
        Patterns = ["*.txt"],
        MimeTypes = ["text/plain"]
    };

    private async void OnImportClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var vm = DataContext as TabEditorViewModel;
        if (vm == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Tabs",
            AllowMultiple = true,
            FileTypeFilter = [TxtFiles]
        });

        if (files.Count == 0) return;

        var parsed = new List<(string FileName, string Content)>(files.Count);
        foreach (var file in files)
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            parsed.Add((file.Name, await reader.ReadToEndAsync()));
        }

        vm.ImportFiles(parsed);
    }
}
