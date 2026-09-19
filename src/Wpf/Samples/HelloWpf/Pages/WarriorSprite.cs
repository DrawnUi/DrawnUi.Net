using DrawnUi.Controls;
using DrawnUi.Draw;

namespace HelloWpf.Pages;

/// <summary>Warrior animation states: idle / walk / attack, facing right or left.</summary>
public enum WarriorAnimState
{
    IdleRight,
    IdleLeft,
    WalkRight,
    WalkLeft,
    WarRight,
    WarLeft,
}

/// <summary>
/// Port of the FastRepro WarriorSprite: maps warrior states to spritesheets and mirroring.
/// Subclassing SkiaSpriteSet keeps Source swaps atomic per state; geometry (Columns/Rows) and
/// mirroring live here.
/// </summary>
public class WarriorSprite : SkiaSpriteSet
{
    private WarriorAnimState _wstate = WarriorAnimState.IdleRight;

    /// <summary>Defines the three sheets for the given warrior colour.</summary>
    public WarriorSprite(string color = "Blue")
    {
        Define(0, $"anims/{color}Warrior/Warrior_Idle.png", 8, 1, 15);
        Define(1, $"anims/{color}Warrior/Warrior_Run.png", 6, 1, 15);
        Define(2, $"anims/{color}Warrior/Warrior_Attack1.png", 4, 1, 8);
        WState = WarriorAnimState.IdleRight;
    }

    /// <summary>Named state; maps to the base <see cref="SkiaSpriteSet.State"/> (0 idle, 1 walk, 2 war) and mirrors.</summary>
    public WarriorAnimState WState
    {
        get => _wstate;
        set
        {
            if (_wstate == value)
                return;

            _wstate = value;
            State = value is WarriorAnimState.IdleLeft or WarriorAnimState.IdleRight ? 0
                : value is WarriorAnimState.WalkLeft or WarriorAnimState.WalkRight ? 1
                : 2;
            ApplyMirror();
        }
    }

    private void ApplyMirror()
    {
        var sprite = CurrentSprite;
        if (sprite == null)
            return;

        var mirror = _wstate is WarriorAnimState.IdleLeft or WarriorAnimState.WalkLeft or WarriorAnimState.WarLeft;
        sprite.ScaleX = mirror ? -1 : 1;
    }

    /// <inheritdoc/>
    protected override void OnChangeState(int oldState, int newState)
    {
        base.OnChangeState(oldState, newState);
        ApplyMirror();
    }
}
