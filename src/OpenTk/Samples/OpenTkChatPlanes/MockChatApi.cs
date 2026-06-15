namespace DrawnUiRepro;

/// <summary>
/// Mock chat backend: "sends" a message through fake network stages
/// (Sent -> Delivered -> Read) mutating the message on the UI thread,
/// then replies with an incoming message after a typing pause.
/// </summary>
public class MockChatApi
{
    private readonly Random _rnd = new();

    /// <summary>
    /// Raised on the UI thread with the reply text when the "other side" answers.
    /// </summary>
    public event EventHandler<string> ReplyReceived;

    /// <summary>
    /// Raised on the UI thread when the "other side" starts/stops typing a reply.
    /// </summary>
    public event EventHandler<bool> Typing;

    private static readonly string[] Replies =
    {
        "Got it!",
        "Sounds good, let me check.",
        "Interesting, tell me more.",
        "Ok 👍",
        "Can we discuss this tomorrow?",
        "Nice. Did you see the latest DrawnUI release?",
        "Hmm, not sure about that.",
    };

    private static readonly string[] RepliesToImage =
    {
        "Nice shot!",
        "Wow, where is that?",
        "Love it, send more!",
    };

    private static readonly string[] RepliesToFile =
    {
        "Got the file, thanks!",
        "Downloading it now…",
        "Opens fine on my side.",
    };

    public void Send(ChatMessage msg)
    {
        Task.Run(async () =>
        {
            // fake network: server accepted
            await Task.Delay(_rnd.Next(300, 800));
            MainThread.BeginInvokeOnMainThread(() => msg.Sent = true);

            // recipient device received
            await Task.Delay(_rnd.Next(400, 1200));
            MainThread.BeginInvokeOnMainThread(() => msg.Delivered = true);

            // recipient typing…
            MainThread.BeginInvokeOnMainThread(() => Typing?.Invoke(this, true));
            await Task.Delay(_rnd.Next(1200, 3000));
            var pool = msg.Type switch
            {
                ChatMessageType.Image => RepliesToImage,
                ChatMessageType.File => RepliesToFile,
                _ => Replies,
            };
            var reply = pool[_rnd.Next(pool.Length)];
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Typing?.Invoke(this, false);
                ReplyReceived?.Invoke(this, reply);
            });

            // they obviously read it — checks turn blue right after their reply lands
            await Task.Delay(_rnd.Next(300, 700));
            MainThread.BeginInvokeOnMainThread(() => msg.Read = true);
        });
    }
}
