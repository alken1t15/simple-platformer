using System.Collections.ObjectModel;
using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Адаптирует данные Core на потоке окна; не выполняет физику, ввод или HUD.</summary>
public sealed class GameScenePresenter
{
    private readonly LevelDefinition _level;
    private readonly Dictionary<string, SceneObject> _surfaces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SceneObject> _coins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SceneObject> _hazards = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hiddenCoins = new(StringComparer.Ordinal);
    private readonly VisualScene? _visual;
    private readonly Dictionary<string, (int Index, SceneObject[] Parts, MaterialData[] Materials)> _fragile = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hiddenPlatforms = new(StringComparer.Ordinal);
    private readonly HashSet<int> _unshadowedSurfaceIndices = [];
    private static readonly MaterialData FragileBody = new() { BaseColor = new(1f, 0.72f, 0.35f) };
    private static readonly MaterialData WarningA = new() { BaseColor = new(1f, 0.43f, 0.12f) };
    private static readonly MaterialData WarningB = new() { BaseColor = new(1f, 0.8f, 0.18f) };
    private Guid _sessionId;
    private long _tick;
    private BallState _lastBall;
    private double _simulationSeconds;
    private Vector3 _spinAxis;
    private double _spinSpeed;
    private Vector3 _previousPosition;
    private Quaternion _previousRotation = Quaternion.Identity;
    private Quaternion _currentRotation = Quaternion.Identity;

    public Scene Scene { get; } = new();
    public SceneObject BallObject { get; }
    public IReadOnlyDictionary<string, SceneObject> SurfaceObjects { get; }
    public IReadOnlyDictionary<string, SceneObject> CoinObjects { get; }
    public IReadOnlyDictionary<string, SceneObject> HazardObjects { get; }
    public IReadOnlyList<SceneObject> FinishObjects { get; }

    public GameScenePresenter(LevelDefinition level, bool visual = false)
    {
        ArgumentNullException.ThrowIfNull(level);
        _level = level;
        bool maze = level.Id == "level2";
        var accent = visual ? VisualScene.GuardRail : new MaterialData { BaseColor = new ColorRgba(0.55f, 0.25f, 0.75f) };
        _visual = visual ? new VisualScene(Scene, level.Surfaces) : null;
        var track = visual ? VisualScene.Track : new MaterialData { BaseColor = maze ? new ColorRgba(0.25f, 0.25f, 0.3f) : new ColorRgba(0.72f, 0.75f, 0.8f) };
        var gold = visual ? VisualScene.Gold : new MaterialData { BaseColor = new ColorRgba(1f, 0.72f, 0.08f) };
        var danger = new MaterialData { BaseColor = new ColorRgba(0.9f, 0.08f, 0.06f) };
        var green = visual ? VisualScene.Finish : new MaterialData { BaseColor = new ColorRgba(0.08f, 0.85f, 0.25f) };
        for (int surfaceIndex = 0; surfaceIndex < level.Surfaces.Count; surfaceIndex++)
        {
            var surface = level.Surfaces[surfaceIndex];
            int firstPart = Scene.Objects.Count;
            bool fragile = surface.CollapseDelaySeconds > 0;
            var item = Add(surface.IsBarrier ? GameMeshes.Box : visual ? VisualScene.RoadBody : GameMeshes.Box,
                surface.IsBarrier ? accent : fragile ? FragileBody : track);
            SetBounds(item, surface.Bounds);
            _surfaces.Add(surface.Id, item);
            if (!surface.IsBarrier) _visual?.SurfaceDetail(surfaceIndex, surface.Bounds, fragile);
            if (fragile)
            {
                _unshadowedSurfaceIndices.Add(surfaceIndex);
                var parts = Scene.Objects.Skip(firstPart).ToArray();
                _fragile.Add(surface.Id, (surfaceIndex, parts, parts.Select(part => part.Material).ToArray()));
            }
        }
        foreach (var coin in level.Coins)
        {
            var item = Add(visual ? VisualScene.Coin : GameMeshes.Coin, gold);
            item.Transform.Position = coin.Center;
            item.Transform.Scale = new Vector3(coin.Radius);
            _coins.Add(coin.Id, item);
        }
        foreach (var hazard in level.Hazards)
        {
            var material = visual ? hazard.Response == HazardResponse.Knockback
                ? VisualScene.BumperBeam : VisualScene.LethalObstacle : danger;
            _hazards.Add(hazard.Id, Add(GameMeshes.Box, material));
            // Декоративная ось не добавляет коллайдер, позу самой балки задаёт snapshot
            var axle = Add(visual ? VisualScene.SoftBox : GameMeshes.Box, maze ? accent : track);
            axle.Transform.Position = hazard.Pivot - Vector3.UnitY * 0.25f;
            axle.Transform.Scale = new Vector3(0.12f, 0.5f, 0.12f);
        }
        float thickness = MathF.Min(0.08f, MathF.Min(level.Finish.HalfExtents.X,
            MathF.Min(level.Finish.HalfExtents.Y, level.Finish.HalfExtents.Z)));
        var frame = new List<SceneObject>();
        foreach (int side in new[] { -1, 1 })
            FramePart(new Vector3(side * level.Finish.HalfExtents.X, 0f, 0f),
                new Vector3(thickness, level.Finish.HalfExtents.Y * 2f, thickness));
        FramePart(new Vector3(0f, level.Finish.HalfExtents.Y, 0f),
            new Vector3(level.Finish.HalfExtents.X * 2f + thickness, thickness, thickness));
        FinishObjects = frame.AsReadOnly();
        _visual?.FinishDetail(level.Finish);
        BallObject = Add(visual ? VisualScene.Ball : GameMeshes.Sphere,
            visual ? VisualScene.BallMaterial(GameSkins.All[0]) : GameSkins.All[0].Material);
        BallObject.Transform.Position = level.Spawn;
        BallObject.Transform.Scale = new Vector3(SimulationSettings.Default.BallRadius);
        SurfaceObjects = new ReadOnlyDictionary<string, SceneObject>(_surfaces);
        CoinObjects = new ReadOnlyDictionary<string, SceneObject>(_coins);
        HazardObjects = new ReadOnlyDictionary<string, SceneObject>(_hazards);

        void FramePart(Vector3 localCenter, Vector3 size)
        {
            var item = Add(visual ? VisualScene.SoftBox : GameMeshes.Box, green);
            item.Transform.Position = level.Finish.ToWorld(localCenter);
            item.Transform.Rotation = level.Finish.Orientation;
            item.Transform.Scale = size;
            frame.Add(item);
        }

    }

