namespace JamSeshun.Services;

public static class EnumerableExtensions
{
    public static (int Index, T Min) ArgMin<T>(this IEnumerable<T> source) => source.Select((v, i) => (Index: i, Min: v)).MinBy(s => s.Min);

    public static (int Index, T Max) ArgMax<T>(this IEnumerable<T> source) => source.Select((v, i) => (Index: i, Max: v)).MaxBy(s => s.Max);
}
