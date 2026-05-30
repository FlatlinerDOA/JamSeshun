namespace JamSeshun.Services;

public record SavedTab(
    string Artist,
    string Song,
    string Content,
    string Tuning = "",
    int Capo = 0,
    DateTimeOffset DateSaved = default);
