using CrystalBall.App;
using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Полноэкранный вихрь как TrueTaro LoadingMagicVortex: золотая/неоновая пыль, 3 центра, спад 0.95 с.
/// </summary>
public partial class VortexField : CanvasLayer
{
    private static readonly Color Gold = new(1f, 0.86f, 0.26f);
    private static readonly Color GoldHot = new(1.15f, 0.95f, 0.45f);
    private static readonly Color[] Neon =
    [
        new(0.05f, 1f, 0.92f),
        new(0.15f, 0.75f, 1f),
        new(0.72f, 0.25f, 1f),
        new(1f, 0.2f, 0.85f),
        new(0.2f, 1f, 0.45f),
        new(1f, 0.95f, 0.25f),
        new(1f, 0.45f, 0.15f),
    ];

    private const float Speed = 18f * (4f / 3f);
    private const float SwirlPeak = 640f * Speed;
    private const float InwardPeak = 95f * Speed;
    private const float Turbulence = 110f * Speed;
    private const float Drag = 1.35f;
    private const float BreakShakeAmp = 2.8f * Speed;
    private const float BreakVelScale = 18f * Speed;
    private const float DissipateOutward = 220f * Speed;

    private int _count;
    private Vector2[] _home = [];
    private Vector2[] _pos = [];
    private Vector2[] _vel = [];
    private Color[] _baseCol = [];
    private float[] _goldMix = [];
    private bool[] _becomesGold = [];
    private float[] _breakJitter = [];
    private float[] _turbPhase = [];
    private float[] _pxSize = [];

    private MultiMeshInstance2D? _mesh;
    private GpuParticles2D? _embers;
    private readonly RandomNumberGenerator _rng = new();

    private Vector2 _area = new(720, 1600);
    private Vector2 _center;
    private readonly Vector2[] _vortexCenters = new Vector2[3];
    private readonly float[] _vortexSpin = [1f, -1f, 1f];

    private float _elapsed;
    private bool _playing;
    private bool _windingDown;
    private float _windT;
    private float _windDur = 0.95f;
    private float _coverAlpha = 0.85f;
    private TaskCompletionSource<bool>? _windDone;

    public bool IsActive => _playing;

    public const int CanvasLayerIndex = 26;

    public override void _Ready()
    {
        Layer = CanvasLayerIndex;
        Visible = false;
        SetProcess(false);
        _rng.Randomize();
        // MultiMesh/частицы — лениво в Begin(), чтобы не тормозить первый кадр старта.
    }

    private void EnsureMesh()
    {
        if (_mesh != null)
            return;

        var quad = new QuadMesh { Size = Vector2.One };
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = quad,
            InstanceCount = AppConfig.VortexParticleCount,
        };
        _mesh = new MultiMeshInstance2D
        {
            Multimesh = multi,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        AddChild(_mesh);
        Allocate(AppConfig.VortexParticleCount);
    }

    public void SyncSize(Vector2 size)
    {
        if (size.X < 8f || size.Y < 8f)
            return;
        _area = size;
        _center = _area * new Vector2(0.5f, 0.46f);
        _vortexCenters[0] = _area * new Vector2(0.5f, 0.46f);
        _vortexCenters[1] = _area * new Vector2(0.30f, 0.36f);
        _vortexCenters[2] = _area * new Vector2(0.70f, 0.60f);
        if (_embers != null)
            _embers.Position = _center;
    }

    /// <summary>Прогрев MultiMesh вне «Спросить», чтобы не фризить ритуал.</summary>
    public void Prewarm()
    {
        EnsureMesh();
        if (_area.X < 8f || _area.Y < 8f)
        {
            var vp = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
            if (vp.X >= 8f)
                SyncSize(vp);
        }

        if (_count <= 0 && _home.Length > 0)
            FillField(AppConfig.VortexParticleCount);
    }

