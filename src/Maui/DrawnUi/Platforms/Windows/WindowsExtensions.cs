using System.Buffers;
using DrawnUi.Draw;
using Microsoft.Maui.Controls.Compatibility.Platform.UWP;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace DrawnUi.Draw;

public static class WindowsExtensions
{

    //public static IntPtr GetPixels(this WriteableBitmap bitmap) =>
    //    bitmap.PixelBuffer.GetByteBuffer();

    //public static IntPtr GetByteBuffer(this IBuffer buffer) =>
    //    (IntPtr)BufferExtensions.GetByteBuffer(buffer);

    public static Microsoft.UI.Xaml.Window GetPlatformWindow(this IMauiContext mauiContext) =>
        mauiContext.Services.GetRequiredService<Microsoft.UI.Xaml.Window>();

    public static IServiceProvider GetServiceProvider(this IElementHandler handler)
	{
		var context = handler.MauiContext ??
					  throw new InvalidOperationException($"Unable to find the context. The {nameof(ElementHandler.MauiContext)} property should have been set by the host.");

		var services = context?.Services ??
					   throw new InvalidOperationException($"Unable to find the service provider. The {nameof(ElementHandler.MauiContext)} property should have been set by the host.");

		return services;
	}

	public static T GetRequiredService<T>(this IElementHandler handler)
		where T : notnull
	{
		var services = handler.GetServiceProvider();

		var service = services.GetRequiredService<T>();

		return service;
	}



	public static IImageSourceHandler GetHandler(this ImageSource source)
	{
		//Image source handler to return
		IImageSourceHandler returnValue = null;
		if (source is FileImageSource)
		{
			returnValue = new FileImageSourceHandler();
		}
		else if (source is FontImageSource)
		{
			returnValue = new FontImageSourceHandler();
		}
		return returnValue;
	}

}
