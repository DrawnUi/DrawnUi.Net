using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace ParallaxGameLoop.Game
{
    public sealed partial class ParallaxGame
    {
        /// <summary>
        /// Magic sparkle trail: glints left behind while the heroine moves, drawn in one atlas call.
        /// Particles live in world space, so they stay where they were born while the camera scrolls.
        /// </summary>
        private sealed class SparkleTrailControl : SkiaControl
        {
            private const int MaxParticles = 160;
            private const float EmitPerSecond = 90f;
            private const int AtlasCell = 64;

            private const string SpriteShader = """
                half4 main(float2 p) {
                    float2 u = (fract(p / 64.0) - 0.5) * 2.0;
                    float r2 = dot(u, u);
                    float edge = 1.0 - smoothstep(0.72, 0.98, sqrt(r2));
                    float a;
                    if (p.x < 64.0) {
                        float core = exp(-r2 * 26.0);
                        float glow = exp(-r2 * 6.0) * 0.32;
                        float rays = exp(-abs(u.y) * 34.0 - u.x * u.x * 3.2) + exp(-abs(u.x) * 34.0 - u.y * u.y * 3.2);
                        float2 d = float2(u.x + u.y, u.x - u.y) * 0.7071;
                        float diag = exp(-abs(d.y) * 40.0 - d.x * d.x * 9.0) + exp(-abs(d.x) * 40.0 - d.y * d.y * 9.0);
                        a = core + glow + rays * 0.85 + diag * 0.35;
                    } else {
                        a = exp(-r2 * 9.0);
                    }
                    a = clamp(a, 0.0, 1.0) * edge;
                    return half4(a);
                }
                """;

            private static readonly SKColor[] Palette =
            [
                new(255, 244, 200),
                new(255, 196, 80),
                new(255, 214, 110),
                new(255, 170, 60),
                new(190, 225, 255),
            ];

            private readonly float[] _x = new float[MaxParticles];
            private readonly float[] _y = new float[MaxParticles];
            private readonly float[] _vx = new float[MaxParticles];
            private readonly float[] _vy = new float[MaxParticles];
            private readonly float[] _age = new float[MaxParticles];
            private readonly float[] _life = new float[MaxParticles];
            private readonly float[] _size = new float[MaxParticles];
            private readonly float[] _phase = new float[MaxParticles];
            private readonly float[] _spin = new float[MaxParticles];
            private readonly byte[] _kind = new byte[MaxParticles];
            private readonly byte[] _tint = new byte[MaxParticles];

            private readonly SKRect[] _sprites = new SKRect[MaxParticles];
            private readonly SKRotationScaleMatrix[] _transforms = new SKRotationScaleMatrix[MaxParticles];
            private readonly SKColor[] _colors = new SKColor[MaxParticles];
            private readonly SKPaint _paint = new() { BlendMode = SKBlendMode.Plus, IsAntialias = true };
            private readonly SKSamplingOptions _sampling = new(SKFilterMode.Linear, SKMipmapMode.None);
            private readonly Random _random = new(7);

            private SKImage _atlas;
            private float _cameraX;
            private float _emitDebt;
            private int _alive;

            public SparkleTrailControl()
            {
                WidthRequest = -1;
                HeightRequest = SceneHeight;
                HorizontalOptions = LayoutOptions.Fill;
                VerticalOptions = LayoutOptions.Start;
                UseCache = SkiaCacheType.None;
            }

            /// <summary>
            /// Advances the trail. While <paramref name="emitting"/>, new glints are born inside the body box
            /// (scene units) and pushed behind the heroine; existing ones always finish their fade.
            /// </summary>
            public void Step(float dt, bool emitting, SKRect body, bool facingLeft, float cameraX)
            {
                _cameraX = cameraX;
                var behind = facingLeft ? 1f : -1f;

                if (emitting)
                {
                    _emitDebt += EmitPerSecond * dt;
                    while (_emitDebt >= 1f)
                    {
                        _emitDebt -= 1f;
                        Spawn(body, behind, cameraX);
                    }
                }
                else
                {
                    _emitDebt = 0f;
                }

                var alive = 0;
                var damping = MathF.Exp(-2.2f * dt);
                for (var i = 0; i < MaxParticles; i++)
                {
                    if (_life[i] <= 0f)
                    {
                        continue;
                    }

                    _age[i] += dt;
                    if (_age[i] >= _life[i])
                    {
                        _life[i] = 0f;
                        continue;
                    }

                    _vx[i] *= damping;
                    _vy[i] = _vy[i] * damping - 18f * dt;
                    _x[i] += _vx[i] * dt;
                    _y[i] += _vy[i] * dt;
                    alive++;
                }

                _alive = alive;
                if (alive > 0 || emitting)
                {
                    Update();
                }
            }

            private void Spawn(SKRect body, float behind, float cameraX)
            {
                var slot = -1;
                var oldest = 0f;
                for (var i = 0; i < MaxParticles; i++)
                {
                    if (_life[i] <= 0f)
                    {
                        slot = i;
                        break;
                    }

                    var progress = _age[i] / _life[i];
                    if (progress > oldest)
                    {
                        oldest = progress;
                        slot = i;
                    }
                }

                var r = _random;
                var isStar = r.NextSingle() < 0.6f;
                _x[slot] = cameraX + body.MidX + behind * body.Width * (0.1f + 0.35f * r.NextSingle());
                _y[slot] = body.Top + body.Height * (0.15f + 0.8f * r.NextSingle());
                _vx[slot] = behind * (20f + 50f * r.NextSingle()) + (r.NextSingle() - 0.5f) * 30f;
                _vy[slot] = (r.NextSingle() - 0.65f) * 50f;
                _age[slot] = 0f;
                _life[slot] = 0.55f + 0.6f * r.NextSingle();
                _size[slot] = isStar ? 30f + 26f * r.NextSingle() : 9f + 9f * r.NextSingle();
                _phase[slot] = r.NextSingle() * MathF.Tau;
                _spin[slot] = (r.NextSingle() - 0.5f) * 0.5f;
                _kind[slot] = isStar ? (byte)0 : (byte)1;
                _tint[slot] = (byte)r.Next(Palette.Length);
            }

            public override void OnDisposing()
            {
                _atlas?.Dispose();
                _atlas = null;
                _paint.Dispose();
                base.OnDisposing();
            }

            protected override void Paint(DrawingContext ctx)
            {
                base.Paint(ctx);

                if (_alive == 0)
                {
                    return;
                }

                EnsureAtlas();
                if (_atlas == null)
                {
                    return;
                }

                var dest = ctx.Destination;
                var scale = ctx.Scale;
                var half = AtlasCell * 0.5f;

                for (var i = 0; i < MaxParticles; i++)
                {
                    if (_life[i] <= 0f)
                    {
                        _colors[i] = SKColors.Transparent;
                        _transforms[i] = new SKRotationScaleMatrix(0, 0, -10000, -10000);
                        _sprites[i] = new SKRect(0, 0, 1, 1);
                        continue;
                    }

                    var t = _age[i] / _life[i];
                    var fadeIn = MathF.Min(1f, t * 8f);
                    var fadeOut = 1f - t * t;
                    var twinkle = 0.55f + 0.45f * MathF.Sin(_phase[i] + _age[i] * 24f);
                    var alpha = fadeIn * fadeOut * twinkle;
                    var size = _size[i] * (1f - 0.65f * t) * scale;
                    var s = size / AtlasCell;
                    var angle = _spin[i] + _age[i] * _spin[i] * 2f;

                    var px = dest.Left + (_x[i] - _cameraX) * scale;
                    var py = dest.Top + _y[i] * scale;
                    var cos = MathF.Cos(angle) * s;
                    var sin = MathF.Sin(angle) * s;
                    _transforms[i] = new SKRotationScaleMatrix(cos, sin, px - cos * half + sin * half, py - sin * half - cos * half);

                    var cell = _kind[i] * AtlasCell;
                    _sprites[i] = new SKRect(cell, 0, cell + AtlasCell, AtlasCell);
                    _colors[i] = Palette[_tint[i]].WithAlpha((byte)(alpha * 255f));
                }

                ctx.Context.Canvas.DrawAtlas(_atlas, _sprites, _transforms, _colors, SKBlendMode.Modulate, _sampling, _paint);
            }

            private void EnsureAtlas()
            {
                if (_atlas != null || Superview is not DrawnView drawnView)
                {
                    return;
                }

                using var effect = SKRuntimeEffect.CreateShader(SpriteShader, out var errors);
                if (effect == null)
                {
                    Console.WriteLine($"[ParallaxGame] sparkle sprite: {errors}");
                    return;
                }

                using var surface = drawnView.CreateSurface(AtlasCell * 2, AtlasCell, true);
                if (surface == null)
                {
                    return;
                }

                using var shader = effect.ToShader();
                using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Src };
                surface.Canvas.Clear(SKColors.Transparent);
                surface.Canvas.DrawRect(0, 0, AtlasCell * 2, AtlasCell, paint);
                surface.Canvas.Flush();
                _atlas = surface.Snapshot();
                drawnView.ReturnSurface(surface);
            }
        }
    }
}