    public async Task PlayAsync(Task holdUntil, CancellationToken cancellationToken = default)
    {
        var holdSec = Mathf.Max(0.5f, AppConfig.Current.VortexSeconds);
        Begin();
        // Дать CastingLog отрисоваться до плотного _Process вихря.
        if (GetTree() is { } tree)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        try
        {
            await Task.WhenAll(Safe(holdUntil), WaitElapsed(holdSec, cancellationToken));
            await WindDownAsync(AppConfig.VortexFadeSeconds, cancellationToken);
        }
        finally
        {
            GetNodeOrNull<AudioService>("/root/AudioService")?.StopVortexMix(0.4f);
        }
    }

    public override void _Process(double delta)
    {
        if (!_playing)
            return;

        var dt = (float)delta;
        _elapsed += dt;
        var dissipate = 0f;
        if (_windingDown)
        {
            _windT += dt / Mathf.Max(_windDur, 0.35f);
            dissipate = Mathf.Clamp(_windT, 0f, 1f);
            dissipate *= dissipate * (3f - 2f * dissipate);
            if (_windT >= 1f)
            {
                Stop();
                return;
            }
        }

        var swirlT = Smooth(AppConfig.VortexBurstSeconds, AppConfig.VortexRampSeconds, _elapsed);
        var densityT = Mathf.Clamp((_elapsed - AppConfig.VortexBurstSeconds) / 0.85f, 0f, 1f);
        var swirlStrength = swirlT * (1f - dissipate * 0.92f) + 0.25f * (1f - dissipate);
        var dragFactor = 1f / (1f + Drag * dt);
        var turbScale = Turbulence * (0.65f + swirlT * 0.35f) * dt;
        var goldRate = dt * Speed;
        var orbitR = Mathf.Min(_area.X, _area.Y) * 0.52f;
        var shakeFreqA = 48f * Speed;
        var shakeFreqB = 41f * Speed;
        var turbA = 9.5f * Speed;
        var turbB = 7.8f * Speed;

        for (var i = 0; i < _count; i++)
        {
            var home = _home[i];
            var pos = _pos[i];
            var vel = _vel[i];
            if (_elapsed < AppConfig.VortexBurstSeconds)
            {
                var jd = _breakJitter[i];
                var localT = Mathf.Clamp((_elapsed - jd) / Mathf.Max(AppConfig.VortexBurstSeconds - jd, 0.05f), 0f, 1f);
                var phase = _turbPhase[i];
                var shake = new Vector2(
                    Mathf.Sin(_elapsed * shakeFreqA + phase),
                    Mathf.Cos(_elapsed * shakeFreqB + phase * 1.2f)
                ) * (1f - localT) * BreakShakeAmp;
                pos = home + shake;
                vel = (pos - home) * BreakVelScale;
            }
            else
            {
                vel += Force(pos, swirlStrength) * dt;
                var phase = _turbPhase[i];
                vel += new Vector2(
                    Mathf.Sin(_elapsed * turbA + phase),
                    Mathf.Cos(_elapsed * turbB + phase * 1.4f)
                ) * turbScale;
                var fromC = pos - _center;
                var dist = fromC.Length();
                if (dissipate < 0.2f && dist > orbitR)
                    vel -= fromC.Normalized() * ((dist - orbitR) * 8f) * dt;
                if (dissipate > 0.001f)
                {
                    var outward = dist > 0.001f ? fromC / dist : Vector2.Right;
                    vel += (new Vector2(140f, 48f) * Speed * dissipate + outward * DissipateOutward * dissipate) * dt;
                }

                vel *= dragFactor;
                var spdSq = vel.LengthSquared();
                if (spdSq > 900f * 900f)
                    vel *= 900f / Mathf.Sqrt(spdSq);
                pos += vel * dt;
            }

            if (_becomesGold[i])
            {
                var gt = Mathf.Clamp(densityT + 0.35f, 0f, 1f);
                _goldMix[i] = Mathf.Min(_goldMix[i] + (0.55f + densityT * 0.8f) * goldRate, gt);
            }

            _pos[i] = pos;
            _vel[i] = vel;
        }

        _coverAlpha = Mathf.Clamp(0.88f + densityT * 0.22f, 0f, 1f) * (1f - dissipate);
        if (_embers != null)
        {
            var mod = _embers.Modulate;
            mod.A = 1f - dissipate;
            _embers.Modulate = mod;
            if (dissipate > 0.35f)
                _embers.Emitting = false;
        }

        WriteMesh();
    }

