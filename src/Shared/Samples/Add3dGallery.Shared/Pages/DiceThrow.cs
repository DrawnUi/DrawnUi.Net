using System.Numerics;

namespace Add3dGallery;

/// <summary>
/// A throw of two dice in physics, stepped along as it is shown: rigid cubes with rounded corners (edge 1, mass 1) thrown
/// across the felt, falling under gravity, bouncing and sliding on their corners with restitution and Coulomb friction,
/// kept on the felt by its edges and bumping into each other, until both lie still. The face each die ends on is the
/// result, as with real dice.
/// </summary>
public sealed class DiceThrow
{
    public struct Body
    {
        public Vector3 Position;
        public Quaternion Turn;
        public Vector3 Velocity;
        public Vector3 Spin;
        /// <summary>Time it has lain on the felt barely moving; past a moment it rests and moves no more.</summary>
        public float Quiet;
        /// <summary>Time it has stood still on an edge or a corner: past a moment it tips over, as a real die does.</summary>
        public float Propped;
        public bool Resting;
    }

    public const float Step = 1 / 360f;
    /// <summary>
    /// Gravity in die edges per second squared: as for a die of about 20 cm, which a die on this felt reads as, so it
    /// falls and rolls at the pace the eye expects.
    /// </summary>
    const float Gravity = 50;
    public const float Table = -0.5f;
    /// <summary>
    /// How far the dice centers may go (die edges from the middle), so they stay in view on narrow screens too. The felt
    /// shows no rail there, so a die reaching it only loses its way out, and never leans on it.
    /// </summary>
    public const float EdgeX = 1.15f, EdgeZ = 0.5f;
    const float Restitution = 0.5f, Friction = 0.3f, ThrowUp = 3.6f, ThrowAcross = 3, SpinMin = 16;
    const float InverseInertia = 6; // cube, mass 1, edge 1: inertia 1/6
    const float Bump = 1.25f; // dice centers keep this apart: faces never sink into each other

    readonly float _radius, _core;
    readonly Vector3[] _corners = new Vector3[8];
    readonly Body[] _dice = new Body[2];
    float _time, _owed;

    /// <summary>Both dice lie still; <see cref="Faces"/> holds the result.</summary>
    public bool Done { get; private set; } = true;
    /// <summary>Seconds of the throw so far.</summary>
    public float Time => _time;
    public int[] Faces { get; } = new int[2];
    public Vector3 Position(int die) => _dice[die].Position;
    public Quaternion Turn(int die) => _dice[die].Turn;

    /// <param name="rounding">The dice's corner radius in die edges.</param>
    public DiceThrow(float rounding)
    {
        _radius = rounding;
        _core = 0.5f - rounding;
        for (var i = 0; i < 8; i++)
            _corners[i] = new Vector3((i & 1) == 0 ? -_core : _core, (i & 2) == 0 ? -_core : _core, (i & 4) == 0 ? -_core : _core);
    }

    /// <summary>
    /// Throws both dice from where they lie: up, across the felt (<paramref name="direction"/>: -1 left, 1 right, 0
    /// either) and spinning around a random axis. <see cref="Run"/> then moves them on.
    /// </summary>
    public void Throw(Vector3 at0, Quaternion turn0, Vector3 at1, Quaternion turn1, int direction, Random random)
    {
        if (direction == 0)
            direction = random.Next(2) * 2 - 1;
        // the die ahead goes faster than the one behind it, so they rarely run into each other and end apart
        var firstAhead = (at0.X - at1.X) * direction > 0;
        _dice[0] = Thrown(at0, turn0, -0.85f, direction, firstAhead ? 1 : 0.55f, random);
        _dice[1] = Thrown(at1, turn1, 0.85f, direction, firstAhead ? 0.55f : 1, random);
        _time = _owed = 0;
        Done = false;
    }

