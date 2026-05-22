namespace DrawnUi.Models
{
    /// <summary>
    /// ARIA role constants for use with <see cref="DrawnUi.Draw.SkiaControl.AccessibilityRole"/>.
    /// Pass these to <c>.WithAccessibility(Aria.RoleButton, ...)</c> to expose drawn controls
    /// to screen readers and assistive technology.
    /// </summary>
    public static class Aria
    {
        // ── Interactive widgets ────────────────────────────────────────────────

        /// <summary>A clickable element that triggers an action.</summary>
        public static readonly string RoleButton = "button";

        /// <summary>A navigational hyperlink.</summary>
        public static readonly string RoleLink = "link";

        /// <summary>A two-state on/off control.</summary>
        public static readonly string RoleCheckbox = "checkbox";

        /// <summary>A single-select option within a group of radio buttons.</summary>
        public static readonly string RoleRadio = "radio";

        /// <summary>A toggle switch with on/off semantics (use <c>aria-checked</c> for state).</summary>
        public static readonly string RoleSwitch = "switch";

        /// <summary>A draggable range control (e.g. volume, brightness).</summary>
        public static readonly string RoleSlider = "slider";

        /// <summary>A spin-button for incrementing/decrementing a numeric value.</summary>
        public static readonly string RoleSpinButton = "spinbutton";

        /// <summary>A single-line text input.</summary>
        public static readonly string RoleTextBox = "textbox";

        /// <summary>A text input specialised for search queries.</summary>
        public static readonly string RoleSearchBox = "searchbox";

        /// <summary>A combo-box: a text input paired with a pop-up list of options.</summary>
        public static readonly string RoleComboBox = "combobox";

        /// <summary>A scrollable list of selectable options.</summary>
        public static readonly string RoleListBox = "listbox";

        /// <summary>A single option inside a <see cref="RoleListBox"/>.</summary>
        public static readonly string RoleOption = "option";

        /// <summary>A tab in a tab-strip (paired with a <see cref="RoleTabPanel"/>).</summary>
        public static readonly string RoleTab = "tab";

        /// <summary>The content panel associated with a <see cref="RoleTab"/>.</summary>
        public static readonly string RoleTabPanel = "tabpanel";

        /// <summary>Container that holds a set of <see cref="RoleTab"/> elements.</summary>
        public static readonly string RoleTabList = "tablist";

        /// <summary>A menu — a list of choices or commands.</summary>
        public static readonly string RoleMenu = "menu";

        /// <summary>A single item inside a <see cref="RoleMenu"/>.</summary>
        public static readonly string RoleMenuItem = "menuitem";

        /// <summary>A menu item that acts as a checkbox.</summary>
        public static readonly string RoleMenuItemCheckbox = "menuitemcheckbox";

        /// <summary>A menu item that acts as a radio button.</summary>
        public static readonly string RoleMenuItemRadio = "menuitemradio";

        /// <summary>A scrollbar widget.</summary>
        public static readonly string RoleScrollBar = "scrollbar";

        // ── Structure & landmark ───────────────────────────────────────────────

        /// <summary>Static text — use when the element is purely informational.</summary>
        public static readonly string RoleText = "text";

        /// <summary>A heading (announce level via <c>aria-level</c> 1–6).</summary>
        public static readonly string RoleHeading = "heading";

        /// <summary>An image. Provide a meaningful <c>AccessibilityLabel</c>; use empty string for decorative images.</summary>
        public static readonly string RoleImg = "img";

        /// <summary>A list container (pairs with <see cref="RoleListItem"/>).</summary>
        public static readonly string RoleList = "list";

        /// <summary>A single item inside a <see cref="RoleList"/>.</summary>
        public static readonly string RoleListItem = "listitem";

        /// <summary>A horizontal or vertical visual divider.</summary>
        public static readonly string RoleSeparator = "separator";

        /// <summary>A progress indicator (determinate or indeterminate).</summary>
        public static readonly string RoleProgressBar = "progressbar";

        /// <summary>A small contextual tooltip.</summary>
        public static readonly string RoleTooltip = "tooltip";

        /// <summary>A dialog window (modal). Use <see cref="RoleAlertDialog"/> for urgent messages.</summary>
        public static readonly string RoleDialog = "dialog";

        /// <summary>A modal dialog that requires an immediate response (e.g. confirmation, error).</summary>
        public static readonly string RoleAlertDialog = "alertdialog";

        /// <summary>A live region for important but non-urgent status messages.</summary>
        public static readonly string RoleStatus = "status";

        /// <summary>A live region for urgent messages that interrupt the user.</summary>
        public static readonly string RoleAlert = "alert";

        /// <summary>A logical grouping of related controls.</summary>
        public static readonly string RoleGroup = "group";

        /// <summary>A landmark region with a user-visible label (provide <c>aria-label</c>).</summary>
        public static readonly string RoleRegion = "region";

        /// <summary>Primary navigation landmark.</summary>
        public static readonly string RoleNavigation = "navigation";

        /// <summary>Main content landmark — use once per page.</summary>
        public static readonly string RoleMain = "main";

        /// <summary>
        /// Removes all ARIA semantics from an element so assistive technology ignores it.
        /// Equivalent to <c>aria-hidden="true"</c> at the role level.
        /// </summary>
        public static readonly string RolePresentation = "presentation";
    }
}
