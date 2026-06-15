using System.Text;

namespace DrawnUiRepro;

public enum ChatMessageType
{
    Text,
    Image,
    Link,
    File
}

public sealed class ChatMessage : BindableObject
{
    public int Index { get; set; }
    public bool Outgoing { get; set; }
    public ChatMessageType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;

    /// <summary>
    /// Tiny inline preview shown instantly in the cell while ImageUrl is still loading.
    /// </summary>
    public string PreviewBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Quoted message this one replies to (the original app's AttachedMessage).
    /// A real backend would store an id; the mock keeps the reference.
    /// </summary>
    public ChatMessage ReplyTo { get; set; }

    public string AuthorName => Outgoing ? "You" : "Bot";

    /// <summary>
    /// First message of a consecutive same-sender run (run resets on a new day):
    /// the bubble shows its deco-triangle tail, follow-ups keep a ghost tail for alignment.
    /// </summary>
    public bool IsFirstOfGroup { get; set; }

    /// <summary>
    /// First message of a new day: a centered date separator is shown above the bubble.
    /// </summary>
    public bool IsFirstDay { get; set; }

    public string DayDesc { get; set; } = string.Empty;

    // Delivery statuses (outgoing only). Notify so the bound cell can update
    // checkmarks live while the mock api advances the stages.

    public bool Sent
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool Delivered
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool Read
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// What a real backend would send along an image record: a tiny blurred jpeg
    /// shown instantly while the full image downloads.
    /// RAW base64 only — SkiaImage.SetFromBase64 feeds it straight to
    /// Convert.FromBase64String, a "data:image/...;base64," data-URI prefix would throw.
    /// </summary>
    public const string MockImagePreview =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAYCAIAAAAUMWhjAAAAAXNSR0IB2cksfwAAAARnQU1BAACxjwv8YQUAAAAgY0hSTQAAeiYAAICEAAD6AAAAgOgAAHUwAADqYAAAOpgAABdwnLpRPAAAAAlwSFlzAAALEwAACxMBAJqcGAAAAAd0SU1FB+oGCw0WOB8Ifv8AAAR6SURBVEjHRVXbbt1IEiNZLfmcGWeCuQS72If9/y/KLwTJroNkbMeW1EXug46z/SBA3V2NIousot7/I0mWsa7rUYukLAtJXS6SWmOMgXfvqmou67qu+P0DgB99VNX9uz/u7+9/e//ncRzP//308PBw//Rtzrl//tTdx/NzEgGQRLK7AQBwd5I5p20AtpN093Ec5zXbY4zjOPZ9n3Nu2/bly5dt25ZlAfD4+EjyjAVQGWuNChBbRcZcNIpFFuL2IjaMefhuCcxs5W309I9Hijlevz89bNvj+33i5Yc/fcrz8/b6iDT6cM/i9V4SpKqKCKBJSaCSaCwAdqCq5qiqMrHv+3G0JNdyvV4tVtX+9dvXhwc+Pc45qwCgyO4uLqsIBIwDMFG7nHhWPO0+NgwOZq1a435+XObhcb1b7sa//3UMXUuvry+//OfzfPym1yf6gA173/ckIzbHCGDbNsnE3Y2hLEuHqgIw59Scx3GEATD33esK+3q9vpO+f/++bVu93QSZRJLtUYZfd6skMQfJqJCA5QRrRq3H68aqtCV1wjFwd69FBf3zzw9/wE+fP+89vW8gUjKYhCYL49QJVEkS6G1ZkDTWNW+rj0NVkMYYfb3+9ddfX47j48ePf8AvLy8rOeespKqWsez7Lqq7xxuWVpXGQjIFCUgYZ98B0EGrLhcivP7yY84PAR6+6u+/fwXQ1rbFPUo0Emd7HommZbMuv9kOoCpdLiRdrKoA3e2qqmoKEq7XZVn67nq5XCbHnLOuVwC17fu+c3+xPTwBYB4AsE+SrF9/O7khyRLJqkXStCXtS63r2qOSrPfv932XlGRFSKZEEuacM7MBIG27AgCd2B4/DRySRFWd1nVyHAfGtbstruuaBECSdV33p0cAFiUxSgI7STyTIABgwPZgN4AoAKtDWzBJC5V02kZaAvenJyR1uWPPQby+vFwgklMkYILkQBzTSbLsJlmnV0HiTS0IkvhEIyRBjaoi1d0hultIgH7dAJhMYsRz3h5p25YBoCpEt5BCBApot91EGBO8o0BhtlR9TO8HA/R0N3u3p9zsmcNylgl1dHR1po7OLLFOBCQDJgkI4CQyZ6OVkkwn9lkGxEnoTuJuJ4CSKLB91qPZsFm1JCFWkk0DGMvVdqNh666qChEAKACAIcljSaLtGYBbZ/1JnrYdAoAdRjJuB0Heyn/sO0gUfg6D8ydvLV5SOzUGgOM4kHHToU0JQPdJPwGwuP7Mrji6GxSAxlLLUtiTGCLpSpLyjdHTgLbpA8AZdUIpC8DO3BDcBpkNNEmQZ0DPGUySGmfLapI/3WC7KQCFSJrdkm5TrIpkhCQkTplOkgkBkCehBeBkv1kA1kEA7TuS8Q8APA2VGmNQ0/bZLg8OkhoE8H8ESXiD6Z+5nHU7v3POc982ThwOALDmnOBBEuApwyTdlsRb3bCQbLSkmxZpJARJWkUSMABldDdKkpJOUidij9uQT6wz90iqW6anTuDgbHvE+fbpiQDJ246km2BxNp3Y9k3Kp5NikEV09/8AB/G6onUvA1sAAAAASUVORK5CYII=";

    private static readonly string[] Words =
    {
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed",
        "do", "eiusmod", "tempor", "incididunt", "labore", "magna", "aliqua", "enim", "minim",
        "veniam", "quis", "nostrud", "exercitation", "ullamco", "laboris", "aliquip", "commodo",
    };

    private const int MessagesPerDay = 10;
    private const int LastMockIndex = 999;
    private static int LastMockDay => LastMockIndex / MessagesPerDay;

    /// <summary>
    /// Same first random draw as CreateMock — lets neighbors be compared without materializing them
    /// (needed for group/day flags in a windowed source where the previous item may not be loaded).
    /// </summary>
    private static bool OutgoingFor(int index) => new Random(index).NextDouble() < 0.45;

    private static string DayDescFor(int index)
    {
        int day = index / MessagesPerDay;
        return day == LastMockDay
            ? "Today"
            : DateTime.Today.AddDays(day - LastMockDay).ToString("ddd, d MMM");
    }

    /// <summary>
    /// Deterministic per-Index mock so a message keeps the same content/height across
    /// rebinds/recycle (re-randomizing per bind would thrash measurement).
    /// </summary>
    public static ChatMessage CreateMock(int index)
    {
        var rnd = new Random(index);

        var type = ChatMessageType.Text;
        if (index % 11 == 5)
            type = ChatMessageType.Image;
        else if (index % 13 == 7)
            type = ChatMessageType.Link;
        else if (index % 17 == 3)
            type = ChatMessageType.File;

        bool outgoing = rnd.NextDouble() < 0.45;
        bool isFirstDay = index == 0 || index / MessagesPerDay != (index - 1) / MessagesPerDay;

        string text = type == ChatMessageType.File
            ? $"document_{index}.pdf ({rnd.Next(50, 4000)} KB)"
            : BuildText(rnd, index);

        return new ChatMessage
        {
            Index = index,
            Outgoing = outgoing,
            Type = type,
            Text = text,
            Time = $"{8 + index % 12}:{index * 7 % 60:00}",
            ImageUrl = type == ChatMessageType.Image ? $"https://picsum.photos/seed/chat{index}/400/240" : string.Empty,
            PreviewBase64 = type == ChatMessageType.Image ? MockImagePreview : string.Empty,
            LinkUrl = type == ChatMessageType.Link ? "https://drawnui.net" : string.Empty,
            IsFirstDay = isFirstDay,
            DayDesc = DayDescFor(index),
            // Tail rule: first of a consecutive same-sender run; a new day starts a new run.
            // Replays the previous message's direction draw instead of materializing it
            // (windowed source friendly).
            IsFirstOfGroup = index == 0 || isFirstDay || OutgoingFor(index - 1) != outgoing,
            // history is long read
            Sent = outgoing,
            Delivered = outgoing,
            Read = outgoing,
        };
    }

    private static string BuildText(Random rnd, int index)
    {
        var sb = new StringBuilder($"Message {index}.");
        int words = rnd.Next(2, 14);
        for (int w = 0; w < words; w++)
        {
            sb.Append(' ');
            sb.Append(Words[rnd.Next(Words.Length)]);
        }

        return sb.ToString();
    }
}
