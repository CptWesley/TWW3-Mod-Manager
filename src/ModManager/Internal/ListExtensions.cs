namespace ModManager.Internal;

internal static class ListExtensions
{
    public static void Sort<T>(this IList<T> list, IComparer<T> comparer)
    {
        var n = list.Count;
        var swapped = false;

        for (var i = 0; i < n - 1; i++)
        {
            swapped = false;
            for (var j = 0; j < n - i - 1; j++)
            {
                var v1 = list[j];
                var v2 = list[j + 1];

                if (comparer.Compare(v1, v2) > 0)
                {
                    var temp = list[j];

                    list.RemoveAt(j); // list[j] = list[j + 1];
                    list.Insert(j + 1, temp); // list[j + 1] = temp;

                    swapped = true;
                }
            }

            if (swapped == false)
            {
                break;
            }
        }
    }

    public static void Sort<T>(this IList<T> list)
        where T : IComparable<T>
        => list.Sort(Comparer<T>.Default);

    public static void SortBy<T, TComparable>(this IList<T> list, Func<T, TComparable> selector)
        where TComparable : IComparable<TComparable>
        => list.Sort(new SelectorComparer<T, TComparable>(selector));

    private sealed class SelectorComparer<T, TComparable>(Func<T, TComparable> selector) : IComparer<T>
        where TComparable : IComparable<TComparable>
    {
        public int Compare(T x, T y)
        {
            var v1 = selector(x);
            var v2 = selector(y);
            return Comparer<TComparable>.Default.Compare(v1, v2);
        }
    }
}
