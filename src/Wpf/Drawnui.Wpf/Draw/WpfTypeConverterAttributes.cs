using System.ComponentModel;
using DrawnUi.Wpf;

// Attaches the XAML type converters to DrawnUI's value types.
//
// System.Xaml resolves a converter by reflecting for a [TypeConverter] attribute on the type; it does
// NOT consult TypeDescriptor, so registering at runtime has no effect on compiled XAML — verified by
// the parser handing the raw string "#9BE15D" straight to SetValue.
//
// The attribute therefore has to be physically present, and it is added here rather than in SharedNet
// so that only this head carries it. Attributes on the parts of a partial type are merged, and the
// SharedNet sources are compiled into this same assembly, so these one-line parts are enough. The
// other heads compile the very same files and simply never see these attributes.

namespace DrawnUi
{
    [TypeConverter(typeof(DrawnColorConverter))]
    public partial class Color;
}

namespace DrawnUi.Views
{
    [TypeConverter(typeof(DrawnThicknessConverter))]
    public partial struct Thickness;
}

namespace DrawnUi.Draw
{
    [TypeConverter(typeof(DrawnCornerRadiusConverter))]
    public readonly partial struct CornerRadius;
}
