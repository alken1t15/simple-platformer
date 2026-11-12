using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

public sealed partial class GameScenePresenter
{
    private readonly Dictionary<string, (SceneObject Rope, Vector3 Pivot)> _pendulumRopes = new(StringComparer.Ordinal);
    private static readonly MaterialData ExpertRope = new() { BaseColor = new(0.88f, 0.73f, 0.45f) };
    private static readonly MaterialData ExpertMetal = new() { BaseColor = new(0.13f, 0.35f, 0.38f) };

    private bool CreateExpertHazard(HazardDefinition hazard, bool visual, MaterialData fallback)
    {
        if (hazard.MotionKind == HazardMotionKind.Rotation) return false;
        if (hazard.MotionKind == HazardMotionKind.Pendulum)
        {
            // Камень использует округлённый параллелепипед внутри физического OBB,
            // а не сферу с невидимыми углами коллайдера. Канат — тонкая декорация.
            _hazards.Add(hazard.Id, Add(visual ? VisualScene.SoftBox : GameMeshes.Box,
                visual ? VisualScene.PendulumStone : fallback));
            if (hazard.CenterOffset.LengthSquared() > 0.000001f)
            {
                var rope = Add(GameMeshes.Box, ExpertRope);
                _pendulumRopes.Add(hazard.Id, (rope, hazard.Pivot));
            }
            var pin = Add(visual ? VisualScene.SoftBox : GameMeshes.Box, ExpertMetal);
            pin.Transform.Position = hazard.Pivot;
            pin.Transform.Scale = new(0.45f, 0.32f, 0.45f);
        }
        else
        {
            _hazards.Add(hazard.Id, Add(GameMeshes.Box, visual ? VisualScene.BumperBeam : fallback));
            // Направляющая ниже дорожки; опоры находятся за пределами хода толкателя.
            var rail = Add(GameMeshes.Box, ExpertMetal);
            SetSegment(rail, hazard.Pivot - Vector3.UnitY * 0.9f - hazard.Axis * (hazard.Amplitude + 1.3f),
                hazard.Pivot - Vector3.UnitY * 0.9f + hazard.Axis * (hazard.Amplitude + 1.3f), 0.16f);
            foreach (int side in new[] { -1, 1 })
            {
                var end = Add(visual ? VisualScene.SoftBox : GameMeshes.Box, ExpertMetal);
                end.Transform.Position = hazard.Pivot - Vector3.UnitY * 0.22f
                    + hazard.Axis * ((hazard.Amplitude + 1.4f) * side);
                end.Transform.Scale = new(0.9f, 1.2f, 0.6f);
            }
        }
        SetBounds(_hazards[hazard.Id], HazardMotion.Evaluate(hazard, 0).Bounds);
        if (_pendulumRopes.TryGetValue(hazard.Id, out var initial))
            SetSegment(initial.Rope, initial.Pivot, HazardMotion.Evaluate(hazard, 0).Bounds.Center, 0.065f);
        return true;
    }

    private static void SetSegment(SceneObject part, Vector3 from, Vector3 to, float thickness)
    {
        Vector3 direction = to - from;
        float length = direction.Length();
        var axis = Vector3.Normalize(direction);
        float dot = Math.Clamp(Vector3.Dot(Vector3.UnitY, axis), -1, 1);
        part.Transform.Position = (from + to) / 2;
        part.Transform.Scale = new(thickness, length, thickness);
        part.Transform.Rotation = dot > 0.99999f ? Quaternion.Identity
            : dot < -0.99999f ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI)
            : Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY, axis)), MathF.Acos(dot));
    }
}
