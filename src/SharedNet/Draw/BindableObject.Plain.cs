using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    public partial class BindableObject : INotifyPropertyChanged
    {
        private readonly Dictionary<BindableProperty, object> _values = new();
        private object _bindingContext;

        public event PropertyChangingEventHandler PropertyChanging;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanging([CallerMemberName] string propertyName = "")
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }
        #endregion

        public virtual void SetPropertyValue(BindableProperty property, object value)
        {
            this.SetValue(property, value);
        }

        public object BindingContext
        {
            get => _bindingContext;
            set
            {
                if (ReferenceEquals(_bindingContext, value))
                    return;

                _bindingContext = value;
                OnPropertyChanged();
                OnBindingContextChanged();
            }
        }

        protected virtual void OnBindingContextChanged()
        {
        }

        protected virtual void InvalidateMeasure()
        {
        }

        // _values is read by the off-thread plane bake (Draw -> GetValue) WHILE the render/main thread writes
        // it (SetValue, or GetValue's own store-on-miss). A plain Dictionary corrupts under concurrent
        // read+write -> "concurrent operations" crash. Guard every structural access with this lock. Callbacks
        // (PropertyChanged / OnPropertyChanged) run OUTSIDE the lock to avoid reentrant/cross-control deadlock.
        // Uncontended (single render thread, e.g. plain CellsStack) the lock is ~20ns -> no measurable cost.
        private readonly object _valuesLock = new();

        // Lazily created on first explicit SetValue: GetValue's store-on-miss writes the DEFAULT into
        // _values, so key presence there cannot answer "was this property explicitly set".
        private HashSet<BindableProperty> _explicitlySet;

        /// <summary>
        /// Returns true if the property value was explicitly set via <see cref="SetValue"/>,
        /// mirroring MAUI's BindableObject.IsSet semantics.
        /// </summary>
        public bool IsSet(BindableProperty targetProperty)
        {
            if (targetProperty == null)
                throw new ArgumentNullException(nameof(targetProperty));

            lock (_valuesLock)
            {
                return _explicitlySet != null && _explicitlySet.Contains(targetProperty);
            }
        }

        public object GetValue(BindableProperty property)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            lock (_valuesLock)
            {
                if (_values.TryGetValue(property, out var value))
                    return value;
            }

            var defaultValue = property.GetDefaultValue(this);
            if (defaultValue != null || property.HasExplicitDefaultValue)
            {
                lock (_valuesLock)
                {
                    _values[property] = defaultValue;
                }
            }

            return defaultValue;
        }

        public void SetValue(BindableProperty property, object value)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            if (!property.IsValid(this, value))
                throw new ArgumentException($"Invalid value for property {property.PropertyName}.", nameof(value));

            value = property.Coerce(this, value);

            bool hadExistingValue;
            object oldValue;
            lock (_valuesLock)
            {
                hadExistingValue = _values.TryGetValue(property, out oldValue);
                (_explicitlySet ??= new()).Add(property);
            }
            if (!hadExistingValue)
            {
                oldValue = property.GetDefaultValue(this);
            }

            if (Equals(oldValue, value))
                return;

            OnPropertyChanging(property.PropertyName);
            lock (_valuesLock)
            {
                _values[property] = value;
            }
            property.PropertyChanged?.Invoke(this, oldValue, value);
            OnPropertyChanged(property.PropertyName);
        }

        public static void SetInheritedBindingContext(BindableObject bindable, object value)
        {
            if (bindable != null)
            {
                bindable.BindingContext = value;
            }
        }


#if !BROWSER
        public string Tag { get; set; }
#endif
    }

    public sealed class BindableProperty
    {
        private BindableProperty(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue,
            Func<BindableObject, object> defaultValueCreator,
            Func<BindableObject, object, bool> validateValue,
            Action<BindableObject, object, object> propertyChanged,
            Func<BindableObject, object, object> coerceValue)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
            DefaultValue = defaultValue;
            DefaultValueCreator = defaultValueCreator;
            ValidateValue = validateValue;
            PropertyChanged = propertyChanged;
            CoerceValue = coerceValue;
            HasExplicitDefaultValue = defaultValueCreator != null || defaultValue != null;
        }

        public string PropertyName { get; }

        public Type ReturnType { get; }

        public Type DeclaringType { get; }

        public object DefaultValue { get; }

        public bool HasExplicitDefaultValue { get; }

        internal Func<BindableObject, object> DefaultValueCreator { get; }

        internal Func<BindableObject, object, bool> ValidateValue { get; }

        internal Action<BindableObject, object, object> PropertyChanged { get; }

        internal Func<BindableObject, object, object> CoerceValue { get; }

        public static BindableProperty Create(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue = null,
            Func<BindableObject, object> defaultValueCreator = null,
            Func<BindableObject, object, bool> validateValue = null,
            Action<BindableObject, object, object> propertyChanged = null,
            Func<BindableObject, object, object> coerceValue = null)
        {
            return new BindableProperty(
                propertyName,
                returnType,
                declaringType,
                defaultValue,
                defaultValueCreator,
                validateValue,
                propertyChanged,
                coerceValue);
        }

        public static BindableProperty Create(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue,
            BindingMode defaultBindingMode,
            Func<BindableObject, object> defaultValueCreator = null,
            Func<BindableObject, object, bool> validateValue = null,
            Action<BindableObject, object, object> propertyChanged = null,
            Func<BindableObject, object, object> coerceValue = null)
        {
            return Create(
                propertyName,
                returnType,
                declaringType,
                defaultValue,
                defaultValueCreator,
                validateValue,
                propertyChanged,
                coerceValue);
        }

        internal object GetDefaultValue(BindableObject bindable)
        {
            return DefaultValueCreator?.Invoke(bindable) ?? DefaultValue;
        }

        internal bool IsValid(BindableObject bindable, object value)
        {
            return ValidateValue?.Invoke(bindable, value) ?? true;
        }

        internal object Coerce(BindableObject bindable, object value)
        {
            return CoerceValue?.Invoke(bindable, value) ?? value;
        }

        public static BindableProperty CreateAttached(
            string propertyName,
            Type returnType,
            Type declaringType,
            object defaultValue = null,
            Func<object, object, bool> validateValue = null,
            Action<BindableObject, object, object> propertyChanged = null)
        {
            return new BindableProperty(
                propertyName,
                returnType,
                declaringType,
                defaultValue,
                null,
                (bindable, value) => validateValue?.Invoke(bindable, value) ?? true,
                propertyChanged,
                null);
        }
    }
}
