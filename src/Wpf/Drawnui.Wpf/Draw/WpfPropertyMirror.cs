using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace DrawnUi.Draw;

/// <summary>
/// Gives every DrawnUI bindable property a real <see cref="DependencyProperty"/> so WPF can bind to
/// it, style it and animate it — without moving storage out of DrawnUI.
/// <para>
/// The DependencyProperty is a surface, not a store. WPF writes (a binding, a Style setter, a
/// trigger) land in its changed callback, which forwards to the control's ordinary
/// <c>SetValue(BindableProperty, value)</c>; the value then lives in the lock-guarded dictionary in
/// SharedNet's BindableObject, exactly as on every other head. Writes coming the other way — from the
/// engine — are pushed back onto the DependencyProperty only when they happen on the UI thread, so
/// DrawnUI's background work never touches a thread-affine WPF object.
/// </para>
/// </summary>
internal static class WpfPropertyMirror
{
    private sealed class Entry
    {
        public DependencyProperty Property;

        /// <summary>Null for a plain CLR property (the View-level ones); then <see cref="Clr"/> is the store.</summary>
        public BindableProperty Bindable;

        public PropertyInfo Clr;

        public object Get(BindableObject target) => Bindable != null ? target.GetValue(Bindable) : Clr.GetValue(target);

        public void Set(BindableObject target, object value)
        {
            if (Bindable != null)
                target.SetValue(Bindable, value);
            else
                Clr.SetValue(target, value);
        }
    }

    /// <summary>
    /// SharedNet's View declares the MAUI VisualElement surface (IsVisible, InputTransparent,
    /// Rotation, Background, Clip…) as plain CLR properties raising PropertyChanged, not as
    /// BindableProperties. They are mirrored too, with the engine default read off a throwaway View.
    /// </summary>
    private static readonly Lazy<View> ViewDefaults = new(() =>
    {
        // Constructing a View would register View itself, from inside a registration that is
        // waiting on this very instance. Skip registration for the throwaway.
        _creatingDefaults = true;
        try
        {
            return new View();
        }
        finally
        {
            _creatingDefaults = false;
        }
    });

    [ThreadStatic]
    private static bool _creatingDefaults;

    private static bool IsPlainViewProperty(PropertyInfo clr)
        => clr.DeclaringType == typeof(View) && clr.Name != nameof(View.Handler);

    private static readonly ConcurrentDictionary<Type, Dictionary<string, Entry>> ByType = new();

    private static readonly HashSet<(Type Owner, string Name)> Registered = new();

    private static readonly Dictionary<(Type Owner, string Name), DependencyProperty> RegisteredProperties = new();

