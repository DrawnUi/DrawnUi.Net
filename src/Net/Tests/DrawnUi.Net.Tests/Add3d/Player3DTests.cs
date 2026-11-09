using System.Numerics;
using DrawnUi.Add3d;
using Xunit;

namespace DrawnUi.Net.Tests.Add3d;

/// <summary>The animation player of the 3D add-on: cross-fades, once and back, holds, layers.</summary>
public class Player3DTests
{
    static readonly Scene3D Dragon = Scene3D.Shared(Add3dAssets.Dragon, out _);

    static NodeTransform[] Locals(Player3D player)
    {
        var local = new NodeTransform[Dragon.Nodes.Length];
        player.Locals(Dragon, local);
        return local;
    }

    static NodeTransform[] Posed(string animation, float time, bool looped = true)
    {
        var local = new NodeTransform[Dragon.Nodes.Length];
        Dragon.Rest(local);
        Dragon.Apply(new Track(Dragon.AnimationNamed(animation), time, looped), local);
        return local;
    }

    static float Distance(NodeTransform[] a, NodeTransform[] b) =>
        a.Zip(b).Sum(p => Vector3.Distance(p.First.T, p.Second.T) + (1 - MathF.Abs(Quaternion.Dot(p.First.R, p.Second.R))));

    [Fact]
    public void A_new_move_fades_in_from_the_old_one()
    {
        var player = new Player3D(Dragon) { Fade = 0.25f };
        player.Play(Dragon.AnimationNamed(Add3dAssets.Idle));
        player.FadeNow();
        player.Advance(Dragon, 0.5f);
        player.Play(Dragon.AnimationNamed(Add3dAssets.Punch));
        player.Advance(Dragon, 0.1f);
        var mid = Locals(player);
        var idle = Posed(Add3dAssets.Idle, 0.6f);
        var punch = Posed(Add3dAssets.Punch, 0.1f);
        Assert.True(Distance(mid, idle) > 1e-3f && Distance(mid, punch) > 1e-3f, "blended, neither pose alone");
        player.Advance(Dragon, 0.2f);
        Assert.True(Distance(Locals(player), Posed(Add3dAssets.Punch, 0.3f)) < 1e-3f, "the fade is over");
    }

    [Fact]
    public void Once_goes_back_to_the_loop_and_reports_its_end_once()
    {
        var player = new Player3D(Dragon) { Fade = 0 };
        var idle = Dragon.AnimationNamed(Add3dAssets.Idle);
        var punch = Dragon.AnimationNamed(Add3dAssets.Punch);
        player.Play(idle);
        player.PlayOnce(punch);
        var ended = new List<int>();
        for (var i = 0; i < 300; i++)
        {
            var e = player.Advance(Dragon, 1 / 60f);
            if (e >= 0)
                ended.Add(e);
        }
        Assert.Equal(new[] { punch }, ended);
        Assert.Equal(idle, player.Main.Animation);
        Assert.True(player.Busy);
    }

    [Fact]
    public void A_hold_with_no_loop_before_stays_and_stops()
    {
        var player = new Player3D(Dragon);
        var death = Dragon.AnimationNamed(Add3dAssets.Death);
        player.Hold(death);
        for (var i = 0; i < 300; i++)
            player.Advance(Dragon, 1 / 60f);
        Assert.Equal(death, player.Main.Animation);
        Assert.Equal(Dragon.Animations[death].Duration, player.Main.Time);
        Assert.False(player.Busy);
    }

    [Fact]
    public void A_layer_plays_on_top_and_keeps_the_frames_going()
    {
        var player = new Player3D(Dragon) { Fade = 0 };
        var death = Dragon.AnimationNamed(Add3dAssets.Death);
        player.Hold(death);
        player.AddLayer(Dragon.AnimationNamed(Add3dAssets.Punch));
        for (var i = 0; i < 300; i++)
            player.Advance(Dragon, 1 / 60f);
        Assert.True(player.Busy, "the layer still moves");
        Assert.True(Distance(Locals(player), Posed(Add3dAssets.Death, 100, looped: false)) > 1e-3f, "the layer changes the pose");
        player.RemoveLayer(Dragon.AnimationNamed(Add3dAssets.Punch));
        Assert.Equal(death, player.Main.Animation);
    }
}
