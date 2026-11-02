using System.Numerics;

namespace Platformer.Core;

internal static class LevelValidation
{
    internal static string Id(string value, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ID не должен быть пустым", parameter);
        return value;
    }

    internal static void Finite(float value, string parameter)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameter, "Требуется конечное число");
    }

    internal static void Finite(Vector3 value, string parameter)
    {
        Finite(value.X, parameter);
        Finite(value.Y, parameter);
        Finite(value.Z, parameter);
    }

    internal static void Coordinate(float value, string parameter)
    {
        Finite(value, parameter);
        if (MathF.Abs(value) > 10000f)
            throw new ArgumentOutOfRangeException(parameter, "Модуль координаты должен быть ≤10000");
    }

    internal static void Position(Vector3 value, string parameter)
    {
        Coordinate(value.X, parameter);
        Coordinate(value.Y, parameter);
        Coordinate(value.Z, parameter);
    }

    internal static void Size(float value, string parameter)
    {
        Finite(value, parameter);
        if (value < 0.001f || value > 10000f)
            throw new ArgumentOutOfRangeException(parameter, "Размер должен быть в [0.001, 10000]");
    }

    internal static void Extents(Vector3 value, string parameter)
    {
        Size(value.X, parameter);
        Size(value.Y, parameter);
        Size(value.Z, parameter);
    }

    // Double не даёт переполниться промежуточным квадратам float-компонент
    private static double Norm(double squared, string parameter)
    {
        double length = Math.Sqrt(squared);
        if (!double.IsFinite(length) || length < float.Epsilon || length > float.MaxValue)
            throw new ArgumentOutOfRangeException(parameter, "Норма должна быть ненулевой и представимой");
        return length;
    }

    internal static Quaternion Rotation(Quaternion value, string parameter)
    {
        double norm = Norm((double)value.X * value.X + (double)value.Y * value.Y
            + (double)value.Z * value.Z + (double)value.W * value.W, parameter);
        return new Quaternion((float)(value.X / norm), (float)(value.Y / norm),
            (float)(value.Z / norm), (float)(value.W / norm));
    }

    internal static Vector3 Direction(Vector3 value, string parameter)
    {
        double norm = Norm((double)value.X * value.X + (double)value.Y * value.Y
            + (double)value.Z * value.Z, parameter);
        return new Vector3((float)(value.X / norm), (float)(value.Y / norm), (float)(value.Z / norm));
    }
}
