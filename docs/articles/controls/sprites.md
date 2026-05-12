# Sprite Controls

DrawnUi provides specialized controls for rendering sprite-based animations. This article covers the sprite animation components available in the framework.

## SkiaSprite

SkiaSprite is a high-performance control for displaying and animating sprite sheets. It loads sprite sheets (a single image containing multiple animation frames arranged in a grid) and renders individual frames with precise timing for smooth animations.

### Basic Usage

```xml
<draw:SkiaSprite
    Source="sprites/explosion.png"
    Columns="8"
    Rows="4"
    FramesPerSecond="24"
    AutoPlay="True"
    Repeat="-1"
    WidthRequest="128"
    HeightRequest="128" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Source` | string | Path or URL of the sprite sheet image |
| `Columns` | int | Number of columns in the sprite sheet grid |
| `Rows` | int | Number of rows in the sprite sheet grid |
| `FramesPerSecond` | int | Animation speed in frames per second (default: 24) |
| `MaxFrames` | int | Maximum number of frames to use (0 means use all) |
| `CurrentFrame` | int | Current frame being displayed (0-based index) |
| `FrameSequence` | int[] | Custom sequence of frames to play |
| `AnimationName` | string | Name of a predefined animation sequence |
| `AutoPlay` | bool | Whether animation starts automatically when loaded |
| `Repeat` | int | Number of times to repeat (-1 for infinite) |
| `SpeedRatio` | double | Adjusts animation speed (1.0 is normal speed) |
| `DefaultFrame` | int | Frame to display when not playing |

### Animation Control

Control playback programmatically:

```csharp
// Start animation
mySprite.Start();

// Stop animation
mySprite.Stop();

// Jump to a specific frame
mySprite.CurrentFrame = 5;

// Seek to a time position
mySprite.Seek(timeInMs);
```

### Animation Events

```csharp
// Animation started event
mySprite.Started += (sender, e) => {
    // Animation has started
};

// Animation completed event (fires after all repeats)
mySprite.Finished += (sender, e) => {
    // Animation has finished
};
```

### Sprite Sheet Structure

A sprite sheet is a single image containing multiple frames arranged in a grid:

```
+---+---+---+---+
| 0 | 1 | 2 | 3 |
+---+---+---+---+
| 4 | 5 | 6 | 7 |
+---+---+---+---+
| 8 | 9 | 10| 11|
+---+---+---+---+
```

The `Columns` and `Rows` properties define the grid structure:
- In the example above, set `Columns="4"` and `Rows="3"`
- Frames are numbered left-to-right, top-to-bottom (0 to 11)
- Each frame must have the same dimensions

### Loading Sprite Sheets

SkiaSprite supports loading sprite sheets from various sources:

```xml
<!-- From app resources -->
<draw:SkiaSprite Source="running_character.png" />

<!-- From local file -->
<draw:SkiaSprite Source="file:///path/to/animation.png" />

<!-- From URL -->
<draw:SkiaSprite Source="https://example.com/sprites/animation.png" />
```

### Creating a Character Animation

```xml
<draw:SkiaLayout
    WidthRequest="200"
    HeightRequest="200"
    BackgroundColor="#F0F0F0">
    
    <draw:SkiaSprite
        x:Name="PlayerAnimation"
        Source="character_run.png"
        Columns="8"
        Rows="1"
        FramesPerSecond="12"
        AutoPlay="False"
        WidthRequest="128"
        HeightRequest="128"
        HorizontalOptions="Center"
        VerticalOptions="Center" />
        
    <draw:SkiaButton
        Text="Run"
        WidthRequest="80"
        HeightRequest="40"
        Margin="0,140,0,0"
        HorizontalOptions="Center"
        Tapped="OnRunButtonTapped" />
        
</draw:SkiaLayout>
```

