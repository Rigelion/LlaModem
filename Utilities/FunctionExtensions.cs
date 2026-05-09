namespace LlaModem.Utilities;

/// <summary>
/// Functional programming extensions for pipeline-style transformations.
/// Follows the principle "Prefer pipelines and transformations" from the project guidelines.
/// </summary>
public static class FunctionExtensions
{
    /// <summary>
    /// Pipes a value through a transformation function.
    /// </summary>
    public static TOut Pipe<TIn, TOut>(this TIn input, Func<TIn, TOut> transform) =>
        transform(input);

    /// <summary>
    /// Pipes a value through a transformation function, returning null if input is null.
    /// </summary>
    public static TOut? PipeOrDefault<TIn, TOut>(this TIn? input, Func<TIn, TOut> transform) where TIn : notnull =>
        input is null ? default : transform(input);

    /// <summary>
    /// Filters out null values from an enumerable.
    /// </summary>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : notnull =>
        source.Where(item => item is not null)!;

    /// <summary>
    /// Returns the last non-null element, or null if all are null.
    /// </summary>
    public static T? LastOrDefault<T>(this IEnumerable<T?> source) where T : notnull
    {
        foreach (var item in source)
        {
            if (item is not null)
                return item;
        }
        return default;
    }
}
