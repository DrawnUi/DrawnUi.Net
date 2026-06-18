using System.Globalization;
using DrawnUi.Blazor.Views;

namespace DrawnUi.Draw
{
    public static partial class FluentExtensions
    {


        public static T OnToggled<T>(this T view, Action<T, bool> action) where T : SkiaToggle
        {
            void onToggled(object? sender, bool isToggled)
            {
                action?.Invoke(view, isToggled);
            }

            view.Toggled += onToggled;

            string subscriptionKey = $"toggled_{Guid.NewGuid()}";
            view.ExecuteUponDisposal[subscriptionKey] = () => { view.Toggled -= onToggled; };

            return view;
        }

        public static Color WithLuminosity(this Color color, float luminosity)
        {
            float target = Math.Clamp(luminosity, 0f, 1f);
            float current = Math.Max(color.Red, Math.Max(color.Green, color.Blue));

            if (current <= 0f)
            {
                return new Color(target, target, target, color.Alpha);
            }

            float scale = target / current;
            return new Color(color.Red * scale, color.Green * scale, color.Blue * scale, color.Alpha);
        }

        private static GridLength ParseGridLength(string value)
        {
            if (string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return GridLength.Auto;
            }

            if (value.EndsWith('*'))
            {
                var weightText = value[..^1].Trim();
                if (string.IsNullOrEmpty(weightText))
                {
                    return GridLength.Star;
                }

                return new GridLength(double.Parse(weightText, CultureInfo.InvariantCulture), GridUnitType.Star);
            }

            return new GridLength(double.Parse(value, CultureInfo.InvariantCulture), GridUnitType.Absolute);
        }
    }
}
