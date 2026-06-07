using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace JamSeshun.Services;

public class FilePickerService : IFilePickerService
{
    private TopLevel? topLevel;

    public void Register(TopLevel topLevel)
    {
        this.topLevel = topLevel;
    }

    public async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        if (this.topLevel == null)
        {
            return [];
        }

        return await this.topLevel.StorageProvider.OpenFilePickerAsync(options);
    }
}
