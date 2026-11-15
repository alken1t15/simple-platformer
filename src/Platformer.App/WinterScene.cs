using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

internal sealed partial class VisualScene
{
    public static MaterialData SnowBoulderMaterial { get; } = Textured(Texture(128, 128, (x,y) =>
        Vector3.Lerp(new(146,192,211),new(240,249,252),0.55f+0.25f*MathF.Sin(x*0.18f+MathF.Sin(y*0.13f))+PropNoise(x,y,113)*0.15f)));
    public static MaterialData PressSteel { get; } = Solid(99, 135, 151);
    public static MaterialData SpikeSteel { get; } = Textured(Texture(64, 64, (x, y) =>
        new Vector3(173, 190, 202) * (0.88f + 0.08f * MathF.Sin(y * 1.7f) + 0.04f * PropNoise(x, y, 97))));
    public static MaterialData WinterSnow { get; } = Textured(Texture(128, 64, (x, y) =>
    {
        float grain = PropNoise(x, y, 61);
        bool edge = y < 3 || y > 60;
        float drift = MathF.Sin(x * 0.04f + MathF.Sin(y * 0.15f)) * 0.04f;
        return edge ? new(110, 196, 223) : new Vector3(224, 242, 249) * (0.94f + grain * 0.055f + drift);
    }));
    public static MaterialData WinterIce { get; } = Textured(Texture(128, 128, (x, y) =>
    {
        float vein = MathF.Abs(MathF.Sin(x * 0.071f + MathF.Sin(y * 0.1f) * 1.8f));
        return vein < 0.045f ? new(228, 253, 255) : new Vector3(88, 179, 215) * (0.86f + PropNoise(x / 3, y / 3, 67) * 0.2f);
    }));
    private static readonly MaterialData WinterCrackedIce = CrackedTiles(new(176, 235, 250), new(116, 198, 231));
    private static readonly MaterialData WinterRock = Textured(Texture(64, 128, (x, y) =>
        Vector3.Lerp(new(171, 209, 230), new(44, 79, 119), y / 127f) * (0.9f + PropNoise(x, y / 4, 71) * 0.1f)));
    private static readonly MaterialData WinterPine = Solid(28, 90, 104);
    private static readonly MaterialData WinterTrunk = Solid(83, 67, 69);
    private static readonly MaterialData WinterRoof = Solid(71, 103, 131);
    private static readonly MaterialData WinterGlow = Solid(255, 209, 112);
    private static readonly (MeshData Rock, MeshData Snow) WinterRidge = WinterMountain.Create();
    private static readonly MeshData LodgeGables = new(
        [new(new(-1.2f, 1.65f, 0.95f), new(0, 0), Vector3.UnitZ),
         new(new(1.2f, 1.65f, 0.95f), new(1, 0), Vector3.UnitZ),
         new(new(0, 2.31f, 0.95f), new(0.5f, 1), Vector3.UnitZ),
         new(new(1.2f, 1.65f, -0.95f), new(0, 0), -Vector3.UnitZ),
         new(new(-1.2f, 1.65f, -0.95f), new(1, 0), -Vector3.UnitZ),
         new(new(0, 2.31f, -0.95f), new(0.5f, 1), -Vector3.UnitZ)], [0, 1, 2, 3, 4, 5]);
    private static readonly MaterialData WinterSky = Textured(Texture(8, 256, (_, y) =>
        Vector3.Lerp(new(19, 35, 75), new(160, 208, 230), y / 255f)));
    private static readonly MaterialData Aurora = Textured(Texture(128, 64, (x, y) =>
    {
        float ribbons = 0.5f + 0.5f * MathF.Sin(x * 0.11f + MathF.Sin(y * 0.12f) * 2);
        return Vector3.Lerp(new(88, 106, 204), new(94, 239, 203), ribbons);
    }));
    private static readonly Lazy<TextureData[]> WinterContactFrames = new(() => VisualContactShadow.CreateFrames(WinterSnow.Texture!));
    private readonly List<(SceneObject Object, Vector3 Origin)> _snowflakes = [];
    private readonly List<(MaterialData Material, ColorRgba Color)> _bulbs = [];

