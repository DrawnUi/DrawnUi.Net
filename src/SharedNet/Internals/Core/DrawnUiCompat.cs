using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DrawnUi.Draw
{
    public readonly struct Font
    {
    }

    public interface IText
    {
    }

    public interface IFontRegistrar
    {
    }

    public static class ServiceProviderServiceExtensions
    {
        public static T? GetService<T>(this IServiceProvider services)
        {
            return (T?)services?.GetService(typeof(T));
        }
    }
}

namespace DrawnUi.Draw
{
    [Flags]
    public enum FontAttributes
    {
        None = 0,
        Bold = 1,
        Italic = 2
    }

    public enum TextAlignment
    {
        Start,
        Center,
        End
    }

    public enum LineBreakMode
    {
        NoWrap,
        WordWrap,
        CharacterWrap,
        HeadTruncation,
        MiddleTruncation,
        TailTruncation
    }

    public class FormattedString
    {
    }

    public class PropertyChangingEventArgs : global::System.ComponentModel.PropertyChangingEventArgs
    {
        public PropertyChangingEventArgs(string? propertyName) : base(propertyName)
        {
        }
    }
}

namespace DrawnUi.Draw
{
    public class DrawnFontAttributesConverter : TypeConverter
    {
    }

    public class FontSizeConverter : TypeConverter
    {
    }

    public enum HardwareAccelerationMode
    {
        Disabled,
        Prerender,
        Enabled
    }


    public static class AddGestures
    {
        public static IDictionary<SkiaControl, GestureListener> AttachedListeners { get; } = new Dictionary<SkiaControl, GestureListener>();

        public sealed class GestureListener : ISkiaGestureListener
        {
            public bool InputTransparent => false;

            public bool LockFocus => false;

            public bool BlockGesturesBelow => false;

            public bool CanDraw => true;

            public string Tag => nameof(GestureListener);

            public Guid Uid { get; } = Guid.NewGuid();

            public int ZIndex => 0;

            public DateTime? GestureListenerRegistrationTime { get; set; }

            public ISkiaGestureListener OnSkiaGestureEvent(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
            {
                return null;
            }

            public bool SetFrameworkFocus(bool focus)
            {
                return false;
            }

            public bool HitIsInside(float x, float y)
            {
                return false;
            }
        }
    }

    public static partial class ColorExtensions
    {
        public static DrawnUi.Color MakeDarker(this DrawnUi.Color color, double percent)
        {
            var factor = Math.Clamp(1.0 - percent / 100.0, 0.0, 1.0);
            return new DrawnUi.Color(
                color.Red * (float)factor,
                color.Green * (float)factor,
                color.Blue * (float)factor,
                color.Alpha);
        }

        public static DrawnUi.Color MakeLighter(this DrawnUi.Color color, double percent)
        {
            float Lerp(float channel) => channel + (1f - channel) * (float)Math.Clamp(percent / 100.0, 0.0, 1.0);

            return new DrawnUi.Color(
                Lerp(color.Red),
                Lerp(color.Green),
                Lerp(color.Blue),
                color.Alpha);
        }
    }

    public static partial class DrawnExtensions
    {
        public static DrawnUiStartupSettings StartupSettings { get; set; }

        public static void RegisterFont(string alias, string sourceUrl)
        {
            SkiaFontManager.Instance.RegisterFont(alias, sourceUrl);
        }

        public static void RegisterFont(string family, FontWeight weight, string sourceUrl)
        {
            SkiaFontManager.Instance.RegisterFont(family, weight, sourceUrl);
        }

        public static void RegisterImage(string sourceUrl)
        {
            SkiaImageManager.Instance.RegisterImage(sourceUrl);
        }

        public static void RegisterImage(string alias, string sourceUrl)
        {
            SkiaImageManager.Instance.RegisterImage(alias, sourceUrl);
        }

        public static void RegisterSvg(string sourceUrl)
        {
            SkiaSvg.RegisterSource(sourceUrl);
        }

        public static void RegisterSvg(string alias, string sourceUrl)
        {
            SkiaSvg.RegisterSource(alias, sourceUrl);
        }

        public static bool IsFinite(float value)
        {
            return float.IsFinite(value);
        }

        public static bool IsFinite(double value)
        {
            return double.IsFinite(value);
        }
    }
}

namespace DrawnUi.Views
{
}

namespace DrawnUi.Models
{
    public partial class Screen
    {
        public static DisplayInfo DisplayInfo => DeviceDisplay.Current.MainDisplayInfo;
    }
}

namespace DrawnUi.Extensions
{
    public static class SkiaCompatExtensions
    {
        public static DrawnUi.Rect ToMauiRectangle(this SKRect rect)
        {
            return new DrawnUi.Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }
    }
}
