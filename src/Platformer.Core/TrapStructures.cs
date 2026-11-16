using System.Numerics;
namespace Platformer.Core;

/// <summary>Неподвижные части механизмов: одни OBB для физики и отрисовки.</summary>
public static class TrapStructures
{
    public static LevelDefinition Attach(LevelDefinition level, bool winter)
    {
        ArgumentNullException.ThrowIfNull(level);
        var parts = new List<SurfaceDefinition>();
        foreach (var hazard in level.Hazards)
        {
            string prefix = "structure:" + hazard.Id + ":";
            void Part(string name, Vector3 center, Vector3 size, Quaternion? rotation = null) =>
                parts.Add(new(prefix + name, new(center, size / 2, rotation ?? Quaternion.Identity), isBarrier: true));
            if (winter && hazard.MotionKind == HazardMotionKind.Shuttle && MathF.Abs(hazard.Axis.Y) > 0.9f)
            {
                float top = hazard.Pivot.Y + hazard.Amplitude + hazard.HalfExtents.Y;
                float floor = hazard.Pivot.Y - 2.5f, bottom = floor - 0.22f;
                Part("base",new(hazard.Pivot.X,floor-0.18f,hazard.Pivot.Z),new(1.8f,0.32f,hazard.HalfExtents.Z*2+0.6f));
                Part("head",new(hazard.Pivot.X,top+0.225f,hazard.Pivot.Z),new(hazard.HalfExtents.X*2+0.4f,0.45f,hazard.HalfExtents.Z*2+0.28f));
                foreach (int side in new[]{-1,1})
                    Part("guide"+side,new(hazard.Pivot.X,(top+bottom)/2,hazard.Pivot.Z+(hazard.HalfExtents.Z+0.07f)*side),new(0.22f,top-bottom,0.14f));
            }
            else if (!winter && hazard.MotionKind == HazardMotionKind.Shuttle)
            {
                float dot = Math.Clamp(Vector3.Dot(Vector3.UnitY,hazard.Axis),-1,1);
                Quaternion rotation = dot > 0.99999f ? Quaternion.Identity : dot < -0.99999f
                    ? Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI)
                    : Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY,hazard.Axis)),MathF.Acos(dot));
                Part("rail",hazard.Pivot-Vector3.UnitY*0.9f,new(0.16f,(hazard.Amplitude+1.3f)*2,0.16f),rotation);
                foreach (int side in new[]{-1,1})
                    Part("stop"+side,hazard.Pivot-Vector3.UnitY*0.22f+hazard.Axis*((hazard.Amplitude+1.4f)*side),new(0.9f,1.2f,0.6f));
            }
            else if (hazard.MotionKind == HazardMotionKind.Rotation)
                Part("axle",hazard.Pivot-Vector3.UnitY*0.25f,new(0.12f,0.5f,0.12f));
        }
        return new(level.Id,level.Spawn,level.KillY,level.Finish,level.Surfaces.Concat(parts).ToArray(),level.Coins,level.Hazards,level.Powerups,level.Springs,level.Winds);
    }
}
