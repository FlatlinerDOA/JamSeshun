using Avalonia.Platform.Storage;

namespace JamSeshun.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options);
}
