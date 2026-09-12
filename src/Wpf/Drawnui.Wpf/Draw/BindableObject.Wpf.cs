using System.ComponentModel;
using System.Windows;

namespace DrawnUi.Draw
{
    /// <summary>
    /// WPF adds <see cref="FrameworkContentElement"/> as base via partial declaration, mirroring what
    /// the Blazor head does with LayoutComponentBase. Plain INotifyPropertyChanged + GetValue/SetValue
    /// logic stays in SharedNet/Draw/BindableObject.Plain.cs.
    /// <para>
    /// Being a DependencyObject is what lets a drawn control be the target of <c>{Binding}</c>, a
    /// Style setter or a trigger. It does NOT store anything: values live in the lock-guarded
    /// dictionary in the shared file, which is thread-agnostic, so DrawnUI's off-thread work keeps
    /// working. <see cref="WpfPropertyMirror"/> is what connects the two.
    /// </para>
    /// </summary>
    public partial class BindableObject : FrameworkContentElement
    {
        /// <summary>
        /// Guards the mirror against feeding itself: a value arriving from WPF is written to the
        /// dictionary, which raises PropertyChanged, which would otherwise write back to WPF.
        /// </summary>
        internal bool SyncingFromMirror;

        /// <summary>
        /// Names of the properties WPF has written through the mirror. Only these are pushed back on
        /// engine writes; every other engine write (animators, layout) skips the property system.
        /// </summary>
        internal HashSet<string> WpfTouched;

        /// <summary>
        /// Guards only the DataContext ⇄ BindingContext hop. Deliberately not <see cref="SyncingFromMirror"/>:
        /// WPF re-evaluates every binding synchronously inside a DataContext change, and those
        /// transfers must reach the engine.
        /// </summary>
        private bool _syncingContext;

        /// <summary>Creates the object and connects it to the WPF property system.</summary>
        public BindableObject()
        {
            WpfPropertyMirror.EnsureRegistered(GetType());

            DataContextChanged += (_, e) =>
            {
                if (_syncingContext)
                    return;

                _syncingContext = true;
                try
                {
                    // DrawnUI propagates BindingContext down its own tree; WPF bindings resolve against
                    // DataContext. Keeping them equal is what makes {Binding Something} inside a drawn
                    // subtree see the host's DataContext.
                    BindingContext = e.NewValue;
                }
                finally
                {
                    _syncingContext = false;
                }
            };

            PropertyChanged += OnBindablePropertyChangedForMirror;
        }

        /// <summary>
        /// FrameworkContentElement owns IsEnabled and Tag, so the mirror cannot give them a
        /// DependencyProperty of its own; XAML and bindings land on WPF's. Forward those into the
        /// engine so <c>IsEnabled="{Binding CanSave}"</c> really disables the drawn control.
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (SyncingFromMirror)
                return;

            if (e.Property == IsEnabledProperty && this is View view)
            {
                (WpfTouched ??= new HashSet<string>(StringComparer.Ordinal)).Add(nameof(View.IsEnabled));
                SyncingFromMirror = true;
                try
                {
                    view.IsEnabled = (bool)e.NewValue;
                }
                finally
                {
                    SyncingFromMirror = false;
                }
            }
            else if (e.Property == TagProperty && this is SkiaControl control)
            {
                SyncingFromMirror = true;
                try
                {
                    control.Tag = e.NewValue as string ?? e.NewValue?.ToString();
                }
                finally
                {
                    SyncingFromMirror = false;
                }
            }
        }

        private void OnBindablePropertyChangedForMirror(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null)
                return;

            var isContext = e.PropertyName == nameof(BindingContext);
            if (isContext ? _syncingContext : SyncingFromMirror)
                return;

            if (!isContext && (WpfTouched == null || !WpfTouched.Contains(e.PropertyName)))
                return;

            // Only mirror back when the write already happened on the UI thread. An off-thread engine
            // write must never touch a DependencyObject, and that restriction is the whole reason the
            // dictionary stays the source of truth.
            if (!CheckAccess())
                return;

            if (isContext)
            {
                _syncingContext = true;
                try
                {
                    DataContext = BindingContext;
                }
                finally
                {
                    _syncingContext = false;
                }

                return;
            }

            if (e.PropertyName == nameof(View.IsEnabled) && this is View view)
            {
                SyncingFromMirror = true;
                try
                {
                    SetCurrentValue(IsEnabledProperty, view.IsEnabled);
                }
                finally
                {
                    SyncingFromMirror = false;
                }

                return;
            }

            WpfPropertyMirror.PushToWpf(this, e.PropertyName);
        }
    }
}