    private void Begin()
    {
        EnsureMesh();
        var vp = GetViewport()?.GetVisibleRect().Size ?? _area;
        SyncSize(vp);
        FillField(AppConfig.VortexParticleCount);
        _elapsed = 0f;
        _windingDown = false;
        _windT = 0f;
        _coverAlpha = 0.9f;
        _playing = true;
        Visible = true;
        SetProcess(true);
        StartEmbers();
        WriteMesh();
        GetNodeOrNull<AudioService>("/root/AudioService")?.StartVortexMix();
    }

    private async Task WindDownAsync(float duration, CancellationToken cancellationToken)
    {
        if (!_playing)
            return;
        _windingDown = true;
        _windT = 0f;
        _windDur = Mathf.Max(duration, 0.35f);
        GetNodeOrNull<AudioService>("/root/AudioService")?.StopVortexMix(_windDur);
        _windDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() =>
        {
            Stop();
            _windDone.TrySetResult(true);
        });
        await _windDone.Task;
    }

    private void Stop()
    {
        _playing = false;
        _windingDown = false;
        SetProcess(false);
        Visible = false;
        StopEmbers();
        _windDone?.TrySetResult(true);
        _windDone = null;
    }

    private void Allocate(int n)
    {
        _home = new Vector2[n];
        _pos = new Vector2[n];
        _vel = new Vector2[n];
        _baseCol = new Color[n];
        _goldMix = new float[n];
        _becomesGold = new bool[n];
        _breakJitter = new float[n];
        _turbPhase = new float[n];
        _pxSize = new float[n];
    }

    private void FillField(int n)
    {
        _count = 0;
        var halfMin = Mathf.Min(_area.X, _area.Y) * 0.54f;
        var coreN = (int)(n * 0.28f);
        const int arms = 5;
        for (var i = 0; i < coreN; i++)
        {
            var ang = _rng.Randf() * Mathf.Tau;
            var r = halfMin * 0.26f * Mathf.Pow(_rng.Randf(), 0.45f);
            Append(_center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.78f) * r);
        }

        var spiralN = n - coreN;
        for (var i = 0; i < spiralN; i++)
        {
            var arm = i % arms;
            var t = i / (float)Mathf.Max(spiralN, 1);
            var ang = t * Mathf.Tau * 3.6f + arm * (Mathf.Tau / arms) + _rng.RandfRange(-0.14f, 0.14f);
            var r = halfMin * (0.05f + t * 0.95f) * _rng.RandfRange(0.86f, 1.14f);
            Append(_center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.80f) * r);
        }
    }

    private void Append(Vector2 home)
    {
        if (_count >= _home.Length)
            return;
        Color col;
        var gold = true;
        if (_rng.Randf() < 0.28f)
        {
            col = Neon[_rng.RandiRange(0, Neon.Length - 1)];
            gold = _rng.Randf() < 0.62f;
        }
        else
        {
            col = Gold.Lerp(GoldHot, _rng.Randf());
        }

        var i = _count++;
        _home[i] = home;
        _pos[i] = home;
        _vel[i] = Vector2.Zero;
        _baseCol[i] = col;
        _goldMix[i] = gold ? 0.25f : 0f;
        _becomesGold[i] = gold;
        _breakJitter[i] = _rng.RandfRange(0f, AppConfig.VortexBurstSeconds * 0.85f);
        _turbPhase[i] = _rng.Randf() * Mathf.Tau;
        _pxSize[i] = gold ? _rng.RandfRange(0.73f, 2.13f) : _rng.RandfRange(0.60f, 1.40f);
    }

    private Vector2 Force(Vector2 pos, float strength)
    {
        if (strength <= 0.001f)
            return Vector2.Zero;
        var force = Vector2.Zero;
        for (var vi = 0; vi < _vortexCenters.Length; vi++)
        {
            var rel = pos - _vortexCenters[vi];
            var distSq = Mathf.Max(rel.LengthSquared(), 36f);
            var invDist = 1f / Mathf.Sqrt(distSq);
            var weight = 6400f / distSq;
            var tangent = new Vector2(-rel.Y * invDist, rel.X * invDist);
            force += tangent * (SwirlPeak * _vortexSpin[vi] * strength * weight);
            force -= rel * (invDist * InwardPeak * strength * weight * 0.35f);
        }

        return force;
    }

    private Color ColorAt(int i)
    {
        var gm = _goldMix[i];
        var col = _baseCol[i].Lerp(Gold, Mathf.Clamp(gm + 0.35f, 0f, 1f));
        if (gm > 0.45f)
            col = col.Lerp(GoldHot, (gm - 0.45f) * 0.85f);
        var a = _coverAlpha * (0.62f + gm * 0.48f);
        if (_elapsed < AppConfig.VortexBurstSeconds)
            a *= 0.75f + 0.25f * Mathf.Clamp(_elapsed / AppConfig.VortexBurstSeconds, 0f, 1f);
        if (_windingDown)
            a *= 1f - Mathf.Clamp(_windT, 0f, 1f) * 0.95f;
        col.A = a;
        return col;
    }

    private void WriteMesh()
    {
        if (_mesh?.Multimesh == null)
            return;
        var multi = _mesh.Multimesh;
        var visible = _playing ? _count : 0;
        multi.VisibleInstanceCount = visible;
        for (var i = 0; i < visible; i++)
        {
            var sz = _pxSize[i];
            multi.SetInstanceTransform2D(i, new Transform2D(0f, new Vector2(sz, sz), 0f, _pos[i]));
            multi.SetInstanceColor(i, ColorAt(i));
        }
    }

    private void StartEmbers()
    {
        StopEmbers();
        var gpu = new GpuParticles2D
        {
            ZIndex = 1,
            Position = _center,
            Amount = 260,
            Lifetime = 1.55,
            Preprocess = 0.55,
            Randomness = 0.35f,
            VisibilityRect = new Rect2(-2400, -2400, 4800, 4800),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            ProcessMaterial = new ParticleProcessMaterial
            {
                ParticleFlagDisableZ = true,
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
                EmissionSphereRadius = Mathf.Min(_area.X, _area.Y) * 0.34f,
                Direction = new Vector3(0, -1, 0),
                Spread = 180f,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 12f,
                InitialVelocityMax = 70f,
                ScaleMin = 0.09f,
                ScaleMax = 0.35f,
                Color = new Color(1f, 0.88f, 0.32f, 0.92f),
            },
            Emitting = true,
        };
        AddChild(gpu);
        _embers = gpu;
    }

    private void StopEmbers()
    {
        if (_embers == null)
            return;
        _embers.Emitting = false;
        _embers.QueueFree();
        _embers = null;
    }

    private async Task WaitElapsed(float seconds, CancellationToken cancellationToken)
    {
        while (_elapsed < seconds && !cancellationToken.IsCancellationRequested && IsInsideTree())
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static async Task Safe(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // вихрь не должен зависать из-за сети
        }
    }

    private static float Smooth(float edge0, float edge1, float x)
    {
        if (Mathf.IsEqualApprox(edge0, edge1))
            return x >= edge1 ? 1f : 0f;
        var t = Mathf.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
