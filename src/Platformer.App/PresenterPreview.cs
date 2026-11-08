using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Контрактный показ Presenter со сценарными snapshot, не GameSession или игровой ввод.</summary>
public sealed class PresenterPreview
{
    private Guid _sessionId = Guid.NewGuid();
    private long _tick;
    private readonly HazardDefinition _hazard;

    public GameScenePresenter Presenter { get; }
    public Camera Camera { get; }
    public SkinDefinition Skin { get; private set; }

    public PresenterPreview(string skinId = "classic-blue", bool overview = false, bool visual = false)
    {
        var track = TrainingTrack.Create();
        float height = track.Surfaces.Single(surface => surface.Id == "upper-platform").Bounds.Center.Y + 0.2f;
        var hazard = new HazardDefinition("preview-beam", new Vector3(7f, height + 0.55f, 10f),
            Vector3.UnitY, Vector3.Zero, new Vector3(2f, 0.15f, 0.15f), Quaternion.Identity, 1f, 0f);
        var level = new LevelDefinition("presenter-preview", track.Spawn, track.KillY,
            track.Finish, track.Surfaces, track.Coins, [hazard]);
        _hazard = hazard;
        Skin = GameSkins.Get(skinId);
        Presenter = new GameScenePresenter(level, visual);
        Camera = new Camera
        {
            Position = overview ? new Vector3(18f, 22f, 27f) : new Vector3(0f, 2f, 3.5f),
            Target = overview ? new Vector3(1f, 0f, 7f) : new Vector3(-0.5f, 0.5f, 0f),
        };
        if (visual)
        {
            Camera.FarPlane = 220f;
            Camera.Position = overview ? new(17f, 14f, 23f) : new(-3.8f, 2.8f, 5.4f);
            Camera.Target = overview ? new(1f, 0.2f, 9f) : new(0.9f, 0.4f, 0.4f);
            Camera.FieldOfViewDegrees = overview ? 54f : 50f;
        }
        Apply();
    }

    public void Advance()
    {
        if (++_tick >= 360) { Restart(); return; }
        Apply();
    }

    public void Restart()
    {
        _sessionId = Guid.NewGuid();
        _tick = 0;
        Apply();
    }

    public void ToggleSkin()
    {
        int index = GameSkins.All.ToList().FindIndex(skin => skin.Id == Skin.Id);
        Skin = GameSkins.All[(index + 1) % GameSkins.All.Count];
        Apply();
    }

    private void Apply()
    {
        var ball = new BallState(new Vector3(-1f + 0.9f * MathF.Sin(_tick / 40f), 0.501f, 0f),
            Vector3.Zero, 0.5f, true, Vector3.UnitY, 0f, 0f);
        var snapshot = new SessionSnapshot(_sessionId, "presenter-preview", _tick, _tick / 120d,
            SessionPhase.Running, ball, _tick >= 120 ? ["coin-start-1"] : [], [HazardMotion.Evaluate(_hazard, _tick / 120d)]);
        Presenter.Apply(snapshot, Skin);
    }
}