    /// <summary>
    /// Names FrameworkContentElement already owns as settable properties — Style, IsEnabled, Tag,
    /// Name, DataContext and friends. DrawnUI shadows several of them with its own meaning; mirroring
    /// those would put two writable properties of the same name on one object and make XAML ambiguous,
    /// so WPF keeps them and they are simply not bindable through the mirror.
    /// </summary>
    private static readonly HashSet<string> WpfOwnedNames = new(
        typeof(FrameworkContentElement)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name),
        StringComparer.Ordinal);

    /// <summary>
    /// Registers every drawn type in this assembly up front.
    /// <para>
    /// Registering lazily per constructor is not enough: the XAML parser resolves a member before it
    /// creates the instance, so for a property that shadows a base one — <c>SkiaShape.Type</c> is
    /// <c>new ShapeType</c> over <c>SkiaLayout.Type</c> — it would find only the base type's mirror
    /// and convert the value to the wrong type. Every type has to be known before any XAML is read.
    /// </para>
    /// </summary>
    [ModuleInitializer]
    internal static void RegisterAllDrawnTypes()
    {
        Type[] types;
        try
        {
            types = typeof(BindableObject).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).ToArray();
        }

        foreach (var type in types)
        {
            if (!typeof(BindableObject).IsAssignableFrom(type) || type.IsGenericTypeDefinition)
                continue;

            try
            {
                EnsureRegistered(type);
            }
            catch
            {
                // One awkward type must not take the whole app down at module load; it simply ends up
                // without a mirror and is still usable from code.
            }
        }
    }

    /// <summary>
    /// Registers the mirror for a control type, once. Also called from the BindableObject constructor
    /// so that types living outside this assembly — an app's own drawn controls — are covered too.
    /// </summary>
    public static void EnsureRegistered(Type type)
    {
        if (_creatingDefaults)
            return;

        GetEntries(type);
    }

    private static Dictionary<string, Entry> GetEntries(Type type)
        => ByType.GetOrAdd(type, Build);

    private static Dictionary<string, Entry> Build(Type type)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        // A name can be declared more than once down the hierarchy — SkiaControl re-declares several
        // of View's properties with `new`. XAML binds to the most derived one, so that is the one
        // that gets the mirror, and using its DeclaringType as the DependencyProperty owner means the
        // base declaration never collides with it.
        foreach (var group in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .GroupBy(p => p.Name, StringComparer.Ordinal))
        {
            var clr = MostDerived(group);
            if (clr == null || !clr.CanRead || !clr.CanWrite || clr.GetIndexParameters().Length > 0)
                continue;

            if (WpfOwnedNames.Contains(clr.Name))
                continue;

            var bindable = FindBindableProperty(clr);
            if (bindable == null && !IsPlainViewProperty(clr))
                continue;

            var owner = clr.DeclaringType;
            if (owner == null)
                continue;

            DependencyProperty dp;
            lock (Registered)
            {
                if (!Registered.Add((owner, clr.Name)))
                {
                    // Already mirrored while walking another type that inherits this declaration.
                    if (RegisteredProperties.TryGetValue((owner, clr.Name), out dp))
                        entries[clr.Name] = new Entry { Property = dp, Bindable = bindable, Clr = clr };

                    continue;
                }

                try
                {
                    dp = Register(clr, owner, bindable);
                }
                catch
                {
                    // Not registered after all: leave the name free for a retry instead of
                    // poisoning it, which would make every later Build of this type skip it silently.
                    Registered.Remove((owner, clr.Name));
                    throw;
                }

                RegisteredProperties[(owner, clr.Name)] = dp;
            }

            entries[clr.Name] = new Entry { Property = dp, Bindable = bindable, Clr = clr };
        }

        return entries;
    }

    /// <summary>
    /// The DependencyProperty default must equal the engine default. WPF skips the changed callback
    /// when a write equals the current effective value, so with a type default (false, 0, null) a
    /// XAML <c>IsVisible="False"</c> on a control whose engine default is true would never reach the
    /// engine. Defaults WPF refuses (unfrozen Freezables and the like) fall back to the type default.
    /// </summary>
    private static DependencyProperty Register(PropertyInfo clr, Type owner, BindableProperty bindable)
    {
        var typeDefault = clr.PropertyType.IsValueType ? Activator.CreateInstance(clr.PropertyType) : null;
        var engineDefault = bindable != null ? bindable.DefaultValue : clr.GetValue(ViewDefaults.Value);
        var useEngine = engineDefault != null
            ? clr.PropertyType.IsInstanceOfType(engineDefault)
            : !clr.PropertyType.IsValueType || Nullable.GetUnderlyingType(clr.PropertyType) != null;

        if (useEngine)
        {
            try
            {
                return DependencyProperty.RegisterAttached(clr.Name, clr.PropertyType, owner,
                    new FrameworkPropertyMetadata(engineDefault, FrameworkPropertyMetadataOptions.None, OnWpfValueChanged));
            }
            catch (ArgumentException)
            {
                // default not accepted by the property system
            }
        }

        return DependencyProperty.RegisterAttached(clr.Name, clr.PropertyType, owner,
            new FrameworkPropertyMetadata(typeDefault, FrameworkPropertyMetadataOptions.None, OnWpfValueChanged));
    }

    private static PropertyInfo MostDerived(IEnumerable<PropertyInfo> group)
    {
        PropertyInfo best = null;
        foreach (var candidate in group)
        {
            if (best == null || (candidate.DeclaringType != null && best.DeclaringType != null
                                                                 && best.DeclaringType.IsAssignableFrom(candidate.DeclaringType)))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static BindableProperty FindBindableProperty(PropertyInfo clr)
    {
        // Convention used everywhere in DrawnUI: `Text` is backed by a static `TextProperty` field.
        var field = clr.DeclaringType?.GetField(
            clr.Name + "Property",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        return field?.GetValue(null) as BindableProperty;
    }


    private static void OnWpfValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not BindableObject bindable || bindable.SyncingFromMirror)
            return;

        if (!GetEntries(bindable.GetType()).TryGetValue(e.Property.Name, out var entry))
            return;

        // From now on this property has a WPF-side consumer (binding, style, trigger, local value),
        // so engine writes to it are worth publishing back. Everything else stays engine-only and
        // never touches the property system.
        (bindable.WpfTouched ??= new HashSet<string>(StringComparer.Ordinal)).Add(e.Property.Name);

        bindable.SyncingFromMirror = true;
        try
        {
            entry.Set(bindable, e.NewValue);
        }
        finally
        {
            bindable.SyncingFromMirror = false;
        }
    }

    /// <summary>
    /// Publishes an engine-side value onto the DependencyProperty, so a TwoWay binding reads it back.
    /// Caller guarantees the UI thread and that WPF has touched the property (<see cref="BindableObject.WpfTouched"/>).
    /// </summary>
    public static void PushToWpf(BindableObject bindable, string propertyName)
    {
        if (!GetEntries(bindable.GetType()).TryGetValue(propertyName, out var entry))
            return;

        object value;
        try
        {
            value = entry.Get(bindable);
        }
        catch
        {
            return;
        }

        bindable.SyncingFromMirror = true;
        try
        {
            // SetCurrentValue, not SetValue: publishing the engine's value must not overwrite a
            // binding or clobber the property's local/style precedence.
            bindable.SetCurrentValue(entry.Property, value);
        }
        catch
        {
            // A value the DependencyProperty will not accept is not worth failing a render over.
        }
        finally
        {
            bindable.SyncingFromMirror = false;
        }
    }
}
