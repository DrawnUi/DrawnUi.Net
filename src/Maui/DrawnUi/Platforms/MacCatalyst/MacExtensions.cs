using CoreGraphics;
using Foundation;
using Microsoft.Maui.Controls.Compatibility.Platform.iOS;
using Microsoft.Maui.Handlers;
using UIKit;

namespace DrawnUi.Draw;

public class KeysInputCatchetLayer : UIView
{
    public override UIView HitTest(CGPoint point, UIEvent uievent)
    {
        // Ignore all touch events, and let them pass through to underlying views
        return null;
    }

    public override void DidUpdateFocus(UIFocusUpdateContext context, UIFocusAnimationCoordinator coordinator)
    {
        base.DidUpdateFocus(context, coordinator);

        if (context.NextFocusedItem == null)
        {
            BecomeFirstResponder();
        }
    }

    public override bool CanBecomeFirstResponder => true;

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        bool consumed = false;
        foreach (UIPress press in presses)
        {
            var mapped = KeyboardManager.MapToMaui((int)press.Type);
            KeyboardManager.KeyboardPressed(mapped);
        }

        if (!consumed)
        {
            base.PressesBegan(presses, evt);
        }
    }
 
    public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        base.PressesEnded(presses, evt);

        bool consumed = false;

        foreach (UIPress press in presses)
        {
            var mapped = KeyboardManager.MapToMaui((int)press.Type);
            KeyboardManager.KeyboardReleased(mapped);

            //Trace.WriteLine($"[KEY] {press.Type}/{(int)press.Type} => {mapped}");
        }
    }
}


public class CustomizedWindowHandler : WindowHandler
{
    protected override UIWindow CreatePlatformElement()
    {
        var test = base.CreatePlatformElement();


        return test;
    }
}

public static class MacExtensions
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