In code-behind:
```csharp
private void OnRunButtonTapped(object sender, EventArgs e)
{
    if (PlayerAnimation.IsPlaying)
    {
        PlayerAnimation.Stop();
    }
    else
    {
        PlayerAnimation.Start();
    }
}
```

### Animation States

Use the `DefaultFrame` property to control which frame is shown when the animation is not playing:

```xml
<!-- Show first frame when not playing -->
<draw:SkiaSprite
    Source="button_press.png"
    Columns="10"
    Rows="1"
    DefaultFrame="0" />

<!-- Show last frame when not playing (useful for transitions that should remain in end state) -->
<draw:SkiaSprite
    Source="door_open.png"
    Columns="8"
    Rows="1"
    DefaultFrame="7" />
```

### Advanced: Frame Sequences and Reusing Spritesheets

One of the key features of SkiaSprite is the ability to create multiple animations from a single spritesheet by defining frame sequences:

#### Using Frame Sequences Directly

```xml
<!-- Manual frame sequence in XAML using array converter -->
<draw:SkiaSprite
    Source="character.png"
    Columns="8"
    Rows="2"
    FrameSequence="{Binding FrameSequence, Converter={StaticResource IntArrayConverter}}"
    FramesPerSecond="12" />
```

In code-behind:
```csharp
// Define a specific frame sequence
mySprite.FrameSequence = new[] { 3, 4, 5, 4, 3 }; // Play frames in this exact order
```

#### Creating Reusable Named Animations

Register animations once at application startup:

```csharp
// In your App.xaml.cs or similar initialization code
protected override void OnStart()
{
    base.OnStart();
    
    // Register named animations for a character spritesheet
    SkiaSprite.CreateAnimationSequence("Idle", new[] { 0, 1, 2, 1 });
    SkiaSprite.CreateAnimationSequence("Walk", new[] { 3, 4, 5, 6, 7, 8 });
    SkiaSprite.CreateAnimationSequence("Jump", new[] { 9, 10, 11 });
    SkiaSprite.CreateAnimationSequence("Attack", new[] { 12, 13, 14, 15 });
}
```

Then in XAML just reference by name:
```xml
<!-- Multiple sprites sharing the same spritesheet with different animations -->
<draw:SkiaSprite Source="character.png" AnimationName="Walk" />
<draw:SkiaSprite Source="character.png" AnimationName="Attack" />
```

Or switch animations in code:
```csharp
// Change the animation based on character state
void UpdateCharacterState(PlayerState state)
{
    switch (state)
    {
        case PlayerState.Idle:
            characterSprite.AnimationName = "Idle";
            break;
        case PlayerState.Walking:
            characterSprite.AnimationName = "Walk";
            break;
        case PlayerState.Jumping:
            characterSprite.AnimationName = "Jump";
            break;
        case PlayerState.Attacking:
            characterSprite.AnimationName = "Attack";
            break;
    }
}
```

### Memory Management and Caching

SkiaSprite includes an intelligent caching system to avoid reloading the same spritesheets multiple times:

```csharp
// Clear the entire spritesheet cache
SkiaSprite.ClearCache();

// Remove a specific spritesheet from cache
SkiaSprite.RemoveFromCache("character.png");
```

The control automatically handles:
- Caching spritesheets in memory when first loaded
- Sharing the same bitmap instance between multiple SkiaSprite controls
- Safe disposal when controls are no longer used

### Advanced: Custom Animation Speed

```csharp
mySprite.SpeedRatio = 0.5; // half speed
mySprite.SpeedRatio = 2.0; // double speed
```

## SkiaSpriteSet

`SkiaSpriteSet` is a stateful wrapper around multiple pre-created `SkiaSprite` instances. It is useful for characters or enemies that switch between states such as idle, run, jump, attack, appear, vanish, and so on.

The usual pattern is:

1. Define one sprite per integer state.
2. Switch the active state by changing `State`.
3. Wrap the integer state in your own semantic enum inside a subclass.

