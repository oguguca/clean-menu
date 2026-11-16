using System.Collections.ObjectModel;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Makes an ObservableCollection match a target order with minimal moves, inserts and removals,
/// so list items keep their visual state (scroll, focus) while values refresh.
/// </summary>
public static class CollectionSync
{
    public static void Apply<T>(ObservableCollection<T> collection, IReadOnlyList<T> target)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(target);

        var wanted = new HashSet<T>(target, ReferenceEqualityComparer.Instance);
        for (var i = collection.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(collection[i]))
            {
                collection.RemoveAt(i);
            }
        }

        for (var i = 0; i < target.Count; i++)
        {
            var item = target[i];
            if (i < collection.Count && ReferenceEquals(collection[i], item))
            {
                continue;
            }

            var current = IndexOf(collection, item, i);
            if (current >= 0)
            {
                collection.Move(current, i);
            }
            else
            {
                collection.Insert(i, item);
            }
        }
    }

    private static int IndexOf<T>(ObservableCollection<T> collection, T item, int start)
        where T : class
    {
        for (var i = start; i < collection.Count; i++)
        {
            if (ReferenceEquals(collection[i], item))
            {
                return i;
            }
        }

        return -1;
    }
}
