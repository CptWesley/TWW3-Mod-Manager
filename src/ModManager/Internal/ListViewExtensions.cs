namespace ModManager.Internal;

internal static class ListViewExtensions
{
    public static IEnumerable<ListViewItem> AsEnumerable(this ListView.ListViewItemCollection collection)
    {
        for (var i = 0; i < collection.Count; i++)
        {
            yield return collection[i];
        }
    }
}