    /// <summary>
    /// Moves the throw on by <paramref name="seconds"/> of real time, in fixed physics steps; when both dice lie still
    /// (or after 4 s) each is laid exactly flat and <see cref="Done"/> turns true.
    /// </summary>
    public void Run(float seconds)
    {
        if (Done)
            return;
        _owed = MathF.Min(_owed + seconds, 0.1f); // a long frame never runs away with the physics
        while (_owed >= Step && !Done)
        {
            _owed -= Step;
            _time += Step;
            Advance(_dice);
            if (_dice[0].Resting && _dice[1].Resting || _time >= 4)
            {
                for (var i = 0; i < 2; i++)
                    Faces[i] = Settle(ref _dice[i]);
                Done = true;
            }
        }
    }

    static Body Thrown(Vector3 at, Quaternion turn, float home, int direction, float pace, Random random)
    {
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        // both the same way, as from one hand, each toward a spot of its own half of the felt (so they end apart and
        // never live at one edge over many throws), at its own pace
        var target = new Vector2(home + direction * Range(0f, 0.3f), Range(-0.35f, 0.35f));
        var across = direction * pace * Range(ThrowAcross * 0.7f, ThrowAcross) + (target.X - at.X) * 2.2f;
        var axis = Vector3.Normalize(new Vector3(Range(-1, 1), Range(-0.4f, 0.4f), Range(-1, 1)));
        return new Body
        {
            Position = at,
            Turn = turn,
            Velocity = new Vector3(across, Range(ThrowUp, ThrowUp * 1.3f), (target.Y - at.Z) * 2.2f),
            Spin = axis * Range(SpinMin, SpinMin * 1.5f),
        };
    }

    void Advance(Body[] dice)
    {
        Span<int> touching = stackalloc int[2];
        Span<Vector3> corners = stackalloc Vector3[16];
        for (var i = 0; i < 2; i++)
        {
            ref var d = ref dice[i];
            d.Velocity.Y -= Gravity * Step;
            // the corners do not move while the contacts are solved: turned once per step
            var turn = Matrix4x4.CreateFromQuaternion(d.Turn);
            for (var c = 0; c < 8; c++)
                corners[i * 8 + c] = d.Position + Vector3.TransformNormal(_corners[c], turn);
            foreach (var c in corners.Slice(i * 8, 8))
                if (c.Y - _radius - Table < 0.02f)
                    touching[i]++;
        }

        // a few passes over the corner contacts, so a die landing on an edge or a face shares the impulse
        for (var pass = 0; pass < 3; pass++)
        {
            for (var i = 0; i < 2; i++)
            {
                ref var d = ref dice[i];
                if (d.Resting)
                    continue;
                foreach (var corner in corners.Slice(i * 8, 8))
                {
                    var depth = Table - (corner.Y - _radius);
                    if (depth < -0.02f)
                        continue;
                    // a corner a hair above the felt may come only that close this step; one sunk in is eased out
                    var allowed = depth < 0 ? depth / Step : MathF.Max(0, depth - 0.002f) * 0.2f / Step;
                    Touch(ref d, corner - new Vector3(0, _radius, 0), Vector3.UnitY, allowed);
                }
            }

            // the two dice bump as two balls
            var between = dice[1].Position - dice[0].Position;
            var distance = between.Length();
            if (distance < Bump && distance > 1e-4f)
            {
                var n = between / distance;
                var closing = Vector3.Dot(dice[1].Velocity - dice[0].Velocity, n);
                if (closing < 0)
                {
                    var j = -(1 + 0.4f) * closing / 2;
                    dice[0].Velocity -= j * n;
                    dice[1].Velocity += j * n;
                    dice[0].Resting = dice[1].Resting = false;
                }
                if (pass == 0)
                {
                    var apart = (Bump - distance) / 2 * n;
                    dice[0].Position -= apart;
                    dice[1].Position += apart;
                }
            }
        }

        for (var i = 0; i < 2; i++)
        {
            ref var d = ref dice[i];
            if (d.Resting)
            {
                d.Velocity = d.Spin = Vector3.Zero;
                continue;
            }
            // flat on the felt and barely moving for a moment: it rests (a die never creeps on its own)
            var quiet = touching[i] >= 3 && d.Velocity.LengthSquared() < 0.15f * 0.15f && d.Spin.LengthSquared() < 0.6f * 0.6f;
            d.Quiet = quiet ? d.Quiet + Step : 0;
            // balanced on an edge, which a real die never manages for long: the smallest touch tips it
            var propped = touching[i] < 3 && d.Position.Y < 0.3f && d.Velocity.LengthSquared() < 0.25f * 0.25f && d.Spin.LengthSquared() < 0.6f * 0.6f;
            d.Propped = propped ? d.Propped + Step : 0;
            if (d.Propped > 0.2f)
            {
                var a = _time * 7 + i;
                d.Spin += new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * 1.5f;
                d.Propped = 0;
            }
            if (d.Quiet > 0.15f)
            {
                d.Resting = true;
                d.Velocity = d.Spin = Vector3.Zero;
                continue;
            }
            // only while it flies or rolls fast: a die slowly tipping over is left alone
            if (d.Velocity.LengthSquared() > 1)
            {
                Edge(ref d.Position.X, ref d.Velocity.X, EdgeX);
                Edge(ref d.Position.Z, ref d.Velocity.Z, EdgeZ);
            }
            d.Position += d.Velocity * Step;
            var w = d.Spin * Step * 0.5f;
            d.Turn = Quaternion.Normalize(d.Turn + new Quaternion(w, 0) * d.Turn);
        }
    }