    private void CreateWinterLandscape(Vector2 middle)
    {
        _scene.Lighting.AmbientColor = new(0.64f, 0.73f, 0.87f);
        _scene.Lighting.DirectionalColor = new(0.39f, 0.43f, 0.52f);
        Add(Sky, WinterSky, new(middle.X, 4, middle.Y), new(105));
        Add(Cloud, Solid(239, 248, 255), new(middle.X - 30, 27, middle.Y - 43), new(3.4f));
        // Отдельные заснеженные островки не пересекают трассу или ход механизмов.
        foreach (var offset in new[] { new Vector2(-9, -4), new(8, -9), new(17, 9), new(-6, 16), new(6, 23), new(28, 22) })
        {
            var requested = new Vector3(middle.X + offset.X, -1.6f, middle.Y + offset.Y);
            if (!TryDecorationSite(requested, 4.2f, 4.2f, 20, out var site)) continue;
            Add(Island, WinterRock, site, new(3.8f, 4, 3.8f));
            Add(Grass, WinterSnow, site + Vector3.UnitY * 0.035f, new(3.8f, 1, 3.8f));
            Pine(site + new Vector3(-1.5f, 0, 0), 2.8f);

            Pine(site + new Vector3(1.4f, 0, -1.1f), 3.6f);
            SnowLodge(site + new Vector3(0.3f, 0.04f, 1.1f), 0.65f);
        }
        for (int i = 0; i < 7; i++)
        {
            float angle = i * MathF.Tau / 7;
            Vector3 foot = new(middle.X + MathF.Cos(angle) * 73, -29, middle.Y + MathF.Sin(angle) * 73);
            Vector3 scale = new(28 + i % 3 * 3, 35 + i % 3 * 3, 20);
            Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -angle + MathF.PI / 2);
            Add(WinterRidge.Rock, WinterRock, foot, scale).Transform.Rotation = rotation;
            Add(WinterRidge.Snow, WinterSnow, foot, scale).Transform.Rotation = rotation;
        }
        // Непрозрачные ленты северного сияния далеко за трассой, без alpha-blend артефактов.
        for (int i = 0; i < 8; i++)
        {
            var ribbon = Add(SoftBox, Aurora, new(middle.X - 23 + i * 6, 23 + MathF.Sin(i * 0.8f) * 4, middle.Y - 45), new(7, 0.6f, 0.3f));
            ribbon.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.Cos(i * 0.8f) * 0.5f);
        }
        for (int i = 0; i < 72; i++)
        {
            float u = (i * 0.618034f) % 1, v = (i * 0.414214f) % 1;
            Vector3 origin = new(_routeMin.X - 5 + u * (_routeMax.X - _routeMin.X + 10),
                2 + i % 11, _routeMin.Y - 5 + v * (_routeMax.Y - _routeMin.Y + 10));
            var flake = Add(SoftBox, WinterSnow, origin, new(0.055f));
            _snowflakes.Add((flake, origin));
        }
        CloudAt(new(middle.X - 23, 12, middle.Y - 20), 3);
        CloudAt(new(middle.X + 27, 15, middle.Y + 22), 4);

        void Pine(Vector3 foot, float height)
        {
            Add(GameMeshes.Box, WinterTrunk, foot + Vector3.UnitY * height * 0.3f, new(0.23f, height * 0.6f, 0.23f));
            for (int tier = 0; tier < 3; tier++)
            {
                float size = (1.35f - tier * 0.3f) * height / 3;
                Vector3 position = foot + Vector3.UnitY * (height * (0.2f + tier * 0.23f));
                Add(Cone, WinterPine, position, new(size, height * 0.55f, size));
                Add(Cone, WinterSnow, position + Vector3.UnitY * height * 0.15f, new(size * 0.8f, height * 0.4f, size * 0.8f));
                for (int bulb=0;bulb<4;bulb++)
                {
                    float angle=bulb*MathF.Tau/4+tier;
                    Add(Cloud,bulb%2==0?Solid(235,72,83):WinterGlow,position+new Vector3(MathF.Cos(angle)*size*0.48f,height*0.13f,MathF.Sin(angle)*size*0.48f),new(0.09f));
                }
            }
        }
    }

    private void AnimateSnow(double seconds)
    {
        for (int j=0;j<_bulbs.Count;j++)
        {
            var bulb=_bulbs[j]; float intensity=0.45f+0.55f*(0.5f+0.5f*MathF.Sin((float)(seconds%20)*2.4f+j*0.8f));
            bulb.Material.BaseColor=new(bulb.Color.R*intensity,bulb.Color.G*intensity,bulb.Color.B*intensity);
        }
        for (int i = 0; i < _snowflakes.Count; i++)
        {
            var (flake, origin) = _snowflakes[i];
            float phase = (float)(seconds % 20);
            flake.Transform.Position = origin + new Vector3(MathF.Sin(phase * 0.6f + i) * 0.45f,
                -((phase * (0.55f + i % 3 * 0.12f) + i * 0.31f) % 10), 0);
        }
    }

    private void WinterFinishDetail(Platformer.Core.OrientedBox bounds)
    {
        // Цельный снежный остров с домиком, а не башня с отдельно висящим основанием.
        Vector3 requested = bounds.Center + Vector3.Transform(new Vector3(7, -0.75f, -5), bounds.Orientation);
        if (!TryDecorationSite(requested, 3, 3, 20, out var foot)) return;
        Add(Island, WinterRock, foot, new(2.5f, 3.2f, 2.5f));
        Add(Grass, WinterSnow, foot, new(2.5f, 1, 2.5f));
        SnowLodge(foot + Vector3.UnitY * 0.04f, 1);
    }

    private void Garland(Vector3 from, Vector3 to)
    {
        foreach (var anchor in new[]{from,to}) Add(SoftBox,WinterRoof,anchor,new(0.12f));
        for (int i = 0; i < 12; i++)
        {
            float t = i / 11f;
            var point = Vector3.Lerp(from,to,t) - Vector3.UnitY * MathF.Sin(t*MathF.PI)*0.3f;
            Add(SoftBox, WinterTrunk, point, new(0.37f,0.025f,0.025f));
            var color = i % 3 == 0 ? Solid(244,86,83) : i % 3 == 1 ? Solid(255,219,114) : Solid(68,222,170);
            _bulbs.Add((color,color.BaseColor));
            Add(Cloud,color,point-Vector3.UnitY*0.07f,new(0.09f,0.12f,0.09f));
            var core=Solid(255,244,194);
            _bulbs.Add((core,core.BaseColor));
            Add(Cloud,core,point-Vector3.UnitY*0.07f+new Vector3(0,0,0.07f),new(0.035f));
        }
    }

    private void SnowLodge(Vector3 foot, float scale)
    {
        Add(SoftBox, WinterTrunk, foot + Vector3.UnitY * (0.825f * scale), new Vector3(2.4f, 1.65f, 1.9f) * scale);
        Add(LodgeGables, WinterTrunk, foot, new(scale));
        Garland(foot+new Vector3(-1.2f,1.62f,0.99f)*scale,foot+new Vector3(1.2f,1.62f,0.99f)*scale);
        // Двускатная крыша: два настоящих ската, снег лежит непосредственно на доме.
        foreach (int side in new[] { -1, 1 })
        {
            var roof = Add(SoftBox, WinterSnow, foot + new Vector3(side * 0.62f, 1.95f, 0) * scale,
                new Vector3(1.55f, 0.2f, 2.3f) * scale);
            roof.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -side * 0.52f);
            Add(GameMeshes.Box, WinterRoof, foot + new Vector3(side * 0.7f, 0.95f, 0.956f) * scale,
                new Vector3(0.66f, 0.68f, 0.04f) * scale);
            Add(GameMeshes.Box, WinterGlow, foot + new Vector3(side * 0.7f, 0.95f, 0.98f) * scale,
                new Vector3(0.45f, 0.46f, 0.03f) * scale);
        }
        Add(SoftBox, WinterRoof, foot + new Vector3(0, 0.58f, 0.97f) * scale, new Vector3(0.5f, 1.16f, 0.05f) * scale);
        Add(SoftBox, WinterRock, foot + new Vector3(0.8f, 2, -0.5f) * scale, new Vector3(0.3f, 1, 0.35f) * scale);
    }
}