```csharp
public class PlayerSprite : SkiaSpriteSet
{
    public enum PlayerAnimState
    {
        Idle,
        Run,
        Jump,
        Attack,
    }

    public new PlayerAnimState State
    {
        get => _state;
        set
        {
            _state = value;
            base.State = value switch
            {
                PlayerAnimState.Idle => 0,
                PlayerAnimState.Run => 1,
                PlayerAnimState.Jump => 2,
                _ => 3,
            };
        }
    }

    private PlayerAnimState _state;

    public PlayerSprite()
    {
        Define(0, "hero-idle.png", columns: 4, rows: 1, fps: 8);
        Define(1, "hero-run.png", columns: 7, rows: 1, fps: 12);
        Define(2, "hero-jump.png", columns: 4, rows: 1, fps: 10);
        Define(3, "hero-attack.png", columns: 5, rows: 1, fps: 18);
    }
}
```

Use a `SkiaSpriteSet` when you want a stable actor/control in the scene while the active sprite sheet changes underneath it.

## Dynamic Frame Sizing

`SkiaSprite` and `SkiaSpriteSet` support frames whose visible content changes from frame to frame.

This works by separating two concepts:

1. Logical frame size.
This is the full frame cell size from the sprite sheet grid.

2. Visible content size.
This is the actual opaque content inside the frame after transparent padding is trimmed.

At draw time, DrawnUi keeps the logical frame as the positioning and anchoring reference, but renders only the trimmed visible content. This gives you three important properties:

- Different transparent padding no longer bloats the rendered sprite.
- Grounding stays stable because anchoring still uses the logical frame, not the cropped visible pixels.
- Dynamic silhouettes such as wide attacks or compact idle frames render correctly without forcing every frame to have the same visible bounds.

In other words, DrawnUi can handle changing visible frame bounds correctly, but it does not automatically normalize badly authored art that is fundamentally inconsistent in scale from one sheet to another.

## Sprite Placement Config

`SkiaSpriteSet.Define(...)` accepts an optional `placement` argument of type `SpritePlacementConfig`.

Most users do not need this for every sprite.

Use it when different state sheets must share a stable scale, pivot, or offset in the scene. If your idle, run, jump, and attack sheets already line up correctly, you can skip it.

Important placement settings include:

- `UnitsPerPixel`: shared logical scale for a sprite state.
- `AnchorX` and `AnchorY`: pivot used to place the sprite inside its logical actor slot.
- `OffsetXUnits` and `OffsetYUnits`: optional state-specific offsets.

Typical game usage is bottom-center anchoring with a shared `UnitsPerPixel` value across idle, run, jump, and attack sheets.

```csharp
Define(0, "hero-idle.png", columns: 4, rows: 1, fps: 8,
    placement: new SpritePlacementConfig
    {
        UnitsPerPixel = 3f,
        AnchorX = 0.5f,
        AnchorY = 1f,
    });
```

After you provide it once during sprite-state setup, the engine uses it internally. Your gameplay code usually should not touch it again.

## Hit Testing for Games

For sprites, there are usually two different hit-testing needs:

1. Visual hit box.
This is the tight box around the currently visible sprite content.

2. Gameplay hit box.
This is the box your game uses for damage, hurt, reach, pickups, or interactions.

DrawnUi provides a useful reusable base for the first part:

- `SkiaSprite` computes a trimmed display rect for the active frame.
- `SkiaSpriteSet.HitBoxAuto` returns that active frame rect in world space rather than the looser control slot.

That means `HitBoxAuto` is a good default for visual body intersection.

For gameplay, it is usually better to derive a box from `HitBoxAuto` instead of using it raw:

```csharp
var body = enemySprite.HitBoxAuto;

var hurtBox = new SKRect(
    body.Left + body.Width * 0.10f,
    body.Top + body.Height * 0.12f,
    body.Right - body.Width * 0.10f,
    body.Bottom - body.Height * 0.08f);
```

For a melee attack, project a custom box from the facing side of the current body box:

