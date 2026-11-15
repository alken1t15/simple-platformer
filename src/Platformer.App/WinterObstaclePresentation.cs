using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

public sealed partial class GameScenePresenter
{
    private static readonly MeshData SnowBoulder = new(GameMeshes.Sphere.Vertices.ToArray().Select(v => new Vertex(v.Position * 0.5f, v.Uv, v.Normal)).ToArray(), GameMeshes.Sphere.Indices.ToArray());
    private readonly Dictionary<string, (SceneObject Rod, float Top)> _pressRods = new();
    private bool CreateWinterHazard(HazardDefinition hazard, bool visual)
    {
        if (hazard.MotionKind != HazardMotionKind.Shuttle) return false;
        bool press = MathF.Abs(hazard.Axis.Y) > 0.9f;
        var art = MachineArt.Box(hazard.HalfExtents * 2, MachineFinish.Danger);
        var item = Add(press && visual ? art.Mesh : visual ? SnowBoulder : GameMeshes.Box,
            press ? art.Material : VisualScene.SnowBoulderMaterial);
        _hazards.Add(hazard.Id, item);
        SetBounds(item, HazardMotion.Evaluate(hazard, 0).Bounds);
        if (press)
        {
            float top = hazard.Pivot.Y + hazard.Amplitude + hazard.HalfExtents.Y;
            var rod = Add(GameMeshes.Box, VisualScene.PressSteel);
            _pressRods.Add(hazard.Id, (rod, top));
        }
        return true;
    }
}
