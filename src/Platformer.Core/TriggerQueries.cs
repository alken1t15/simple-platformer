using System.Numerics;

namespace Platformer.Core;

/// <summary>Статические триггеры включают касание; физический Sweep блокирует только входящее движение.</summary>
internal static class TriggerQueries
{
    internal static bool Coin(IReadOnlyList<MotionSegment> path, CoinDefinition coin, float ballRadius)
    {
        double radius = (double)ballRadius + coin.Radius;
        return path.Any(segment => PointDistanceSquared(segment.Start, segment.End, coin.Center) <= radius * radius);
    }

    internal static bool Box(IReadOnlyList<MotionSegment> path, OrientedBox box, float radius) =>
        path.Any(segment => SegmentBox(box.ToLocal(segment.Start), box.ToLocal(segment.End), box.HalfExtents, radius));

    private static double PointDistanceSquared(Vector3 start, Vector3 end, Vector3 point)
    {
        double x = (double)end.X - start.X, y = (double)end.Y - start.Y, z = (double)end.Z - start.Z;
        double px = (double)start.X - point.X, py = (double)start.Y - point.Y, pz = (double)start.Z - point.Z;
        double length = x * x + y * y + z * z;
        double t = length == 0d ? 0d : Math.Clamp(-(px * x + py * y + pz * z) / length, 0d, 1d);
        return Math.Pow(px + t * x, 2) + Math.Pow(py + t * y, 2) + Math.Pow(pz + t * z, 2);
    }

    private static bool SegmentBox(Vector3 start, Vector3 end, Vector3 extents, float radius)
    {
        Span<double> cuts = stackalloc double[8];
        Span<double> delta = stackalloc double[3];
        int count = 2;
        cuts[0] = 0d;
        cuts[1] = 1d;
        for (int axis = 0; axis < 3; axis++)
        {
            delta[axis] = (double)end[axis] - start[axis];
            if (delta[axis] == 0d) continue;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                double crossing = (sign * (double)extents[axis] - start[axis]) / delta[axis];
                if (crossing > 0d && crossing < 1d) cuts[count++] = crossing;
            }
        }
        cuts[..count].Sort();
        // На каждом интервале squared distance до AABB — квадратичная функция.
        // Проверка её минимума учитывает рёбра/углы, не расширяет OBB до ложного AABB.
        for (int interval = 0; interval < count - 1; interval++)
        {
            double lo = cuts[interval], hi = cuts[interval + 1], middle = (lo + hi) / 2d;
            double a = 0d, b = 0d;
            for (int axis = 0; axis < 3; axis++)
            {
                double at = start[axis] + delta[axis] * middle;
                if (Math.Abs(at) <= extents[axis]) continue;
                double offset = start[axis] - Math.CopySign(extents[axis], at);
                a += delta[axis] * delta[axis];
                b += offset * delta[axis];
            }
            double t = a == 0d ? lo : Math.Clamp(-b / a, lo, hi);
            double squared = 0d;
            for (int axis = 0; axis < 3; axis++)
            {
                double outside = Math.Max(0d, Math.Abs(start[axis] + delta[axis] * t) - extents[axis]);
                squared += outside * outside;
            }
            if (squared <= (double)radius * radius) return true;
        }
        return false;
    }
}
