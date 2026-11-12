using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

internal static class ExpertGeometry
{
    internal static HazardDefinition Pendulum(string id, float x, float top, float z, float phase = 0) =>
        new(id, new(x, top + 4.8f, z), Vector3.UnitX, new(0, -4.25f, 0), new(0.68f),
            Quaternion.Identity, 1.25f, phase, HazardResponse.Knockback, HazardMotionKind.Pendulum, 0.95f);

    internal static HazardDefinition Piston(string id, float x, float top, float z, float phase = 0) =>
        new(id, new(x, top + 0.55f, z), Vector3.UnitZ, Vector3.Zero, new(0.38f, 0.55f, 0.9f),
            Quaternion.Identity, 1.45f, phase, HazardResponse.Knockback, HazardMotionKind.Shuttle, 3.2f);

    // Стойки и перекладина имеют те же bounds в физике и рендерере.
    internal static SurfaceDefinition[] Gantry(string id, float x, float top, float z) =>
    [
        Flat(id + "-left-footing", x, top, z - 5, 1.5f, 1.5f),
        Flat(id + "-right-footing", x, top, z + 5, 1.5f, 1.5f),
        Barrier(id + "-left", new(x, top + 2.4f, z - 5), new(0.18f, 2.4f, 0.18f)),
        Barrier(id + "-right", new(x, top + 2.4f, z + 5), new(0.18f, 2.4f, 0.18f)),
        Barrier(id + "-lintel", new(x, top + 4.8f, z), new(0.2f, 0.18f, 5.2f)),
    ];

    private static SurfaceDefinition Barrier(string id, Vector3 center, Vector3 half) =>
        new(id, new(center, half, Quaternion.Identity), isBarrier: true);
}