    public void Apply(SessionSnapshot snapshot, SkinDefinition skin) => Apply(snapshot, skin, null);

    /// <summary>motionPath — полный путь между прошлым и текущим Apply, если он доступен.</summary>
    public void Apply(SessionSnapshot snapshot, SkinDefinition skin, IReadOnlyList<MotionSegment>? motionPath)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(skin);
        skin.Material.Validate();
        if (!StringComparer.Ordinal.Equals(snapshot.LevelId, _level.Id))
            throw new ArgumentException("Snapshot относится к другому уровню", nameof(snapshot));
        bool reset = _sessionId != snapshot.SessionId;
        if (!reset && snapshot.Tick < _tick)
            throw new ArgumentException("Порядок snapshot нарушен", nameof(snapshot));
        if (!reset && (snapshot.SimulationSeconds < _simulationSeconds
            || (snapshot.Tick == _tick && snapshot.SimulationSeconds != _simulationSeconds)))
            throw new ArgumentException("Время симуляции не может убывать или меняться внутри одного tick", nameof(snapshot));
        if (!reset && (MathF.Abs(snapshot.Ball.Radius - _lastBall.Radius) > 1e-6f
            || (snapshot.Tick == _tick && snapshot.Ball != _lastBall)))
            throw new ArgumentException("Радиус сеанса и состояние уже показанного tick неизменяемы", nameof(snapshot));
        if (snapshot.CollectedCoinIds.Any(id => !_coins.ContainsKey(id))
            || snapshot.Hazards.Count != _hazards.Count || snapshot.Hazards.Any(pose => !_hazards.ContainsKey(pose.Id)))
            throw new ArgumentException("Неизвестные или отсутствующие ID объектов", nameof(snapshot));

        if (snapshot.Platforms.Count != _fragile.Count || snapshot.Platforms.Any(state => !_fragile.ContainsKey(state.Id)))
            throw new ArgumentException("Неизвестные или отсутствующие платформы", nameof(snapshot));

