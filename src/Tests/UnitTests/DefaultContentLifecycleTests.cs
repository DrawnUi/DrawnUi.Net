using DrawnUi.Controls;
using DrawnUi.Draw;
using Xunit;

namespace UnitTests
{
    /// <summary>
    /// Default content lifecycle: .Initialize() runs on first attach (not at first measure),
    /// and a ControlStyle change after the content exists rebuilds it instead of being ignored.
    /// </summary>
    public class DefaultContentLifecycleTests : DrawnTestsBase
    {
        [Fact]
        public void Initialize_RunsOnAttach_EvenWhenInvisible()
        {
            var ran = 0;

            var parent = new SkiaLayout
            {
                Children =
                {
                    new SkiaLabel { IsVisible = false }
                        .Initialize(me => ran++)
                }
            };

            Assert.Equal(1, ran);

            // an invisible child is never measured, the hook must not depend on it
            parent.CommitInvalidations();
            parent.Measure(200, 200, 1);

            Assert.Equal(1, ran);
        }

        [Fact]
        public void Initialize_RunsOnce_NotAgainOnRebuild()
        {
            var ran = 0;

            var parent = new SkiaLayout
            {
                Children =
                {
                    new SkiaSwitch { ControlStyle = PrebuiltControlStyle.Unset }
                        .Initialize(me => ran++)
                }
            };

            var toggle = (SkiaSwitch)parent.Views[0];
            parent.CommitInvalidations();
            parent.Measure(200, 200, 1);

            toggle.ControlStyle = PrebuiltControlStyle.Cupertino;
            parent.CommitInvalidations();
            parent.Measure(200, 200, 1);

            Assert.Equal(1, ran);
        }

        [Fact]
        public void Initialize_RunsAtMeasure_WhenNeverAttached()
        {
            var ran = 0;
            var label = new SkiaLabel().Initialize(me => ran++);

            Assert.Equal(0, ran);

            label.CommitInvalidations();
            label.Measure(200, 200, 1);

            Assert.Equal(1, ran);
        }

        [Fact]
        public void ControlStyle_ChangeAfterMeasure_RebuildsContentAndSize()
        {
            var toggle = new SkiaSwitch { ControlStyle = PrebuiltControlStyle.Unset };
            toggle.CommitInvalidations();
            toggle.Measure(200, 200, 1);

            var firstChildren = toggle.Views.ToList();
            Assert.True(firstChildren.Count > 0, "style content was not created");
            Assert.Equal(46, toggle.WidthRequest);
            Assert.Equal(28, toggle.HeightRequest);

            toggle.ControlStyle = PrebuiltControlStyle.Cupertino;
            toggle.CommitInvalidations();
            toggle.Measure(200, 200, 1);

            Assert.True(toggle.Views.Count > 0, "style content was not rebuilt");
            Assert.DoesNotContain(toggle.Views, v => firstChildren.Contains(v));
            Assert.Equal(51, toggle.WidthRequest);
            Assert.Equal(31, toggle.HeightRequest);
        }

        [Fact]
        public void ControlStyle_ChangeAfterMeasure_KeepsUserSize()
        {
            var toggle = new SkiaSwitch { ControlStyle = PrebuiltControlStyle.Unset, HeightRequest = 40 };
            toggle.CommitInvalidations();
            toggle.Measure(200, 200, 1);

            toggle.ControlStyle = PrebuiltControlStyle.Cupertino;
            toggle.CommitInvalidations();
            toggle.Measure(200, 200, 1);

            Assert.Equal(40, toggle.HeightRequest);
            Assert.Equal(51, toggle.WidthRequest);
        }

        [Fact]
        public void ControlStyle_ChangeAfterMeasure_KeepsUserChildren()
        {
            var userChild = new SkiaLabel { Text = "mine" };
            var button = new SkiaButton { ControlStyle = PrebuiltControlStyle.Unset, Children = { userChild } };
            button.CommitInvalidations();
            button.Measure(200, 200, 1);

            button.ControlStyle = PrebuiltControlStyle.Cupertino;
            button.CommitInvalidations();
            button.Measure(200, 200, 1);

            Assert.Contains(userChild, button.Views);
            Assert.False(userChild.IsDisposed);
        }

        [Fact]
        public void Slider_EnableRangeAfterMeasure_RebuildsThumbs()
        {
            var slider = new SkiaSlider { ControlStyle = PrebuiltControlStyle.Unset };
            slider.CommitInvalidations();
            slider.Measure(400, 100, 1);

            var firstEndThumb = slider.FindView<SliderThumb>("EndThumb");
            Assert.NotNull(firstEndThumb);
            Assert.Null(slider.FindView<SliderThumb>("StartThumb"));

            slider.EnableRange = true;
            slider.CommitInvalidations();
            slider.Measure(400, 100, 1);

            var endThumb = slider.FindView<SliderThumb>("EndThumb");
            Assert.NotNull(endThumb);
            Assert.NotNull(slider.FindView<SliderThumb>("StartThumb"));
            Assert.NotSame(firstEndThumb, endThumb);
            Assert.DoesNotContain(firstEndThumb, slider.Views);
        }
    }
}
