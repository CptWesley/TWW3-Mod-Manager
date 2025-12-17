using System.Collections;

namespace ModManager.Internal;

internal sealed class AnnotatedListView<T> : ListView
{
    private readonly AnnotatedListViewItemCollection<T> items;

    public AnnotatedListView() : base()
    {
        items = new(base.Items);
    }

    public new AnnotatedListViewItemCollection<T> Items => items;

    public new AnnotatedListViewItem<T> TopItem
    {
        get => (AnnotatedListViewItem<T>)base.TopItem;
        set => base.TopItem = value;
    }

    public new AnnotatedListViewItem<T> GetItemAt(int x, int y)
        => (AnnotatedListViewItem<T>)base.GetItemAt(x, y);
}

internal sealed class AnnotatedListViewItemCollection<T>(ListView.ListViewItemCollection wrapped)
    : IList<AnnotatedListViewItem<T>>, IReadOnlyList<AnnotatedListViewItem<T>>
{
    public AnnotatedListViewItem<T> this[int index]
    {
        get => (AnnotatedListViewItem<T>)wrapped[index];
        set => wrapped[index] = value;
    }

    public int Count => wrapped.Count;

    public bool IsReadOnly => wrapped.IsReadOnly;

    public void Add(AnnotatedListViewItem<T> item)
        => wrapped.Add(item);

    public void Clear()
        => wrapped.Clear();

    public bool Contains(AnnotatedListViewItem<T> item)
        => wrapped.Contains(item);

    public void CopyTo(AnnotatedListViewItem<T>[] array, int arrayIndex)
        => wrapped.CopyTo(array, arrayIndex);

    public IEnumerator<AnnotatedListViewItem<T>> GetEnumerator()
    {
        for (var i = 0; i < wrapped.Count; i++)
        {
            yield return (AnnotatedListViewItem<T>)wrapped[i];
        }
    }

    public int IndexOf(AnnotatedListViewItem<T> item)
        => wrapped.IndexOf(item);

    public void Insert(int index, AnnotatedListViewItem<T> item)
        => wrapped.Insert(index, item);

    public bool Remove(AnnotatedListViewItem<T> item)
    {
        var countBefore = wrapped.Count;
        wrapped.Remove(item);
        var countAfter = wrapped.Count;
        return countBefore != countAfter;
    }

    public void RemoveAt(int index)
        => wrapped.RemoveAt(index);

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

internal sealed class AnnotatedListViewItem<T> : ListViewItem
{
    public AnnotatedListViewItem(T annotation, params string[] items)
        : base(items)
    {
        Annotation = annotation;
    }

    public T Annotation { get; set; }
}
