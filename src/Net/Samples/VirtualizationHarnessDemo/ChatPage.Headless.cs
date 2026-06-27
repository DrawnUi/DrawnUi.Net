namespace DrawnChatList;

// Headless head partial for ChatPage: supplies the one member the shared code expects from a platform
// head (the OpenTk head provides it in ChatPage.OpenTk.cs). Lets the harness compile the REAL ChatPage
// against DrawnUi.Net without a windowing head.
public sealed partial class ChatPage : BindableObject, IChatCellActions
{
    public float KeyboardSize { get; set; }

    // Probe accessors into the private windowed source (same class -> can read it).
    public int ProbeWindowStart => _window.WindowStart;
    public int ProbeWindowEnd => _window.WindowEnd;
    public int ProbeResident => _window.Items.Count;
}
