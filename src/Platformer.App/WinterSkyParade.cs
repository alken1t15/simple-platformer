using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

internal sealed partial class VisualScene
{
    private static readonly MeshData ParadeRound = GameMeshes.CreateSphere(16, 10);
    private static readonly MeshData ParadeStar = CreateParadeStar();
    private readonly List<(SceneObject Object, Vector3 Local, Quaternion Rotation, int Deer, bool Head)> _paradeParts = [];
    private readonly List<(SceneObject Upper, SceneObject Lower, SceneObject Hoof, Vector3 Hip, int Deer, int Leg, int Flank)> _paradeLegs = [];
    private readonly Vector3[] _deerOrigins = new Vector3[4];
    private readonly List<(SceneObject Object, MaterialData Material, int Index)> _paradeSparks = [];
    private Vector2 _paradeCenter;
    private readonly double _paradeStartSeconds = Random.Shared.NextDouble() * 48;

    private void CreateWinterParade(Vector2 middle)
    {
        _paradeCenter = middle;
        var red = Solid(183, 34, 58);
        var gold = Solid(250, 192, 79);
        var cream = Solid(255, 242, 217);
        var brown = Solid(127, 73, 49);
        var leather = Solid(61, 39, 39);
        var skin = Solid(246, 185, 145);
        var dark = Solid(24, 36, 47);
        int currentDeer = -1;
        bool headPart = false;

        // Упряжка смотрит вдоль локальной +X. Все детали движутся одной позой.
        Part(SoftBox, red, new(-2.8f, 0.1f, 0), new(2.8f, 0.5f, 1.8f));
        Part(SoftBox, red, new(-4, 0.75f, 0), new(0.25f, 1.4f, 1.8f));
        Part(SoftBox, leather, new(-2.8f, 0.5f, 0), new(2.1f, 0.25f, 1.3f));
        foreach (int side in new[] { -1, 1 })
        {
            Part(SoftBox, red, new(-2.9f, 0.53f, side * 0.85f), new(2.5f, 0.65f, 0.16f));
            Part(ParadeStar, gold, new(-2.9f, 0.53f, side * 0.95f), new(0.28f));
            Segment(new(-4, 0.88f, side * 0.88f), new(-1.6f, 0.88f, side * 0.88f), 0.065f, gold);
            Segment(new(-4.2f, -0.6f, side * 0.69f), new(-1.4f, -0.6f, side * 0.69f), 0.11f, gold);
            Segment(new(-1.4f, -0.6f, side * 0.69f), new(-0.95f, -0.25f, side * 0.69f), 0.11f, gold);
            Segment(new(-0.95f, -0.25f, side * 0.69f), new(-0.99f, 0.05f, side * 0.69f), 0.11f, gold);
            foreach (float x in new[] { -3.7f, -1.9f })
                Segment(new(x, -0.5f, side * 0.69f), new(x, 0, side * 0.69f), 0.085f, gold);
        }
        // Мешок подарков и сидящий Санта: мех, ремень, борода и загнутая шапка.
        Part(ParadeRound, Solid(47, 116, 102), new(-3.65f, 1.07f, 0), new(0.55f, 0.7f, 0.65f));
        Part(ParadeRound, red, new(-2.7f, 1.12f, 0), new(0.62f, 0.73f, 0.57f));
        Part(SoftBox, leather, new(-2.7f, 0.97f, 0), new(1.15f, 0.16f, 1.02f));
        Part(SoftBox, gold, new(-2.1f, 0.99f, 0), new(0.08f, 0.24f, 0.25f));
        Part(ParadeRound, skin, new(-2.55f, 2, 0), new(0.44f, 0.48f, 0.43f));
        Part(ParadeRound, cream, new(-2.26f, 1.76f, 0), new(0.32f, 0.43f, 0.4f));
        Part(ParadeRound, skin, new(-2.09f, 2.04f, 0), new(0.12f));
        foreach (int side in new[] { -1, 1 })
        {
            Part(ParadeRound, dark, new(-2.17f, 2.16f, side * 0.23f), new(0.04f));
            Segment(new(-2.6f, 1.45f, side * 0.43f), new(-1.9f, 1.3f, side * 0.59f), 0.23f, red);
            Part(ParadeRound, cream, new(-1.89f, 1.3f, side * 0.59f), new(0.16f));
            Part(SoftBox, dark, new(-1.95f, 0.55f, side * 0.32f), new(0.6f, 0.3f, 0.33f));
            Segment(new(-1.84f, 1.3f, side * 0.59f), new(2.4f, 0.1f, side * 0.85f), 0.022f, leather);
        }
        Part(ParadeRound, cream, new(-2.55f, 2.36f, 0), new(0.47f, 0.11f, 0.47f));
        Part(Cone, red, new(-2.55f, 2.36f, 0), new(0.65f, 0.73f, 0.65f));
        Part(ParadeRound, red, new(-2.37f, 2.91f, 0), new(0.27f, 0.18f, 0.23f));
        Part(ParadeRound, cream, new(-2.09f, 2.91f, 0), new(0.16f));

        for (int row = 0; row < 2; row++)
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 origin = new(1.1f + row * 3.4f, 0.25f, side * 0.95f);
            currentDeer = row * 2 + (side + 1) / 2;
            _deerOrigins[currentDeer] = origin;
            Part(ParadeRound, brown, origin, new(1, 0.52f, 0.36f));
            Part(ParadeRound, brown, origin + new Vector3(0.6f, 0.55f, 0), new(0.34f, 0.7f, 0.3f));
            headPart = true;
            Part(ParadeRound, brown, origin + new Vector3(0.83f, 1.02f, 0), new(0.43f, 0.33f, 0.29f));
            Part(ParadeRound, cream, origin + new Vector3(1.15f, 0.91f, 0), new(0.27f, 0.2f, 0.25f));
            Part(ParadeRound, row == 1 ? red : leather, origin + new Vector3(1.38f, 0.98f, 0), new(0.105f));
            foreach (int flank in new[] { -1, 1 })
            {
                Part(ParadeRound, dark, origin + new Vector3(0.98f, 1.14f, flank * 0.27f), new(0.047f));
                Part(ParadeRound, brown, origin + new Vector3(0.5f, 1.32f, flank * 0.35f), new(0.22f, 0.1f, 0.12f));
                Vector3 root = origin + new Vector3(0.62f, 1.25f, flank * 0.2f);
                Segment(root, root + new Vector3(-0.2f, 0.74f, flank * 0.23f), 0.075f, cream);
                Segment(root + new Vector3(-0.12f, 0.4f, flank * 0.13f), root + new Vector3(0.12f, 0.66f, flank * 0.29f), 0.055f, cream);
                Segment(root + new Vector3(-0.19f, 0.64f, flank * 0.2f), root + new Vector3(-0.43f, 0.79f, flank * 0.24f), 0.055f, cream);
            }
            headPart = false;
            foreach (int flank in new[] { -1, 1 })
            foreach (int leg in new[] { -1, 1 })
            {
                Vector3 hip = origin + new Vector3(leg * 0.57f, -0.22f, flank * 0.24f);
                var upper = Add(SoftBox, brown, hip, new(0.14f, 0.42f, 0.14f));
                var lower = Add(SoftBox, brown, hip, new(0.11f, 0.42f, 0.11f));
                var hoof = Add(SoftBox, leather, hip, new(0.24f, 0.16f, 0.2f));
                _paradeLegs.Add((upper, lower, hoof, hip, currentDeer, leg, flank));
            }
            Part(ParadeRound, cream, origin + new Vector3(-0.96f, 0.1f, 0), new(0.28f, 0.13f, 0.16f));
            Segment(origin + new Vector3(0.35f, -0.1f, -0.39f), origin + new Vector3(0.35f, 0.5f, -0.23f), 0.07f, red);
            Segment(origin + new Vector3(0.35f, -0.1f, 0.39f), origin + new Vector3(0.35f, 0.5f, 0.23f), 0.07f, red);
            Part(ParadeRound, gold, origin + new Vector3(0.4f, -0.15f, 0.37f), new(0.1f));
            currentDeer = -1; // Соединительные постромки следуют саням, не кивают вместе с телом.
            if (row == 0)
                Segment(origin + new Vector3(0.35f, 0.2f, 0), origin + new Vector3(3.7f, 0.2f, 0), 0.025f, leather);
        }
        for (int i = 0; i < 30; i++)
        {
            var material = i % 3 == 0 ? Solid(131, 238, 223) : Solid(255, 225, 149);
            var spark = Add(ParadeStar, material, Vector3.Zero, new(0.1f));
            _paradeSparks.Add((spark, material, i));
        }
        AnimateWinterParade(0);