        Quaternion rotation = reset ? Quaternion.Identity : _currentRotation;
        var spinAxis = reset ? Vector3.Zero : _spinAxis;
        double spinSpeed = reset ? 0d : _spinSpeed;
        if (!reset && snapshot.Tick > _tick)
        {
            if (motionPath is not null) ValidatePath(motionPath, _lastBall.Center, snapshot.Ball.Center);
            if (_lastBall.Grounded && snapshot.Ball.Grounded)
            {
                if (motionPath is null)
                    rotation = Roll(rotation, snapshot.Ball.Center - _lastBall.Center, snapshot.Ball);
                else
                    foreach (var segment in motionPath)
                        rotation = Roll(rotation, segment.End - segment.Start, snapshot.Ball);
            }
            else
                rotation = Spin(rotation, spinAxis, spinSpeed, snapshot.SimulationSeconds - _simulationSeconds);
        }
        if (snapshot.Ball.Grounded) (spinAxis, spinSpeed) = GroundSpin(snapshot.Ball);

        // Валидация завершена до изменения Scene и внутреннего состояния Presenter
        var collected = new HashSet<string>(snapshot.CollectedCoinIds, StringComparer.Ordinal);
        foreach (var (id, item) in _coins)
        {
            if (collected.Contains(id))
            {
                if (_hiddenCoins.Add(id)) Scene.Remove(item);
            }
            else if (_hiddenCoins.Remove(id)) Scene.Add(item);
        }
        foreach (var state in snapshot.Platforms)
        {
            var group = _fragile[state.Id];
            bool wasHidden = _hiddenPlatforms.Contains(state.Id);
            if (state.Collapsed && !wasHidden)
            {
                foreach (var part in group.Parts) Scene.Remove(part);
                _hiddenPlatforms.Add(state.Id);
                _unshadowedSurfaceIndices.Add(group.Index);
            }
            else if (!state.Collapsed && wasHidden)
            {
                foreach (var part in group.Parts) Scene.Add(part);
                _hiddenPlatforms.Remove(state.Id);
            }
            bool flash = ((int)((snapshot.SimulationSeconds - (state.TriggeredAtSeconds ?? 0)) * 8) & 1) == 0;
            for (int i = 0; i < group.Parts.Length; i++)
                group.Parts[i].Material = state.TriggeredAtSeconds is null ? group.Materials[i]
                    : _visual is not null ? (flash ? VisualScene.CrumbleWarningA : VisualScene.CrumbleWarningB)
                    : flash ? WarningA : WarningB;
        }
        foreach (var pose in snapshot.Hazards) SetBounds(_hazards[pose.Id], pose.Bounds);
        if (reset || snapshot.Tick > _tick)
        {
            _previousPosition = reset ? snapshot.Ball.Center : _lastBall.Center;
            _previousRotation = reset ? rotation : _currentRotation;
        }
        _currentRotation = rotation;
        BallObject.Transform.Position = snapshot.Ball.Center;
        BallObject.Transform.Scale = new Vector3(snapshot.Ball.Radius);
        BallObject.Transform.Rotation = rotation;
        BallObject.Material = _visual is not null ? VisualScene.BallMaterial(skin) : skin.Material;
        if (_visual is not null)
        {
            VisualScene.AnimateCoins(_coins, snapshot.SimulationSeconds);
            _visual.UpdateContactShadow(snapshot.Ball, snapshot.SimulationSeconds, _unshadowedSurfaceIndices);
        }
        _sessionId = snapshot.SessionId;
        _tick = snapshot.Tick;
        _lastBall = snapshot.Ball;
        _simulationSeconds = snapshot.SimulationSeconds;
        _spinAxis = spinAxis;
        _spinSpeed = spinSpeed;
    }

    /// <summary>Только отображение между двумя принятыми ticks; snapshot и физика не меняются.</summary>
    public void InterpolateBall(float alpha)
    {
        if (!float.IsFinite(alpha) || alpha < 0 || alpha > 1) throw new ArgumentOutOfRangeException(nameof(alpha));
        if (_sessionId == Guid.Empty) return;
        var position = Vector3.Lerp(_previousPosition, _lastBall.Center, alpha);
        BallObject.Transform.Position = position;
        BallObject.Transform.Rotation = Quaternion.Slerp(_previousRotation, _currentRotation, alpha);
        _visual?.UpdateContactShadow(_lastBall with { Center = position }, _simulationSeconds, _unshadowedSurfaceIndices);
    }

    /// <summary>Переход/restart не интерполирует между разными попытками или через паузу.</summary>
    public void ResetInterpolation()
    {
        _previousPosition = _lastBall.Center;
        _previousRotation = _currentRotation;
        InterpolateBall(1);
    }

    private SceneObject Add(MeshData mesh, MaterialData material)
    {
        var item = new SceneObject(mesh, material);
        Scene.Add(item);
        return item;
    }

    private static void SetBounds(SceneObject item, OrientedBox box)
    {
        item.Transform.Position = box.Center;
        item.Transform.Rotation = box.Orientation;
        item.Transform.Scale = box.HalfExtents * 2f;
    }

    private static Quaternion Roll(Quaternion rotation, Vector3 displacement, BallState ball)
    {
        Vector3 normal = Vector3.Normalize(ball.GroundNormal);
        Vector3 tangent = displacement - Vector3.Dot(displacement, normal) * normal;
        float distance = tangent.Length();
        if (distance < 1e-7f) return rotation;
        Vector3 axis = Vector3.Normalize(Vector3.Cross(normal, tangent));
        float angle = (float)Math.IEEERemainder((double)distance / ball.Radius, Math.Tau);
        return Quaternion.Normalize(Quaternion.Concatenate(rotation, Quaternion.CreateFromAxisAngle(axis, angle)));
    }

    // В воздухе сохраняется угловая скорость последнего контакта, камера её не задаёт.
    // double-компоненты не переполняются при конечной float-скорости snapshot.
    private static (Vector3 Axis, double Speed) GroundSpin(BallState ball)
    {
        Vector3 n = Vector3.Normalize(ball.GroundNormal), v = ball.Velocity;
        double x = (double)n.Y * v.Z - (double)n.Z * v.Y;
        double y = (double)n.Z * v.X - (double)n.X * v.Z;
        double z = (double)n.X * v.Y - (double)n.Y * v.X;
        double length = Math.Sqrt(x * x + y * y + z * z);
        if (length < 1e-7d) return (Vector3.Zero, 0d);
        return (new((float)(x / length), (float)(y / length), (float)(z / length)), length / ball.Radius);
    }

    private static Quaternion Spin(Quaternion rotation, Vector3 axis, double speed, double seconds)
    {
        if (speed == 0d || seconds <= 0d) return rotation;
        // Сначала модуль по периоду: speed*seconds может переполниться на допустимом double-времени.
        double angle = Math.IEEERemainder(seconds, Math.Tau / speed) * speed;
        return Quaternion.Normalize(Quaternion.Concatenate(rotation, Quaternion.CreateFromAxisAngle(axis, (float)angle)));
    }

    private static void ValidatePath(IReadOnlyList<MotionSegment> path, Vector3 start, Vector3 end)
    {
        if (path.Count == 0) throw new ArgumentException("Путь пуст", nameof(path));
        float fraction = 0f;
        Vector3 point = start;
        foreach (var segment in path)
        {
            if (!ValidPosition(segment.Start) || !ValidPosition(segment.End)
                || !float.IsFinite(segment.StartFraction) || !float.IsFinite(segment.EndFraction)
                || segment.StartFraction < 0f
                || MathF.Abs(segment.StartFraction - fraction) > 1e-5f
                || segment.EndFraction <= segment.StartFraction || segment.EndFraction > 1f
                || Vector3.Distance(point, segment.Start) > 1e-4f)
                throw new ArgumentException("Путь должен непрерывно покрывать [0,1]", nameof(path));
            point = segment.End;
            fraction = segment.EndFraction;
        }
        if (MathF.Abs(fraction - 1f) > 1e-5f || Vector3.Distance(point, end) > 1e-4f)
            throw new ArgumentException("Путь не соответствует snapshot", nameof(path));
    }

    private static bool ValidPosition(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y)
        && float.IsFinite(value.Z) && MathF.Abs(value.X) <= 10000f
        && MathF.Abs(value.Y) <= 10000f && MathF.Abs(value.Z) <= 10000f;
}
