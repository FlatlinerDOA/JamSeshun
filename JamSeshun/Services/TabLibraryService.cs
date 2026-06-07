using LiteDB;
using STJ = System.Text.Json;

namespace JamSeshun.Services;

using System.Reactive.Subjects;

public sealed class TabLibraryService : IDisposable
{
    private readonly LiteDatabase db;
    private readonly ILiteCollection<StoreEntry> entries;

    public Subject<Guid> Changed { get; } = new();

    public TabLibraryService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JamSeshun")) { }

    public TabLibraryService(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        this.db = new LiteDatabase(Path.Combine(dataDir, "tabs.db"));
        this.entries = this.db.GetCollection<StoreEntry>("tabs");
        this.entries.EnsureIndex(x => x.Name);
    }

    public void Save(Guid id, string name, SavedTab tab)
    {
        this.entries.Upsert(new StoreEntry
        {
            Id = id,
            Name = name,
            Json = STJ.JsonSerializer.Serialize(tab)
        });
        this.Changed.OnNext(id);
    }

    public SavedTab? Get(Guid id)
    {
        var entry = this.entries.FindById(new BsonValue(id));
        return entry is null ? null : STJ.JsonSerializer.Deserialize<SavedTab>(entry.Json);
    }

    public IEnumerable<(Guid Id, string Name)> GetAll() => this.entries.FindAll().Select(e => (e.Id, e.Name));

    public IEnumerable<(Guid Id, string Name)> Search(string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return this.entries.FindAll()
                       .Where(e => terms.All(t => e.Name.Contains(t, StringComparison.OrdinalIgnoreCase)))
                       .Select(e => (e.Id, e.Name));
    }

    public bool NameExists(string name) => this.entries.Exists(Query.EQ("Name", name));

    public void Delete(Guid id)
    {
        this.entries.Delete(new BsonValue(id));
        this.Changed.OnNext(id);
    }

    public void Dispose() => this.db.Dispose();
}
