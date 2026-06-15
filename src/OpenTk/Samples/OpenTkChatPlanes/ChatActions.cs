namespace DrawnUiRepro;

/// <summary>The 3 cell->page callbacks ChatCell expects via Parent.BindingContext.</summary>
public interface IChatCellActions
{
    void ShowImageFullscreen(ChatMessage msg);
    void ReplyToMessage(ChatMessage msg);
    void ScrollToMessage(ChatMessage msg);
}
