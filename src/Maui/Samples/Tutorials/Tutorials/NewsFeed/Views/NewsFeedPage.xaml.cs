using System.ComponentModel;
using DrawnUI.Tutorials.NewsFeed.ViewModels;
using DrawnUi.Views;

namespace DrawnUI.Tutorials.NewsFeed;

public partial class NewsFeedPage : DrawnUiBasePage
{
    private readonly NewsViewModel _vm;

    public NewsFeedPage()
    {
        try
        {
            InitializeComponent();

            _vm = new NewsViewModel();
            BindingContext = _vm;

            // LoadMore spinner: chat-style, driven directly (lottie start/stop + visibility)
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler == null && _vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NewsViewModel.IsLoadingMore) && sender is NewsViewModel vm)
        {
            var on = vm.IsLoadingMore;
            LoadMoreSpinner.IsVisible = on;
            if (on)
                LoadMoreLoader.Start();
            else
                LoadMoreLoader.Stop();
        }
    }

}

public class AppScroll : SkiaScroll
{
    public AppScroll()
    {
        AutoCache = true;
        ScrollBar = new SkiaScrollBar();
    }
}
