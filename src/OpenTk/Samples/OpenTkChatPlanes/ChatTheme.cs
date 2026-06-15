namespace DrawnUiRepro;

/// <summary>
/// Telegram-dark inspired palette, single source for page and cells.
/// </summary>
public static class ChatTheme
{
    public static readonly Color Bg = Color.FromArgb("#0E1621");           //chat background
    public static readonly Color BarBg = Color.FromArgb("#17212B");        //navbar / typing bar / panels
    public static readonly Color InputBg = Color.FromArgb("#242F3D");      //editor field
    public static readonly Color Accent = Color.FromArgb("#5288C1");       //telegram blue (send, quotes)
    public static readonly Color AccentBright = Color.FromArgb("#64B5EF"); //read checks, typing status
    public static readonly Color IconMuted = Color.FromArgb("#6C7883");    //bar icons, secondary text
    public static readonly Color Incoming = Color.FromArgb("#182533");     //incoming bubble
    public static readonly Color Outgoing = Color.FromArgb("#2B5278");     //outgoing bubble
    public static readonly Color TimeIncoming = Color.FromArgb("#6C7883");
    public static readonly Color TimeOutgoing = Color.FromArgb("#7DA8D3");
    public static readonly Color Check = Color.FromArgb("#8BAAC9");        //sent/delivered checks
}
