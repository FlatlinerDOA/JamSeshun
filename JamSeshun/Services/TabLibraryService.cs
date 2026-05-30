using LiteDB;
using STJ = System.Text.Json;

namespace JamSeshun.Services;

public sealed class TabLibraryService : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<StoreEntry> _entries;

    public event Action? Changed;

    public TabLibraryService()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JamSeshun");
        Directory.CreateDirectory(dataDir);
        _db = new LiteDatabase(Path.Combine(dataDir, "tabs.db"));
        _entries = _db.GetCollection<StoreEntry>("tabs");
        _entries.EnsureIndex(x => x.Name);
    }

    public void Save(Guid id, string name, SavedTab tab)
    {
        _entries.Upsert(new StoreEntry
        {
            Id = id,
            Name = name,
            Json = STJ.JsonSerializer.Serialize(tab)
        });
        Changed?.Invoke();
    }

    public SavedTab? Get(Guid id)
    {
        var entry = _entries.FindById(new BsonValue(id));
        return entry is null ? null : STJ.JsonSerializer.Deserialize<SavedTab>(entry.Json);
    }

    public IEnumerable<(Guid Id, string Name)> GetAll() =>
        _entries.FindAll().Select(e => (e.Id, e.Name));

    public IEnumerable<(Guid Id, string Name)> Search(string query) =>
        _entries.FindAll()
                .Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(e => (e.Id, e.Name));

    public void Delete(Guid id)
    {
        _entries.Delete(new BsonValue(id));
        Changed?.Invoke();
    }

    public void Dispose() => _db.Dispose();
}
