using System.Runtime.InteropServices;

namespace DrawnUi.Draw;

// ── Windows UIA COM interface definitions ────────────────────────────────────
// Shared between all DrawnUI Windows targets (MAUI Windows, OpenTK Windows).
// Must be public — .NET's CCW does not expose internal types via QueryInterface.
// Same IIDs as UIAutomationCore.dll. COM QI matches by GUID, not .NET type name.

[ComVisible(true)]
[Guid("D6DD68D1-86FD-4332-8666-9ABEDEA2D24C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRawElementProviderSimple
{
    UiaProviderOptions ProviderOptions { get; }

    [return: MarshalAs(UnmanagedType.IUnknown)]
    object? GetPatternProvider(int patternId);

    [return: MarshalAs(UnmanagedType.Struct)]
    object? GetPropertyValue(int propertyId);

    [return: MarshalAs(UnmanagedType.Interface)]
    IRawElementProviderSimple? HostRawElementProvider { get; }
}

[ComVisible(true)]
[Guid("F7063DA8-8359-439C-9297-BBC5299A7D87")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRawElementProviderFragment
{
    [return: MarshalAs(UnmanagedType.Interface)]
    IRawElementProviderFragment? Navigate(UiaNavigateDirection direction);

    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_I4)]
    int[]? GetRuntimeId();

    UiaRect BoundingRectangle { get; }

    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_UNKNOWN)]
    object[]? GetEmbeddedFragmentRoots();

    void SetFocus();

    [return: MarshalAs(UnmanagedType.Interface)]
    IRawElementProviderFragmentRoot? FragmentRoot { get; }
}

[ComVisible(true)]
[Guid("620CE2A5-AB8F-40A9-86CB-DE3C75599B58")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRawElementProviderFragmentRoot
{
    [return: MarshalAs(UnmanagedType.Interface)]
    IRawElementProviderFragment? ElementProviderFromPoint(double x, double y);

    [return: MarshalAs(UnmanagedType.Interface)]
    IRawElementProviderFragment? GetFocus();
}

// ── UIA pattern providers ─────────────────────────────────────────────────────

[ComVisible(true)]
[Guid("54FCB24B-E18E-47a2-B4AF-9BC1184F912F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IInvokeProvider
{
    void Invoke();
}

// ── Supporting enums / structs ────────────────────────────────────────────────

[Flags]
public enum UiaProviderOptions
{
    ServerSideProvider = 0x1,
    UseComThreading    = 0x20,
}

public enum UiaNavigateDirection { Parent = 0, NextSibling = 1, PreviousSibling = 2, FirstChild = 3, LastChild = 4 }

[StructLayout(LayoutKind.Sequential)]
public struct UiaRect
{
    public double left, top, width, height;
}

// ── Windows UIA constants ─────────────────────────────────────────────────────

internal static class UiaPatternId
{
    public const int Invoke = 10000;
}

internal static class UiaPropertyId
{
    public const int BoundingRectangle    = 30001;
    public const int ControlType          = 30003;
    public const int LocalizedControlType = 30004;
    public const int Name                 = 30005;
    public const int IsKeyboardFocusable  = 30009;
    public const int IsEnabled            = 30010;
    public const int HelpText             = 30013;
    public const int IsControlElement     = 30016;
    public const int IsContentElement     = 30017;
}

internal static class UiaControlType
{
    public const int Button      = 50000;
    public const int CheckBox    = 50002;
    public const int ComboBox    = 50003;
    public const int Edit        = 50004;
    public const int Hyperlink   = 50005;
    public const int Image       = 50006;
    public const int ListItem    = 50007;
    public const int List        = 50008;
    public const int Menu        = 50009;
    public const int MenuBar     = 50010;
    public const int MenuItem    = 50011;
    public const int ProgressBar = 50012;
    public const int RadioButton = 50013;
    public const int ScrollBar   = 50014;
    public const int Slider      = 50015;
    public const int Spinner     = 50016;
    public const int StatusBar   = 50017;
    public const int Tab         = 50018;
    public const int TabItem     = 50019;
    public const int Text        = 50020;
    public const int ToolTip     = 50022;
    public const int Custom      = 50025;
    public const int Group       = 50026;
    public const int Pane        = 50033;
    public const int Separator   = 50038;
}
