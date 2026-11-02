using System.Numerics;

namespace Platformer.Core;

internal readonly record struct SphereContact(Vector3 Normal, float Separation);

/// <summary>Sphere/OBB: signed distance и sweep вдоль отрезка, без зависимости от симулятора.</summary>
internal static class SphereGeometry
{
    internal static SphereContact Contact(OrientedBox box, Vector3 center, float radius)
    {
        Vector3 local = box.ToLocal(center);
        Vector3 offset = local - Vector3.Clamp(local, -box.HalfExtents, box.HalfExtents);
        double distance = Math.Sqrt((double)offset.X * offset.X + (double)offset.Y * offset.Y + (double)offset.Z * offset.Z);
        Vector3 normal;
        float separation;
        if (distance > 0d)
        {
            normal = new Vector3((float)(offset.X / distance), (float)(offset.Y / distance), (float)(offset.Z / distance));
            separation = (float)(distance - radius);
        }
        else
        {
            Vector3 gaps = box.HalfExtents - Vector3.Abs(local);
            // Строгое сравнение сохраняет X/Y/Z, при нулевой координате — положительную грань.
            int axis = gaps.Y < gaps.X ? 1 : 0;
            if (gaps.Z < gaps[axis]) axis = 2;
            normal = Vector3.Zero;
            normal[axis] = local[axis] < 0f ? -1f : 1f;
            separation = -radius - gaps[axis];
        }
        return new SphereContact(Vector3.Normalize(Vector3.Transform(normal, box.Orientation)), separation);
    }

    internal static bool Sweep(OrientedBox box, Vector3 start, Vector3 displacement, float radius,
        out double fraction, out Vector3 normal)
    {
        Vector3 origin = box.ToLocal(start);
        Vector3 direction = Vector3.Transform(displacement, Quaternion.Conjugate(box.Orientation));
        Span<double> cuts = stackalloc double[8];
        int count = 2;
        cuts[0] = 0d;
        cuts[1] = 1d;
        for (int axis = 0; axis < 3; axis++)
        {
            if (direction[axis] == 0f) continue;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                double crossing = (sign * (double)box.HalfExtents[axis] - origin[axis]) / direction[axis];
                if (crossing > 0d && crossing < 1d) cuts[count++] = crossing;
            }
        }
        cuts[..count].Sort();

        // Между пересечениями плоскостей AABB набор clamped координат постоянен.
        // squared distance до объёма — квадратичная функция: корень даёт точный
        // вход в грань, цилиндр ребра либо сферу вершины, без ложных AABB-углов.
        for (int interval = 0; interval < count - 1; interval++)
        {
            double lo = cuts[interval], hi = cuts[interval + 1];
            if (hi <= lo) continue;
            double middle = (lo + hi) / 2d;
            double a = 0d, b = 0d, c = -(double)radius * radius;
            for (int axis = 0; axis < 3; axis++)
            {
                double position = origin[axis] + direction[axis] * middle;
                double extent = box.HalfExtents[axis];
                if (position >= -extent && position <= extent) continue;
                double offset = origin[axis] + direction[axis] * lo - Math.CopySign(extent, position);
                double delta = direction[axis];
                a += delta * delta;
                b += 2d * offset * delta;
                c += offset * offset;
            }
            if (a == 0d || b >= 0d) continue;
            double candidate = 0d;
            if (c > 0d)
            {
                double discriminant = b * b - 4d * a * c;
                if (discriminant <= 0d) continue; // Касание без входящего движения не блокирует путь.
                candidate = 2d * c / (-b + Math.Sqrt(discriminant));
            }
            if (candidate > hi - lo) continue;
            double hit = lo + candidate;
            Vector3 localHit = origin + direction * (float)hit;
            Vector3 offsetAtHit = localHit - Vector3.Clamp(localHit, -box.HalfExtents, box.HalfExtents);
            if (offsetAtHit.LengthSquared() == 0f) continue;
            Vector3 worldNormal = Vector3.Normalize(Vector3.Transform(Vector3.Normalize(offsetAtHit), box.Orientation));
            if (Vector3.Dot(displacement, worldNormal) >= -1e-9f) continue;
            fraction = hit;
            normal = worldNormal;
            return true;
        }
        fraction = 0d;
        normal = Vector3.Zero;
        return false;
    }
}
