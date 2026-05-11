namespace DrawnUi.Blazor.Server;

public sealed record DrawnUiServerFrame(string ContentType, byte[] Payload)
{
    public string ToDataUrl()
    {
        return $"data:{ContentType};base64,{Convert.ToBase64String(Payload)}";
    }
}