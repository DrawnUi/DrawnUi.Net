using Microsoft.Maui.Controls.Compatibility.Platform.iOS;

namespace DrawnUi.Draw;

public static class IOSExtensions
{
    public static IImageSourceHandler GetHandler(this ImageSource source)
    {
        //Image source handler to return
        IImageSourceHandler returnValue = null;
        //check the specific source type and return the correct image source handler
        if (source is UriImageSource)
        {
            returnValue = new ImageLoaderSourceHandler();
        }
        else if (source is FileImageSource)
        {
            returnValue = new FileImageSourceHandler();
        }
        else if (source is StreamImageSource)
        {
            returnValue = new StreamImagesourceHandler();
        }
        else if (source is FontImageSource)
        {
            returnValue = new FontImageSourceHandler();
        }
        return returnValue;
    }
}