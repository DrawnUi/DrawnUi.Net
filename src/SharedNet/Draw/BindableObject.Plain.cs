using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    public partial class BindableObject : INotifyPropertyChanged
    {
        // keyed by the property's id (every property maps to one id), in a small table of its own: property reads are
        // the most frequent call of a frame, and a Dictionary lookup costs a comparer call and more on the AOT web heads
        private readonly PropertyValues _values = new();

        /// <summary>
        /// Values by property id: open addressing with linear probing over an array sized a power of two, ids start at
        /// 1 so 0 marks a free slot. Writes run under <see cref="_valuesLock"/>; reads take no lock. A slot only ever
        /// holds one property: its value is stored before its key, a removed property leaves a tombstone that is never
        /// reused, and a grown table is published whole, so a read on another thread (an off-thread cache bake) sees
        /// the property's old or new value, never another property's.
        /// </summary>
        private sealed class PropertyValues
        {
            private const int Tombstone = -1;

            private struct Entry
            {
                public int Key;
                public object Value;
            }

            private Entry[] _entries = new Entry[8];
            private int _used; // keys and tombstones

            public bool TryGetValue(int id, out object value)
            {
                var entries = Volatile.Read(ref _entries);
                var mask = entries.Length - 1;
                for (var i = id & mask; ; i = (i + 1) & mask)
                {
                    var key = Volatile.Read(ref entries[i].Key);
                    if (key == id)
                    {
                        value = Volatile.Read(ref entries[i].Value);
                        return true;
                    }

                    if (key == 0)
                    {
                        value = null;
                        return false;
                    }
                }
            }

            public object this[int id]
            {
                set
                {
                    var entries = _entries;
                    var mask = entries.Length - 1;
                    for (var i = id & mask; ; i = (i + 1) & mask)
                    {
                        var key = entries[i].Key;
                        if (key == id)
                        {
                            Volatile.Write(ref entries[i].Value, value);
                            return;
                        }

                        if (key == 0)
                        {
                            entries[i].Value = value;
                            Volatile.Write(ref entries[i].Key, id);
                            // grow at three quarters, so a probe always reaches a free slot
                            if (++_used * 4 > entries.Length * 3)
                                Grow();
                            return;
                        }
                    }
                }
            }

            public bool Remove(int id, out object value)
            {
                var entries = _entries;
                var mask = entries.Length - 1;
                for (var i = id & mask; ; i = (i + 1) & mask)
                {
                    var key = entries[i].Key;
                    if (key == 0)
                    {
                        value = null;
                        return false;
                    }

                    if (key == id)
                    {
                        value = entries[i].Value;
                        // the value stays for a read already past the key
                        Volatile.Write(ref entries[i].Key, Tombstone);
                        return true;
                    }
                }
            }

            private void Grow()
            {
                var entries = _entries;
                var live = 0;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Key > 0)
                        live++;
                }

                // only tombstones filled it: same size, rebuilt without them
                var size = entries.Length;
                while (live * 2 > size)
                    size *= 2;

                var grown = new Entry[size];
                var mask = size - 1;
                for (var i = 0; i < entries.Length; i++)
                {
                    var key = entries[i].Key;
                    if (key <= 0)
                        continue;
                    var j = key & mask;
                    while (grown[j].Key != 0)
                        j = (j + 1) & mask;
                    grown[j] = entries[i];
                }

                _used = live;
                Volatile.Write(ref _entries, grown);
            }
        }
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
        // it (SetValue, or GetValue's own store-on-miss). Every write takes this lock; reads take none (see
        // PropertyValues). Callbacks (PropertyChanged / OnPropertyChanged) run OUTSIDE the lock to avoid
        // reentrant/cross-control deadlock.
        private readonly object _valuesLock = new();

        // Lazily created on first explicit SetValue: GetValue's store-on-miss writes the DEFAULT into
        // _values, so key presence there cannot answer "was this property explicitly set".
        private HashSet<int> _explicitlySet;

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
                return _explicitlySet != null && _explicitlySet.Contains(targetProperty.Id);
            }
        }

        public object GetValue(BindableProperty property)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            if (_values.TryGetValue(property.Id, out var value))
                return value;

            var defaultValue = property.GetDefaultValue(this);
            if (defaultValue != null || property.HasExplicitDefaultValue)
            {
                lock (_valuesLock)
                {
                    // set meanwhile on another thread: that value stays
                    if (_values.TryGetValue(property.Id, out value))
                        return value;
                    _values[property.Id] = defaultValue;
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
                hadExistingValue = _values.TryGetValue(property.Id, out oldValue);
                (_explicitlySet ??= new()).Add(property.Id);
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
                _values[property.Id] = value;
            }
            property.PropertyChanged?.Invoke(this, oldValue, value);
            OnPropertyChanged(property.PropertyName);
        }

        /// <summary>
        /// Resets the property to its default value and forgets that it was set (<see cref="IsSet"/> becomes false),
        /// mirroring MAUI's BindableObject.ClearValue. Raises the change callbacks when the value actually changes.
        /// </summary>
        public void ClearValue(BindableProperty property)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            bool hadExistingValue;
            object oldValue;
            lock (_valuesLock)
            {
                hadExistingValue = _values.Remove(property.Id, out oldValue);
                _explicitlySet?.Remove(property.Id);
            }

            if (!hadExistingValue)
                return;

            var defaultValue = property.GetDefaultValue(this);
            if (Equals(oldValue, defaultValue))
                return;

            OnPropertyChanging(property.PropertyName);
            property.PropertyChanged?.Invoke(this, oldValue, defaultValue);
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
            Id = Interlocked.Increment(ref _lastId);
        }

        private static int _lastId;

        /// <summary>A number of its own for every property, the key its values are kept under.</summary>
        internal int Id { get; }

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
