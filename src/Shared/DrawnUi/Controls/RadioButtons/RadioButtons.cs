namespace DrawnUi.Controls
{
    /// <summary>
    /// Manages radio button groups, ensuring only one button is selected per group.
    /// Supports grouping by parent control or by string name.
    /// </summary>
    public class RadioButtons
    {
        static RadioButtons _instance;

        /// <summary>
        /// Gets the singleton instance of the RadioButtons manager.
        /// </summary>
        public static RadioButtons All
        {
            get
            {
                if (_instance == null)
                    _instance = new RadioButtons();

                return _instance;
            }
        }

        /// <summary>
        /// Occurs when a radio button selection changes in any group.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>
        /// Gets the currently selected radio button in the group associated with the specified parent control.
        /// </summary>
        /// <param name="parent">The parent control that defines the radio button group.</param>
        /// <returns>The selected SkiaControl, or null if no button is selected or group doesn't exist.</returns>
        public SkiaControl GetSelected(SkiaControl parent)
        {
            return FindGroup(parent)?.FirstOrDefault(c => c.GetValueInternal()) as SkiaControl;
        }

        /// <summary>
        /// Gets the currently selected radio button in the group with the specified name.
        /// </summary>
        /// <param name="groupName">The name of the radio button group.</param>
        /// <returns>The selected SkiaControl, or null if no button is selected or group doesn't exist.</returns>
        public SkiaControl GetSelected(string groupName)
        {
            return FindGroup(groupName)?.FirstOrDefault(c => c.GetValueInternal()) as SkiaControl;
        }

        /// <summary>
        /// Gets the index of the currently selected radio button in the group associated with the specified parent control.
        /// </summary>
        /// <param name="parent">The parent control that defines the radio button group.</param>
        /// <returns>The zero-based index of the selected button, or -1 if no button is selected or group doesn't exist.</returns>
        public int GetSelectedIndex(SkiaControl parent)
        {
            var group = FindGroup(parent);
            return group == null ? -1 : GetSelectedIndexInternal(group);
        }

        /// <summary>
        /// Gets the index of the currently selected radio button in the group with the specified name.
        /// </summary>
        /// <param name="groupName">The name of the radio button group.</param>
        /// <returns>The zero-based index of the selected button, or -1 if no button is selected or group doesn't exist.</returns>
        public int GetSelectedIndex(string groupName)
        {
            var group = FindGroup(groupName);
            return group == null ? -1 : GetSelectedIndexInternal(group);
        }

        int GetSelectedIndexInternal(List<ISkiaRadioButton> group)
        {
            var index = -1;
            foreach (ISkiaRadioButton radio in group)
            {
                index++;
                if (radio.GetValueInternal())
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// Selects the radio button at the specified index in the group associated with the container control.
        /// </summary>
        /// <param name="container">The parent control that defines the radio button group.</param>
        /// <param name="index">The zero-based index of the button to select.</param>
        public void Select(SkiaControl container, int index)
        {
            var group = FindGroup(container);
            if (group != null)
            {
                var i = -1;
                foreach (ISkiaRadioButton radio in group)
                {
                    i++;
                    radio.SetValueInternal(index == i);
                }
            }
        }

        /// <summary>
        /// The members of one group, held weakly: a page dropped without Dispose must not stay alive through this static
        /// manager. Parent-based groups are also keyed weakly by their parent.
        /// </summary>
        private sealed class Group
        {
            private readonly List<WeakReference<ISkiaRadioButton>> _items = new();

            /// <summary>The members still alive, in the order they joined.</summary>
            public List<ISkiaRadioButton> Live()
            {
                var live = new List<ISkiaRadioButton>(_items.Count);
                _items.RemoveAll(w =>
                {
                    if (!w.TryGetTarget(out var radio))
                        return true;
                    live.Add(radio);
                    return false;
                });
                return live;
            }

            public void Add(ISkiaRadioButton control)
            {
                if (!Live().Any(r => ReferenceEquals(r, control)))
                    _items.Add(new WeakReference<ISkiaRadioButton>(control));
            }

            public bool Remove(ISkiaRadioButton control) =>
                _items.RemoveAll(w => w.TryGetTarget(out var r) && ReferenceEquals(r, control)) > 0;
        }

        private readonly Dictionary<string, Group> _byName = new();
        private readonly ConditionalWeakTable<SkiaControl, Group> _byParent = new();

        private List<ISkiaRadioButton> FindGroup(SkiaControl parent) =>
            parent != null && _byParent.TryGetValue(parent, out var group) ? group.Live() : null;

        private List<ISkiaRadioButton> FindGroup(string groupName) =>
            groupName != null && _byName.TryGetValue(groupName, out var group) ? group.Live() : null;

        /// <summary>A snapshot of the named groups and their live members; changing it changes nothing.</summary>
        [Obsolete("A read-only snapshot: groups hold their members weakly.")]
        protected Dictionary<string, List<ISkiaRadioButton>> GroupsByName =>
            _byName.ToDictionary(x => x.Key, x => x.Value.Live());

        /// <summary>A snapshot of the parent-based groups and their live members; changing it changes nothing.</summary>
        [Obsolete("A read-only snapshot: groups hold their members weakly.")]
        protected Dictionary<SkiaControl, List<ISkiaRadioButton>> GroupsByParent =>
            _byParent.ToDictionary(x => x.Key, x => x.Value.Live());

        /// <summary>
        /// Initializes a new instance of the RadioButtons class.
        /// </summary>
        public RadioButtons()
        {
        }

        /// <summary>
        /// Adds a radio button control to a named group. Ensures at least one button in the group is selected.
        /// </summary>
        /// <param name="control">The radio button control to add to the group.</param>
        /// <param name="groupName">The name of the group to add the control to.</param>
        public void AddToGroup(ISkiaRadioButton control, string groupName)
        {
            if (!_byName.TryGetValue(groupName, out var group))
                _byName[groupName] = group = new Group();

            group.Add(control);
            Normalize(group.Live());
        }

        /// <summary>
        /// Adds a radio button control to a parent-based group. Ensures at least one button in the group is selected.
        /// </summary>
        /// <param name="control">The radio button control to add to the group.</param>
        /// <param name="parent">The parent control that defines the group.</param>
        public void AddToGroup(ISkiaRadioButton control, SkiaControl parent)
        {
            var group = _byParent.GetValue(parent, _ => new Group());
            group.Add(control);
            Normalize(group.Live());
        }

        /// <summary>
        /// Removes a radio button control from a named group. Ensures at least one button remains selected in the group.
        /// </summary>
        /// <param name="groupName">The name of the group to remove the control from.</param>
        /// <param name="control">The radio button control to remove.</param>
        public void RemoveFromGroup(string groupName, ISkiaRadioButton control)
        {
            if (_byName.TryGetValue(groupName, out var group) && group.Remove(control))
                AfterRemove(group, () => _byName.Remove(groupName));
        }

        /// <summary>
        /// Removes a radio button control from a parent-based group. Ensures at least one button remains selected in the group.
        /// </summary>
        /// <param name="parent">The parent control that defines the group to remove the control from.</param>
        /// <param name="control">The radio button control to remove.</param>
        public void RemoveFromGroup(SkiaControl parent, ISkiaRadioButton control)
        {
            if (_byParent.TryGetValue(parent, out var group) && group.Remove(control))
                AfterRemove(group, () => _byParent.Remove(parent));
        }

        /// <summary>
        /// Removes a radio button control from all groups it belongs to. Ensures at least one button remains selected in affected groups.
        /// </summary>
        /// <param name="control">The radio button control to remove from all groups.</param>
        public void RemoveFromGroups(ISkiaRadioButton control)
        {
            foreach (var pair in _byName.ToList())
            {
                if (pair.Value.Remove(control))
                    AfterRemove(pair.Value, () => _byName.Remove(pair.Key));
            }

            foreach (var pair in _byParent.ToList())
            {
                if (pair.Value.Remove(control))
                    AfterRemove(pair.Value, () => _byParent.Remove(pair.Key));
            }
        }

        // an empty group is dropped (a named group would keep its name forever, a parent group its key)
        private void AfterRemove(Group group, Action dropEmpty)
        {
            var live = group.Live();
            if (live.Count == 0)
                dropEmpty();
            else
                Normalize(live);
        }

        private void Normalize(List<ISkiaRadioButton> group)
        {
            NormalizeGroupSelection(group);
            SyncGroupVisuals(group);
        }

        private void NormalizeGroupSelection(List<ISkiaRadioButton> group)
        {
            if (group == null || group.Count == 0)
            {
                return;
            }

            var selected = group.Where(c => c.GetValueInternal()).ToList();
            if (selected.Count == 0)
            {
                group[0].SetValueInternal(true);
                return;
            }

            var keeper = selected[0];
            foreach (var radio in selected)
            {
                if (!ReferenceEquals(radio, keeper))
                {
                    radio.SetValueInternal(false);
                }
            }
        }

        private static void SyncGroupVisuals(List<ISkiaRadioButton> group)
        {
            if (group == null)
            {
                return;
            }

            foreach (var radio in group)
            {
                if (radio is SkiaToggle toggle)
                {
                    toggle.ApplyProperties();
                }
            }
        }

        /// <summary>
        /// Called by radio button controls to report value changes. Manages mutual exclusion within groups and fires the Changed event.
        /// </summary>
        /// <param name="control">The radio button control reporting the change.</param>
        /// <param name="newValue">The new value of the control (true for selected, false for unselected).</param>
        public void ReportValueChange(ISkiaRadioButton control, bool newValue)
        {
            var group = _byName.Values.Select(g => g.Live()).FirstOrDefault(g => g.Contains(control))
                        ?? _byParent.Select(x => x.Value.Live()).FirstOrDefault(g => g.Contains(control));
            if (group == null)
                return;

            if (newValue)
            {
                group.ForEach(c => { if (c != control) c.SetValueInternal(false); });
                Changed?.Invoke(control, EventArgs.Empty);
            }
            else
            {
                EnsureOneTrueInGroup(group, control);
            }
        }

        private void EnsureOneTrueInGroup(List<ISkiaRadioButton> group, ISkiaRadioButton exceptControl)
        {
            var trueControls = group?.Where(c => c.GetValueInternal()).ToList();

            if (trueControls == null || trueControls.Count == 0 || (trueControls.Count == 1 && trueControls.Contains(exceptControl)))
            {
                var controlToSetTrue = group?.FirstOrDefault(c => c != exceptControl);
                controlToSetTrue?.SetValueInternal(true);
            }
        }
    }
}
