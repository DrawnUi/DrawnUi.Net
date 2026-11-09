#nullable disable
namespace DrawnUi.Add3d;

/// <summary>Fluent wiring of the 3D controls' events, in the style of DrawnUI's .OnTapped(...).</summary>
public static class Add3dFluent
{
    /// <summary>Runs when an animation played with PlayOnce or Hold ended, with its name.</summary>
    public static T OnAnimationEnded<T>(this T model, Action<T, string> action) where T : SkiaModel3D
    {
        model.AnimationEnded += (_, name) => action(model, name);
        return model;
    }

    /// <summary>Runs when a tap lands on the scene's surface, with what it hit: the node, the material and the point.</summary>
    public static T OnTapped3D<T>(this T model, Action<T, ModelHit> action) where T : SkiaModel3D
    {
        model.Tapped3D += (_, hit) => action(model, hit);
        return model;
    }

    /// <summary>Starts with an image on a material (see <see cref="SkiaModel3D.SetMaterialImage"/>).</summary>
    public static T MaterialImage<T>(this T model, string name, Texture3D image) where T : SkiaModel3D
    {
        model.SetMaterialImage(name, image);
        return model;
    }

    /// <summary>Runs when an object's animation played with PlayOnce or Hold ended: the object's name and the animation's.</summary>
    public static T OnAnimationEnded<T>(this T layer, Action<T, SceneAnimationEnded> action) where T : SkiaScene3D
    {
        layer.AnimationEnded += (_, ended) => action(layer, ended);
        return layer;
    }

    /// <summary>Runs when a tap lands on an object, with what it hit: the object, its node and its material.</summary>
    public static T OnTapped3D<T>(this T layer, Action<T, SceneHit> action) where T : SkiaScene3D
    {
        layer.Tapped3D += (_, hit) => action(layer, hit);
        return layer;
    }
}