        void Part(MeshData mesh, MaterialData material, Vector3 local, Vector3 scale)
        {
            var item = Add(mesh, material, local, scale);
            _paradeParts.Add((item, local, Quaternion.Identity, currentDeer, headPart));
        }
        void Segment(Vector3 from, Vector3 to, float width, MaterialData material)
        {
            Vector3 direction = Vector3.Normalize(to - from);
            var item = Add(SoftBox, material, (from + to) / 2, new(width, Vector3.Distance(from, to), width));
            float dot = Math.Clamp(Vector3.Dot(Vector3.UnitY, direction), -1, 1);
            var rotation = dot > 0.99999f ? Quaternion.Identity : dot < -0.99999f
                ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI)
                : Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction)), MathF.Acos(dot));
            _paradeParts.Add((item, (from + to) / 2, rotation, currentDeer, headPart));
        }
    }

    private (Vector3 Position, Quaternion Rotation) ParadePose(double seconds)
    {
        float phase = (float)(Math.IEEERemainder(seconds + _paradeStartSeconds, 48) * Math.Tau / 48);
        // За 48с плавный круг без телепорта. Нижняя точка выше всех игровых платформ.
        Vector3 position = new(_paradeCenter.X + 24 * MathF.Sin(phase),
            18 + 1.1f * MathF.Sin(phase * 2), _paradeCenter.Y - 24 + 16 * MathF.Cos(phase));
        float yaw = MathF.Atan2(16 * MathF.Sin(phase), 24 * MathF.Cos(phase));
        return (position, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));
    }

    private static MeshData CreateParadeStar()
    {
        var mesh = new MeshBuilder();
        for (int i = 0; i < 8; i++)
        {
            Vector3 Point(int index, float z)
            {
                float angle = index * MathF.Tau / 8;
                float radius = index % 2 == 0 ? 1 : 0.28f;
                return new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, z);
            }
            var a = Point(i, 0.05f); var b = Point(i + 1, 0.05f);
            var c = Point(i, -0.05f); var d = Point(i + 1, -0.05f);
            mesh.Triangle(new(0, 0, 0.05f), a, b);
            mesh.Triangle(new(0, 0, -0.05f), d, c);
            mesh.Triangle(a, c, d); mesh.Triangle(a, d, b);
        }
        return mesh.Build();
    }

    private void AnimateWinterParade(double seconds)
    {
        // Общий период предотвращает накопление ошибки и разницу % на границе
        // 48-секундного полёта / 1.2-секундного шага / 1.8-секундных искр.
        double cycleSeconds = seconds % 144;
        var pose = ParadePose(cycleSeconds);
        foreach (var part in _paradeParts)
        {
            var local = part.Local;
            var rotation = part.Rotation;
            if (part.Deer >= 0)
            {
                var body = DeerPose(part.Deer, cycleSeconds);
                if (part.Head)
                {
                    Vector3 neck = _deerOrigins[part.Deer] + new Vector3(0.6f, 0.65f, 0);
                    var nod = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.Sin(body.Phase + 0.6f) * 0.12f);
                    local = neck + Vector3.Transform(local - neck, nod);
                    rotation = nod * rotation;
                }
                local = _deerOrigins[part.Deer] + body.Bob + Vector3.Transform(local - _deerOrigins[part.Deer], body.Rotation);
                rotation = body.Rotation * rotation;
            }
            part.Object.Transform.Position = pose.Position + Vector3.Transform(local, pose.Rotation);
            part.Object.Transform.Rotation = Quaternion.Normalize(pose.Rotation * rotation);
        }
        foreach (var leg in _paradeLegs)
        {
            var body = DeerPose(leg.Deer, cycleSeconds);
            float phase = body.Phase + (leg.Leg == leg.Flank ? 0 : MathF.PI) + (leg.Leg < 0 ? 0.45f : 0);
            float swing = MathF.Sin(phase) * 0.65f;
            float bend = 0.55f + 0.4f * MathF.Sin(phase + 1);
            var knee = leg.Hip + Vector3.Transform(new(0, -0.42f, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, swing));
            var ankle = knee + Vector3.Transform(new(0, -0.42f, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, swing - bend));
            Vector3 World(Vector3 local) => pose.Position + Vector3.Transform(_deerOrigins[leg.Deer] + body.Bob
                + Vector3.Transform(local - _deerOrigins[leg.Deer], body.Rotation), pose.Rotation);
            Vector3 hipWorld = World(leg.Hip), kneeWorld = World(knee), ankleWorld = World(ankle);
            SetLimb(leg.Upper, hipWorld, kneeWorld);
            SetLimb(leg.Lower, kneeWorld, ankleWorld);
            leg.Hoof.Transform.Position = World(ankle + new Vector3(0.04f, -0.045f, 0));
            leg.Hoof.Transform.Rotation = Quaternion.Normalize(pose.Rotation * body.Rotation
                * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, swing * 0.3f));
        }
        foreach (var (spark, material, index) in _paradeSparks)
        {
            float age = (float)((cycleSeconds + index * 0.061) % 1.8);
            var previous = ParadePose(cycleSeconds - age);
            var offset = new Vector3(-4.4f, -0.3f + MathF.Sin(index * 2.4f) * age * 0.3f, MathF.Cos(index * 1.7f) * age * 0.38f);
            spark.Transform.Position = previous.Position + Vector3.Transform(offset, previous.Rotation);
            spark.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(age * 2, age * 3, index);
            spark.Transform.Scale = new(_preferences?.ReducedEffects==true?0.001f:0.02f + 0.1f * MathF.Sin(age * MathF.PI / 1.8f));
            float pulse = _preferences?.FlashingEnabled==false?0.8f:0.55f + 0.45f * MathF.Sin(age * MathF.PI / 1.8f);
            material.BaseColor = index % 3 == 0 ? new(0.51f * pulse, 0.93f * pulse, 0.87f * pulse) : new(pulse, 0.88f * pulse, 0.58f * pulse);
        }
    }

    private static (Vector3 Bob, Quaternion Rotation, float Phase) DeerPose(int index, double seconds)
    {
        float phase = (float)(Math.IEEERemainder(seconds, 1.2) * Math.Tau / 1.2) + index * 0.83f;
        return (new(0, 0.09f * MathF.Sin(phase * 2), 0),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.055f * MathF.Sin(phase)), phase);
    }

    private static void SetLimb(SceneObject item, Vector3 from, Vector3 to)
    {
        var direction = Vector3.Normalize(to - from);
        float dot = Math.Clamp(Vector3.Dot(Vector3.UnitY, direction), -1, 1);
        item.Transform.Position = (from + to) / 2;
        item.Transform.Rotation = dot > 0.99999f ? Quaternion.Identity : dot < -0.99999f
            ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI)
            : Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction)), MathF.Acos(dot));
    }
}
