using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DrawnUi.Draw
{
    /// <summary>
    /// We have to subclass ObservableCollection to avoid it sending empty oldItems upon Reset.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObservableAttachedItemsCollection<T> : ObservableCollection<T>
    {
        internal static class EventArgsCache
        {
            internal static readonly PropertyChangedEventArgs CountPropertyChanged = new PropertyChangedEventArgs("Count");
            internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new PropertyChangedEventArgs("Item[]");
            internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        }

        protected override void ClearItems()
        {
            CheckReentrancy();
            var oldItems = new List<T>(this); // Capture all items
            if (oldItems.Count > 0)
            {
                Items.Clear(); // Clear the underlying list directly
                OnPropertyChanged(EventArgsCache.CountPropertyChanged);
                OnPropertyChanged(EventArgsCache.IndexerPropertyChanged); OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove, oldItems, 0));
            }
        }
    }
}
