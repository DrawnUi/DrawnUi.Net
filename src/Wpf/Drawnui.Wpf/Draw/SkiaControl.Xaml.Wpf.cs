namespace DrawnUi.Draw;

/// <summary>
/// Makes drawn controls read in WPF XAML the way they do in MAUI XAML — children written directly
/// inside the element, with no property-element wrapper:
/// <code>
/// &lt;draw:SkiaStack&gt;
///     &lt;draw:SkiaLabel Text="hello" /&gt;
/// &lt;/draw:SkiaStack&gt;
/// </code>
/// <para>
/// Two things are needed for that. The XAML markup compiler does not treat the declared type of
/// <see cref="SkiaControl.Children"/>, <c>IList&lt;SkiaControl&gt;</c>, as a collection — it only
/// recognises concrete collection types — so <see cref="ChildrenXaml"/> exposes the very same list
/// under its real type. And the content property is declared here rather than in shared code because
/// attributes on the parts of a partial class are merged into the one type.
/// </para>
/// </summary>
[System.Windows.Markup.ContentProperty(nameof(ChildrenXaml))]
public partial class SkiaControl
{
    /// <summary>
    /// The control's <see cref="Children"/> under a concrete collection type, for the XAML parser.
    /// Not a copy — adding here adds to the control.
    /// </summary>
    public ObservableAttachedItemsCollection<SkiaControl> ChildrenXaml
    {
        get
        {
            if (Children is ObservableAttachedItemsCollection<SkiaControl> typed)
                return typed;

            // Children was replaced with some other IList implementation. Move its content into the
            // type the rest of the framework expects rather than throwing at parse time.
            var created = new ObservableAttachedItemsCollection<SkiaControl>();
            if (Children != null)
            {
                foreach (var child in Children)
                    created.Add(child);
            }

            Children = created;
            return created;
        }
    }
}
