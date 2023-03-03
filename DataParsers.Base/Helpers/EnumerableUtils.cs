using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DataParsers.Base.Helpers;

public static class EnumerableUtils
{
    public static IEnumerable<T> SelectMany<T>(this IEnumerable<IEnumerable<T>> seq)
    {
        return seq.SelectMany(suseq => suseq);
    }

    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach(var item in enumerable)
            action.Invoke(item);
    }

    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<int, T> action)
    {
        var count = 0;
        foreach(var item in enumerable)
            action.Invoke(count++, item);
    }

    public static void ForEach<T>(this IEnumerable<T> enumerable, Func<bool, int, T, bool> action)
    {
        using(var enumerator = enumerable.GetEnumerator())
        {
            var isLast = !enumerator.MoveNext();
            var count = 0;
            while(!isLast)
            {
                var current = enumerator.Current;
                isLast = !enumerator.MoveNext();
                var @continue = action.Invoke(isLast, count++, current);
                if(!@continue)
                    break;
            }
        }
    }

    public static Dictionary<TKey, TValue> ToDictSafeFirst<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> enumerable, IEqualityComparer<TKey> comparer = null)
    {
        return ToDictSafeFirst(enumerable, pair => pair.Key, pair => pair.Value, comparer);
    }

    public static Dictionary<TKey, TValue> ToDictSafe<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> enumerable, IEqualityComparer<TKey> comparer = null)
    {
        return ToDictSafe(enumerable, pair => pair.Key, pair => pair.Value, comparer);
    }

    public static Dictionary<TKey, TValue> ToDictSafeFirst<TKey, TValue>(this IEnumerable<TValue> enumerable, Func<TValue, TKey> getKey, IEqualityComparer<TKey> comparer = null)
    {
        return ToDictSafeFirst(enumerable, getKey, item => item, comparer);
    }

    public static Dictionary<TKey, TValue> ToDictSafe<TKey, TValue>(this IEnumerable<TValue> enumerable, Func<TValue, TKey> getKey, IEqualityComparer<TKey> comparer = null)
    {
        return ToDictSafe(enumerable, getKey, item => item, comparer);
    }

    public static Dictionary<TKey, TValue> ToDictSafeFirst<TKey, TValue, TSource>(this IEnumerable<TSource> enumerable, Func<TSource, TKey> getKey, Func<TSource, TValue> getValue, IEqualityComparer<TKey> comparer = null)
    {
        var dict = enumerable is ICollection collection ? new Dictionary<TKey, TValue>(collection.Count, comparer) : new Dictionary<TKey, TValue>(comparer);
        if(enumerable == null)
            return dict;
        foreach(var item in enumerable)
        {
            var key = getKey(item);
            if(!dict.ContainsKey(key))
                dict[key] = getValue(item);
        }

        return dict;
    }

    public static Dictionary<TKey, TValue> ToDictSafe<TKey, TValue, TSource>(this IEnumerable<TSource> enumerable, Func<TSource, TKey> getKey, Func<TSource, TValue> getValue, IEqualityComparer<TKey> comparer = null)
    {
        var dict = enumerable is ICollection collection ? new Dictionary<TKey, TValue>(collection.Count, comparer) : new Dictionary<TKey, TValue>(comparer);
        if(enumerable == null)
            return dict;
        foreach(var item in enumerable)
            dict[getKey(item)] = getValue(item);
        return dict;
    }

    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> enumerable)
    {
        return enumerable ?? GetEmpty<T>();
    }

    public static IEnumerable<T> GetEmpty<T>()
    {
        return Enumerable.Empty<T>();
    }

    public static T[] EmptyIfNull<T>(this T[] enumerable)
    {
        return enumerable ?? EmptyCollectionsProvider<T>.Array;
    }

    public static bool IsSignificant<T>(this List<T> list)
    {
        return list != null && list.Count != 0;
    }

    public static bool IsSignificant(this IEnumerable<string> enumerable, Func<string, bool> action = null)
    {
        return enumerable.EmptyIfNull().Any(action ?? (str => !string.IsNullOrEmpty(str)));
    }

    public static bool IsSignificant<T>(this IEnumerable<T> enumerable, Func<T, bool> action = null)
    {
        return enumerable.EmptyIfNull().Any(action ?? (t => t != null && !t.Equals(default(T))));
    }

    public static IEnumerable<T> WhereNotNull<T, TV>(this IEnumerable<T> enumerable, Func<T, TV> select) where TV : class
    {
        return enumerable.Where(item => item != null && select(item) != null);
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T> enumerable)
    {
        return enumerable.Where(item => item != null);
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable) where T : struct
    {
        return enumerable.Where(item => item != null).Select(item => item.Value);
    }

    public static IEnumerable<T> GetLast<T>(this IEnumerable<T> enumerable, int count)
    {
        return enumerable.Reverse().Take(count).Reverse();
    }

    public static IEnumerable<T> WhereNotDefault<T>(this IEnumerable<T> enumerable)
    {
        return enumerable.Where(item => !EqualityComparer<T>.Default.Equals(item, default));
    }

    public static bool IsDefault<T>(this T value, T def = default)
    {
        return EqualityComparer<T>.Default.Equals(value, def);
    }

    public static IEnumerable<T> WhereNotDefault<T>(this IEnumerable<T> enumerable, T def = default)
    {
        return enumerable.Where(item => !item.IsDefault(def));
    }

    private static class EmptyCollectionsProvider<T>
    {
        public static readonly T[] Array = new T[0];
    }
}