```csharp
var body = playerSprite.HitBoxAuto;
var attack = new SKRect(
    body.Right - 16,
    body.Top + body.Height * 0.2f,
    body.Right + 80,
    body.Bottom - body.Height * 0.2f);
```

This split is recommended for reusable game code:

- Use `HitBoxAuto` as the truthful visual frame body box.
- Derive scene-specific hurt boxes and attack boxes from it.
- Keep combat tuning in the game layer, not inside the sprite renderer.

If you need fixed gameplay consistency independent of animation silhouette, use authored gameplay boxes instead of relying entirely on the visual bounds.

Adjust animation speed using `SpeedRatio`:

```xml
<!-- Half speed -->
<draw:SkiaSprite
    Source="walking.png"
    Columns="8"
    Rows="1"
    SpeedRatio="0.5" />

<!-- Double speed -->
<draw:SkiaSprite
    Source="running.png"
    Columns="8"
    Rows="1"
    SpeedRatio="2.0" />
```

### Example: Button with Animated States

```xml
<draw:SkiaShape
    Type="Rectangle"
    CornerRadius="8"
    BackgroundColor="#3498DB"
    WidthRequest="200"
    HeightRequest="60">
    
    <draw:SkiaHotspot Tapped="OnButtonTapped">
        <draw:SkiaLayout
            HorizontalOptions="Fill"
            VerticalOptions="Fill">
            
            <!-- Button text -->
            <draw:SkiaLabel
                Text="Click Me"
                TextColor="White"
                FontSize="18"
                HorizontalOptions="Center"
                VerticalOptions="Center" />
                
            <!-- Button animation that plays on tap -->
            <draw:SkiaSprite
                x:Name="ButtonAnimation"
                Source="button_press.png"
                Columns="5"
                Rows="1"
                FramesPerSecond="30"
                AutoPlay="False"
                Repeat="0"
                HorizontalOptions="Fill"
                VerticalOptions="Fill"
                Opacity="0.5" />
                
        </draw:SkiaLayout>
    </draw:SkiaHotspot>
    
</draw:SkiaShape>
```

In code-behind:
```csharp
private void OnButtonTapped(object sender, EventArgs e)
{
    ButtonAnimation.Stop();
    ButtonAnimation.CurrentFrame = 0;
    ButtonAnimation.Start();
}
```

## Performance Considerations

### Memory Management

- Sprite sheets are cached automatically to avoid redundant loading
- For large or numerous sprite sheets, consider monitoring memory usage
- Use `ClearCache()` or `RemoveFromCache()` when spritesheets are no longer needed

### Optimization Tips

1. **Sprite Sheet Size**
   - Keep sprite sheets as small as possible while maintaining required quality
   - Consider using sprite packing algorithms to maximize space efficiency
   - Use power-of-two dimensions for better GPU compatibility

2. **Frame Rate**
   - Choose an appropriate `FramesPerSecond` value for your animation
   - For simple character animations, 12-15 FPS is often sufficient
   - For smoother animations, 24-30 FPS provides better results
   - Higher frame rates consume more resources

3. **Frame Sequences**
   - For complex animations, use frame sequences to avoid redundant frames
   - Share spritesheets between multiple sprites using the built-in caching

4. **Image Format**
   - Use PNG for sprite sheets with transparency
   - Consider WebP for better compression if supported
   - Optimize image file size using appropriate compression tools

### Implementation Notes

The `SkiaSprite` control derives from `AnimatedFramesRenderer`, which provides the base functionality for frame-based animation. The control internally:

1. Loads a spritesheet image into an `SKBitmap`
2. Calculates frame dimensions based on `Columns` and `Rows`
3. Extracts individual frames on demand by creating a new bitmap for each frame
4. Uses a `SkiaImage` control to display the current frame
5. Manages animation timing through the inherited animator functionality

This architecture aligns with other animation controls in DrawnUi like `SkiaGif` and `SkiaLottie`.