    /// <summary>
    /// The end of the room a die has: past it, its way further out is braked hard (no bounce off a rail nobody sees).
    /// Nothing pushes it back, so a die there still tips over freely and never stays propped on an edge.
    /// </summary>
    static void Edge(ref float position, ref float velocity, float limit)
    {
        if (position > limit && velocity > 0 || position < -limit && velocity < 0)
            velocity *= 0.8f;
    }

    /// <summary>A corner touching the felt: an impulse that stops it going in (bouncing a part back when fast), then friction.</summary>
    void Touch(ref Body d, Vector3 point, Vector3 n, float allowed)
    {
        var r = point - d.Position;
        var velocity = d.Velocity + Vector3.Cross(d.Spin, r);
        var vn = Vector3.Dot(velocity, n);
        var target = vn < -1.5f ? MathF.Max(-Restitution * vn, allowed) : allowed;
        if (vn >= target)
            return;
        var rn = Vector3.Cross(r, n);
        var j = (target - vn) / (1 + InverseInertia * rn.LengthSquared());
        d.Velocity += j * n;
        d.Spin += InverseInertia * Vector3.Cross(r, j * n);

        velocity = d.Velocity + Vector3.Cross(d.Spin, r);
        var slide = velocity - Vector3.Dot(velocity, n) * n;
        var speed = slide.Length();
        if (speed < 1e-5f)
            return;
        var t = slide / speed;
        var rt = Vector3.Cross(r, t);
        var jt = MathF.Min(speed / (1 + InverseInertia * rt.LengthSquared()), Friction * j);
        d.Velocity -= jt * t;
        d.Spin -= InverseInertia * Vector3.Cross(r, jt * t);
    }

    static readonly (Vector3 Axis, int Value)[] FaceAxes =
    {
        (Vector3.UnitX, 3), (-Vector3.UnitX, 4), (Vector3.UnitY, 1), (-Vector3.UnitY, 6), (Vector3.UnitZ, 2), (-Vector3.UnitZ, 5),
    };

    /// <summary>Lays a die exactly flat on the face nearest the felt (keeping its turn around the vertical); its face up.</summary>
    static int Settle(ref Body d)
    {
        var best = FaceAxes[0];
        var height = float.MinValue;
        foreach (var f in FaceAxes)
        {
            var y = Vector3.Transform(f.Axis, d.Turn).Y;
            if (y > height)
            {
                height = y;
                best = f;
            }
        }
        var up = Vector3.Normalize(Vector3.Transform(best.Axis, d.Turn));
        var axis = Vector3.Cross(up, Vector3.UnitY);
        if (axis.LengthSquared() > 1e-10f)
            d.Turn = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(up.Y, -1, 1))) * d.Turn);
        d.Position.Y = 0;
        d.Velocity = d.Spin = Vector3.Zero;
        return best.Value;
    }
}
