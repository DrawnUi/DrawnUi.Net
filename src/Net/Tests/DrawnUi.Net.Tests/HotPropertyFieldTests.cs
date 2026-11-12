using DrawnUi.Draw;
using Xunit;

namespace DrawnUi.Net.Tests;

/// <summary>
/// The properties read on every draw keep their value in a field next to the bindable one. Whatever way the value
/// changes (the property, SetValue, ClearValue), the property returns what the bindable store holds, also from inside
/// the change notification.
/// </summary>
public class HotPropertyFieldTests
{
    public static TheoryData<string, object> Cases => new()
    {
        { nameof(SkiaControl.UseCache), SkiaCacheType.Image },
        { nameof(SkiaControl.AllowCaching), false },
        { nameof(SkiaControl.RenderingScale), 2.5f },
        { nameof(SkiaControl.SkewX), 7f },
        { nameof(SkiaControl.SkewY), -3f },
        { nameof(SkiaControl.Perspective1), 0.001f },
        { nameof(SkiaControl.Perspective2), 0.002f },
        { nameof(SkiaControl.RotationZ), 15.0 },
        { nameof(SkiaControl.TranslationZ), 20.0 },
        { nameof(SkiaControl.AddTranslationX), 4.0 },
        { nameof(SkiaControl.AddTranslationY), -4.0 },
        { nameof(SkiaControl.TranslationX), 11.0 },
        { nameof(SkiaControl.TranslationY), -11.0 },
        { nameof(SkiaControl.ScaleX), 1.5 },
        { nameof(SkiaControl.ScaleY), 0.5 },
        { nameof(SkiaControl.Rotation), 30.0 },
        { nameof(SkiaControl.RotationX), 10.0 },
        { nameof(SkiaControl.RotationY), -10.0 },
        { nameof(SkiaControl.Opacity), 0.25 },
    };

    static BindableProperty Property(string name) =>
        (BindableProperty)typeof(SkiaControl).GetField(name + "Property",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)!
            .GetValue(null)!;

    static object Read(SkiaControl control, string name) => typeof(SkiaControl).GetProperty(name)!.GetValue(control);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Property_ReturnsTheStoredValue_AfterEveryKindOfChange(string name, object value)
    {
        var property = Property(name);
        var control = new SkiaLayout();
        var initial = Read(control, name);

        object seenInNotification = null;
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == name)
                seenInNotification = Read(control, name);
        };

        typeof(SkiaControl).GetProperty(name)!.SetValue(control, value);
        Assert.Equal(value, Read(control, name));
        Assert.Equal(value, seenInNotification);

        control.ClearValue(property);
        Assert.Equal(initial, Read(control, name));

        control.SetValue(property, value);
        Assert.Equal(value, Read(control, name));
    }

    [Fact]
    public void Parent_FollowsAddAndRemove()
    {
        var parent = new SkiaLayout();
        var child = new SkiaLayout();
        parent.AddSubView(child);
        Assert.Same(parent, child.Parent);
        parent.RemoveSubView(child);
        Assert.Null(child.Parent);
        Assert.Null(child.GetValue(SkiaControl.ParentProperty));
    }

    [Fact]
    public void ItemTemplate_FollowsTheStore()
    {
        var layout = new SkiaLayout();
        var template = new DataTemplate(() => new SkiaLayout());
        layout.ItemTemplate = template;
        Assert.Same(template, layout.ItemTemplate);
        layout.ClearValue(SkiaControl.ItemTemplateProperty);
        Assert.Null(layout.ItemTemplate);
    }
}
