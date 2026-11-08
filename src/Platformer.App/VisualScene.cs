using System.Collections.Concurrent;
using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Локальное оформление --visual: только меши, материалы и декорации App.</summary>
internal sealed partial class VisualScene
{
    private readonly Scene _scene;
    private readonly VisualContactShadow _contactShadow;
    private readonly VisualRoadLayout _roads;
    private readonly Vector2 _routeMin;
    private readonly Vector2 _routeMax;
    private static readonly ConcurrentDictionary<(MaterialData Source, int Amount), MaterialData> DistantMaterials = new();
    private static readonly MeshData Sky = InwardSphere();
    private static readonly MeshData Island = CreateIsland();
    private static readonly MeshData Grass = CreateDisc();
    private static readonly MeshData Trunk = CreateTrunk();
    private static readonly MeshData Frond = CreateFrond();
    private static readonly MeshData Roof = CreateRoof();
    private static readonly MeshData Cloud = GameMeshes.CreateSphere(28, 18);
    public static MeshData SoftBox { get; } = CreateSoftBox();
    public static MeshData Ball { get; } = GameMeshes.CreateSphere(48, 32);
    public static MeshData Coin { get; } = CreateCoin();
    public static MeshData RoadBody { get; } = CreateRoadBody();

    private static readonly MaterialData RoadTiles = Textured(Texture(128, 64, (x, y) =>
    {
        int cell = x / 64, local = x % 64;
        Vector3 color = cell == 0 ? new(253, 100, 118) : new(255, 247, 219);
        float bevel = Math.Min(Math.Min(local, 63 - local), Math.Min(y, 63 - y));
        float light = (local < 3 || y < 3) ? 1.035f : 1f;
        return color * light * (bevel < 1 ? 0.88f : 1f);
    }));
    private static readonly Lazy<TextureData[]> ContactFrames = new(() => VisualContactShadow.CreateFrames(RoadTiles.Texture!));
    public static MaterialData Track { get; } = Textured(Texture(8, 128, (_, y) =>
    {
        float t = 1 - y / 127f;
        if (t is > 0.15f and < 0.25f) return new(232, 112, 132);
        return Vector3.Lerp(new(207, 167, 151), new(255, 246, 218), MathF.Sqrt(t));
    }));
    public static MaterialData Gold { get; } = Textured(CoinTexture());
    public static MaterialData Danger { get; } = Solid(150, 70, 210);
    public static MaterialData StaticDanger { get; } = Textured(Texture(128, 64, (x, y) =>
        ((x + y) / 12 & 1) == 0 ? new(255, 79, 70) : new(64, 38, 58)));
    public static MaterialData Finish { get; } = Solid(25, 201, 173);
    private static readonly MaterialData Banner = Textured(Texture(64, 32, (x, y) =>
        ((x / 8 + y / 8) & 1) == 0 ? new(255, 250, 222) : new(36, 76, 96)));
    private static readonly MaterialData GrassMaterial = Textured(Texture(64, 64, (x, y) =>
    {
        float r = Vector2.Distance(new(x / 31.5f - 1, y / 31.5f - 1), Vector2.Zero);
        return Vector3.Lerp(new(139, 221, 73), new(72, 170, 61), Math.Clamp(r, 0, 1));
    }));
    private static readonly MaterialData Rock = Textured(Texture(128, 128, (x, y) =>
    {
        float t = y / 127f, wave = t * 7 + 0.12f * MathF.Sin(x * 0.055f) + 0.045f * MathF.Sin(x * 0.14f + t * 9);
        float strata = 0.96f + 0.04f * MathF.Sin(wave * MathF.Tau);
        Vector3 color = Vector3.Lerp(new(177, 146, 211), new(74, 100, 169), t);
        return color * strata;
    }));
    private static readonly MaterialData Bark = Textured(Texture(8, 64, (_, y) =>
        (y / 6 & 1) == 0 ? new(195, 132, 75) : new(160, 104, 65)));
    private static readonly MaterialData Leaves = Solid(77, 190, 76);
    private static readonly MaterialData YoungLeaves = Solid(154, 220, 70);
    private static readonly MaterialData Coconut = Solid(134, 96, 46);
    private static readonly MaterialData Stone = Solid(171, 166, 204);
    private static readonly MaterialData Sand = Solid(251, 220, 160);
    private static readonly MaterialData Timber = Solid(172, 112, 68);
    private static readonly MaterialData Window = Solid(37, 116, 143);
    private static readonly MaterialData WindowLight = Solid(161, 238, 236);
    private static readonly MaterialData Walls = Textured(Texture(64, 64, (x, y) =>
        new Vector3(255, 222, 160) * ((x % 16 == 0 || y % 32 == 0) ? 0.88f : 1f)));
    private static readonly MaterialData CoralRoof = RoofMaterial(new(247, 112, 98));
    private static readonly MaterialData TealRoof = RoofMaterial(new(47, 190, 174));
    private static readonly MaterialData CloudMaterial = Solid(253, 255, 245);
    private static readonly MaterialData BlueBall = Textured(SkinTextures.StarBall(new(58, 162, 255)));
    private static readonly MaterialData OrangeBall = Textured(SkinTextures.StarBall(new(255, 157, 48)));
    private static readonly Lazy<TextureData> BluePreview = new(() => SkinTextures.Preview(BlueBall));
    private static readonly Lazy<TextureData> OrangePreview = new(() => SkinTextures.Preview(OrangeBall));
    private static readonly MaterialData SkyMaterial = Textured(Texture(8, 256, (_, y) =>
    {
        float t = y / 255f;
        return t < 0.5f ? Vector3.Lerp(new(35, 164, 224), new(161, 237, 247), t * 2)
            : Vector3.Lerp(new(161, 237, 247), new(54, 162, 231), Math.Clamp((t - 0.5f) * 2.7f, 0, 1));
    }));

    // Тяжёлые CPU-ассеты создаются при первом включении режима.
    static VisualScene() { }

    public VisualScene(Scene scene, IReadOnlyList<SurfaceDefinition> surfaces)
    {
        _scene = scene;
        Vector2 minimum = new(float.PositiveInfinity), maximum = new(float.NegativeInfinity);
        foreach (var surface in surfaces)
        foreach (int x in new[] { -1, 1 })
        foreach (int y in new[] { -1, 1 })
        foreach (int z in new[] { -1, 1 })
        {
            Vector3 p = surface.Bounds.ToWorld(surface.Bounds.HalfExtents * new Vector3(x, y, z));
            minimum = Vector2.Min(minimum, new(p.X, p.Z));
            maximum = Vector2.Max(maximum, new(p.X, p.Z));
        }
        _routeMin = surfaces.Count == 0 ? new(-2) : minimum;
        _routeMax = surfaces.Count == 0 ? new(2) : maximum;
        _roads = VisualRoadLayout.Get(surfaces);
        _contactShadow = new(scene, _roads, ContactFrames.Value);
        ConfigureLighting(scene);
        Vector2 middle = (_routeMin + _routeMax) / 2;
        Add(Sky, SkyMaterial, new(middle.X, 4, middle.Y), new(105));
        Add(Cloud, Solid(255, 243, 164), new(-33, 31, -48), new(4.3f));

        Village(new(-8.5f, -1.8f, 1), 4.1f, 3.4f, 2, 0.3f, CoralRoof);
        Village(new(15.8f, -1.8f, 5.8f), 5.2f, 4.4f, 3, 1.2f, TealRoof);
        Village(new(4.6f, -2.4f, -7.5f), 4.3f, 3.6f, 2, 2.1f, TealRoof);
        Village(new(-14, -1.1f, 25), 6.4f, 5.2f, 3, 2.3f, TealRoof);
        Village(new(12, -2.3f, 32), 7.2f, 5.3f, 3, 0.8f, CoralRoof);
        Village(new(-21, -3.2f, -15), 7.1f, 5.4f, 2, 1.5f, null);
        Village(new(20, -3.4f, -15), 6.5f, 4.7f, 2, 2.8f, null);
        Village(new(37, -3.3f, 35), 8.4f, 6.2f, 3, 0.7f, null);
        Village(new(-35, -4, 44), 10, 6.8f, 2, 1.7f, null);
        AddLandmarks();

        CloudAt(new(-21, 12, -22), 3.3f);
        CloudAt(new(19, 15, -28), 3.8f);
        CloudAt(new(-30, 10, 22), 3.5f);
        CloudAt(new(30, 14, 34), 4f);
        CloudAt(new(-13, 18, 44), 4.4f);
        CloudAt(new(2, 23, -40), 3.2f);
        CloudAt(new(-39, 15, -5), 4.1f);
        CloudAt(new(-7, -7, 6), 3.8f);
        CloudAt(new(10, -8, -10), 4.5f);
        CloudAt(new(25, -7, 28), 5.8f);
        CloudAt(new(1, -10, 32), 5.2f);
        CloudAt(new(-22, -8, 26), 4.5f);
        CloudAt(new(16, 2.2f, -26), 3.1f);
        CloudAt(new(6, 4, -29), 2.7f);
    }

    public static void ConfigureLighting(Scene scene)
    {
        scene.Lighting.AmbientColor = new(0.58f, 0.64f, 0.72f);
        scene.Lighting.DirectionalColor = new(0.47f, 0.42f, 0.32f);
        scene.Lighting.Direction = new(-0.6f, -1f, -0.45f);
    }

    public static MaterialData BallMaterial(SkinDefinition skin) => skin.Id switch
    {
        "classic-blue" => BlueBall,
        "orange" => OrangeBall,
        _ => skin.Material,
    };

    public static TextureData BallPreview(SkinDefinition skin) => skin.Id switch
    {
        "classic-blue" => BluePreview.Value,
        "orange" => OrangePreview.Value,
        _ => skin.Preview,
    };

    public void UpdateContactShadow(BallState ball, double seconds, IReadOnlySet<int>? hidden = null)
    {
        _contactShadow.Apply(ball, hidden);
        AnimateLandmarks(seconds);
    }

    private static readonly MaterialData FragileTiles = CrackedTiles(new(255, 220, 132), new(240, 160, 82));
    public static MaterialData CrumbleWarningA { get; } = CrackedTiles(new(255, 184, 61), new(245, 112, 61));
    public static MaterialData CrumbleWarningB { get; } = CrackedTiles(new(255, 230, 115), new(255, 157, 71));

    private static MaterialData CrackedTiles(Vector3 a, Vector3 b) => Textured(Texture(128, 64, (x, y) =>
    {
        float seam = MathF.Abs((x % 64) - 31 + MathF.Sin(y * 0.22f) * 7);
        bool crack = seam < 1.5f || (y > 26 && y < 29 && x % 64 > 27);
        return crack ? new(116, 69, 44) : x < 64 ? a : b;
    }));

    public void SurfaceDetail(int index, OrientedBox bounds, bool fragile = false)
    {
        if (_roads.Patches[index].Top is { } top) Add(top, fragile ? FragileTiles : RoadTiles, Vector3.Zero, Vector3.One);
        if (!fragile && bounds.HalfExtents.X >= 1.9f && bounds.HalfExtents.Z >= 1.9f)
        {
            var stone = Add(Island, Rock, bounds.ToWorld(new(0, -bounds.HalfExtents.Y, 0)),
                new(bounds.HalfExtents.X * 0.92f, 1.8f, bounds.HalfExtents.Z * 0.92f));
            stone.Transform.Rotation = bounds.Orientation;
        }
    }

    public void FinishDetail(OrientedBox bounds)
    {
        var flag = Add(GameMeshes.Box, Banner, bounds.ToWorld(new(0, bounds.HalfExtents.Y + 0.24f, 0)),
            new(bounds.HalfExtents.X * 2 + 0.32f, 0.42f, 0.12f));
        flag.Transform.Rotation = bounds.Orientation;
        foreach (int side in new[] { -1, 1 })
            Add(Cloud, Solid(255, 196, 54), bounds.ToWorld(new(side * bounds.HalfExtents.X,
                bounds.HalfExtents.Y + 0.55f, 0)), new(0.13f));
    }

    public static void AnimateCoins(IReadOnlyDictionary<string, SceneObject> coins, double seconds)
    {
        int index = 0;
        foreach (var coin in coins.Values)
            coin.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,
                (float)Math.IEEERemainder(seconds * 1.5 + index++ * 0.31, Math.Tau));
    }

    private SceneObject Add(MeshData mesh, MaterialData material, Vector3 position, Vector3 scale)
    {
        var item = new SceneObject(mesh, material);
        item.Transform.Position = position;
        item.Transform.Scale = scale;
        _scene.Add(item);
        return item;
    }

    private void Village(Vector3 center, float rx, float rz, int palms, float phase, MaterialData? roof)
    {
        float plant = PlantScale(rx, rz);
        float ex = MathF.Max(rx * 1.08f, rx * 0.48f + 2.6f * plant) + 0.8f;
        float ez = MathF.Max(rz * 1.08f, rz * 0.48f + 2.6f * plant) + 0.8f;
        if (!TryDecorationSite(center, ex, ez, 24, out center)) return;
        IslandAt(center, rx, rz, palms, phase);
        if (roof is not null) HouseAt(center + new Vector3(0, 0.08f, -0.4f), phase * 0.25f, 1.1f, roof);
    }

    private void IslandAt(Vector3 center, float radiusX, float radiusZ, int palms, float phase)
    {
        int first = _scene.Objects.Count;
        float plant = PlantScale(radiusX, radiusZ);
        Add(Island, Rock, center, new(radiusX, MathF.Min(4.5f, 1.5f + MathF.Min(radiusX, radiusZ) * 0.65f), radiusZ));
        Add(Grass, GrassMaterial, center + new Vector3(0, 0.025f, 0), new(radiusX, 1, radiusZ));
        for (int i = 0; i < palms; i++)
        {
            float angle = phase + i * 2.1f;
            var foot = center + new Vector3(MathF.Cos(angle) * radiusX * 0.48f, 0.06f,
                MathF.Sin(angle) * radiusZ * 0.48f);
            float height = (3.2f + i * 0.45f) * plant;
            var trunk = Add(Trunk, Bark, foot, new(plant, height, plant));
            trunk.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
            Vector3 crown = foot + Vector3.Transform(new Vector3(0.34f * plant, height, 0), trunk.Transform.Rotation);
            for (int leaf = 0; leaf < 7; leaf++)
            {
                var frond = Add(Frond, leaf % 3 == 0 ? YoungLeaves : Leaves, crown,
                    new Vector3(2.6f, 1.8f, 2.6f) * plant);
                frond.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle + leaf * MathF.Tau / 7);
            }
            Add(Cloud, Coconut, crown - new Vector3(0, 0.13f, 0), new(0.13f, 0.17f, 0.13f));
        }
        // Кусты у края и пара камней дают масштаб островам.
        for (int i = 0; i < 4; i++)
        {
            float angle = phase + i * 1.6f;
            Add(Cloud, i % 2 == 0 ? YoungLeaves : Leaves,
                center + new Vector3(MathF.Cos(angle) * radiusX * 0.7f, 0.16f, MathF.Sin(angle) * radiusZ * 0.7f),
                new(0.55f, 0.32f, 0.45f));
        }
        for (int i = 0; i < 3; i++)
        {
            float angle = phase + i * 1.9f + 0.4f;
            var stone = Add(Cloud, Stone,
                center + new Vector3(MathF.Cos(angle) * radiusX * 0.78f, 0.15f, MathF.Sin(angle) * radiusZ * 0.78f),
                new(0.42f + i * 0.1f, 0.32f, 0.36f));
            stone.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
        }
        ApplyDistancePalette(first, center);
    }

    private void HouseAt(Vector3 foot, float yaw, float size, MaterialData roof)
    {
        int first = _scene.Objects.Count;
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
        Part(SoftBox, Sand, new(0, -0.025f, 0), new(2.65f, 0.1f, 2.3f));
        Part(SoftBox, Timber, new(0, 0.16f, 0), new(2.25f, 0.23f, 1.95f));
        Part(SoftBox, Walls, new(0, 0.86f, 0), new(1.9f, 1.25f, 1.55f));
        Part(Roof, roof, new(0, 1.5f, 0), new(1.25f, 1.15f, 1.05f));
        Part(SoftBox, Timber, new(0, 0.66f, 0.79f), new(0.46f, 0.83f, 0.05f));
        Part(Cloud, Sand, new(0.14f, 0.66f, 0.83f), new(0.035f));
        foreach (int side in new[] { -1, 1 })
        {
            Part(SoftBox, Timber, new(side * 0.65f, 1.03f, 0.795f), new(0.44f, 0.5f, 0.06f));
            Part(SoftBox, Window, new(side * 0.65f, 1.03f, 0.83f), new(0.34f, 0.38f, 0.02f));
            Part(SoftBox, WindowLight, new(side * 0.65f - 0.055f, 1.09f, 0.845f), new(0.1f, 0.21f, 0.012f));
            Part(SoftBox, Sand, new(side * 0.65f, 1.03f, 0.86f), new(0.025f, 0.39f, 0.025f));
            Part(SoftBox, Timber, new(side * 0.96f, 0.97f, 0), new(0.035f, 0.48f, 0.6f));
            Part(SoftBox, Window, new(side * 0.985f, 0.97f, 0), new(0.025f, 0.35f, 0.47f));
        }
        for (int i = 0; i < 3; i++)
            Part(SoftBox, Sand, new(0, 0.1f - i * 0.035f, 1.05f + i * 0.3f), new(0.75f, 0.1f, 0.28f));
        ApplyDistancePalette(first, foot);

        void Part(MeshData mesh, MaterialData material, Vector3 position, Vector3 scale)
        {
            var part = Add(mesh, material, foot + Vector3.Transform(position * size, rotation), scale * size);
            part.Transform.Rotation = rotation;
        }
    }

    private void ApplyDistancePalette(int first, Vector3 center)
    {
        float distance = Vector2.Distance(new(center.X, center.Z), (_routeMin + _routeMax) / 2);
        int amount = distance > 36 ? 28 : distance > 24 ? 14 : 0;
        if (amount == 0) return;
        for (int i = first; i < _scene.Objects.Count; i++)
        {
            var item = _scene.Objects[i];
            var source = item.Material;
            if (!DistantMaterials.TryGetValue((source, amount), out var material))
            {
                Vector3 mist = new(153, 215, 234);
                float mix = amount / 100f;
                var color = source.BaseColor;
                if (source.Texture is { } texture)
                    material = Textured(Texture(texture.Width, texture.Height, (x, y) =>
                    {
                        int at = (y * texture.Width + x) * 4;
                        Vector3 original = new(texture.Pixels[at] * color.R, texture.Pixels[at + 1] * color.G,
                            texture.Pixels[at + 2] * color.B);
                        return Vector3.Lerp(original, mist, mix);
                    }));
                else
                {
                    Vector3 toned = Vector3.Lerp(new Vector3(color.R, color.G, color.B) * 255, mist, mix);
                    material = Solid(toned.X, toned.Y, toned.Z);
                }
                material = DistantMaterials.GetOrAdd((source, amount), material);
            }
            item.Material = material;
        }
    }

    private void CloudAt(Vector3 center, float size)
    {
        Add(Cloud, CloudMaterial, center, new(size, size * 0.42f, size * 0.58f));
        Add(Cloud, CloudMaterial, center + new Vector3(-size * 0.65f, -size * 0.07f, 0),
            new(size * 0.65f, size * 0.32f, size * 0.48f));
        Add(Cloud, CloudMaterial, center + new Vector3(size * 0.7f, -size * 0.1f, 0),
            new(size * 0.6f, size * 0.3f, size * 0.45f));
        Add(Cloud, CloudMaterial, center + new Vector3(-size * 0.12f, size * 0.3f, 0),
            new(size * 0.65f, size * 0.5f, size * 0.5f));
    }

    private static MaterialData Solid(float r, float g, float b) =>
        new() { BaseColor = new(r / 255, g / 255, b / 255) };
    private static MaterialData Textured(TextureData texture) => new() { Texture = texture };

    private static MaterialData RoofMaterial(Vector3 color) => Textured(Texture(64, 64, (x, y) =>
        color * (x % 8 < 2 ? 0.8f : y % 16 < 2 ? 0.88f : 1f)));

    private static TextureData Texture(int width, int height, Func<int, int, Vector3> sample)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector3 color = Vector3.Clamp(sample(x, y), Vector3.Zero, new(255));
            int at = (y * width + x) * 4;
            pixels[at] = (byte)color.X;
            pixels[at + 1] = (byte)color.Y;
            pixels[at + 2] = (byte)color.Z;
            pixels[at + 3] = 255;
        }
        return new TextureData(width, height, pixels);
    }

    private static TextureData CoinTexture() => Texture(256, 256, (x, y) =>
    {
        Vector2 p = new((x - 127.5f) / 127.5f, (127.5f - y) / 127.5f);
        float radius = p.Length();
        Vector3 color = radius > 0.96f ? new(226, 146, 15)
            : radius > 0.87f ? new(255, 225, 89) : new(255, 191, 20);
        if (radius is > 0.81f and < 0.84f) color = new(231, 151, 14);
        if (InsideStar(p / 1.12f)) color = new(255, 249, 169);
        float light = Math.Clamp(1.04f + p.Y * 0.12f - p.X * 0.06f, 0.8f, 1.15f);
        return color * light;
    });

    private static bool InsideStar(Vector2 p) => SkinTextures.InsideStar(p);

    private static MeshData InwardSphere()
    {
        var sphere = GameMeshes.CreateSphere(48, 24);
        var indices = sphere.Indices.ToArray();
        for (int i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
        return new MeshData(sphere.Vertices.ToArray().Select(v => v with { Normal = Vector3.UnitY }).ToArray(), indices);
    }

    private static MeshData CreateRoadBody()
    {
        var mesh = new MeshBuilder();
        // Верх, включая светлый край, целиком принадлежит VisualRoadLayout.
        (float Y, float Radius)[] bands =
            [(0.5f, 0.5f), (0.46f, 0.5f), (0.36f, 0.496f), (0.2f, 0.483f),
             (-0.05f, 0.459f), (-0.28f, 0.438f), (-0.43f, 0.426f), (-0.5f, 0.422f)];
        const int sides = 32;
        for (int side = 0; side < sides; side++)
        for (int band = 0; band < bands.Length - 1; band++)
        {
            var hi = bands[band];
            var lo = bands[band + 1];
            Vector3 a = Point(side, hi.Y, hi.Radius), b = Point(side + 1, hi.Y, hi.Radius);
            Vector3 c = Point(side + 1, lo.Y, lo.Radius), d = Point(side, lo.Y, lo.Radius);
            mesh.Triangle(a, d, c, new(0, hi.Y + 0.5f), new(0, lo.Y + 0.5f), new(1, lo.Y + 0.5f));
            mesh.Triangle(a, c, b, new(0, hi.Y + 0.5f), new(1, lo.Y + 0.5f), new(1, hi.Y + 0.5f));
        }
        for (int side = 0; side < sides; side++)
            mesh.Triangle(new(0, -0.5f, 0), Point(side + 1, -0.5f, 0.422f), Point(side, -0.5f, 0.422f),
                new(0.5f, 0), new(0.5f, 0), new(0.5f, 0));
        return mesh.Build(smooth: true);

        static Vector3 Point(int side, float y, float radius)
        {
            float angle = -(side % sides) * MathF.Tau / sides;
            Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            Vector2 square = direction / MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
            // Верхняя прямоугольная плоскость точно сохраняется; скругление только ниже неё.
            float rounding = (0.5f - y) * 0.035f;
            Vector2 p = square * (radius - rounding) + direction * rounding;
            return new(p.X, y, p.Y);
        }
    }

    private static MeshData CreateIsland()
    {
        var mesh = new MeshBuilder();
        const int sides = 48;
        (float Radius, float Y)[] rings =
            [(1, 0), (1.008f, -0.045f), (0.995f, -0.13f), (0.955f, -0.25f),
             (0.875f, -0.39f), (0.75f, -0.53f), (0.58f, -0.66f), (0.39f, -0.77f),
             (0.22f, -0.84f), (0.09f, -0.88f), (0, -0.89f)];
        for (int ring = 0; ring < rings.Length - 1; ring++)
        for (int i = 0; i < sides; i++)
        {
            Vector3 a = Point(ring, i), b = Point(ring, i + 1);
            Vector3 c = Point(ring + 1, i), d = Point(ring + 1, i + 1);
            mesh.Triangle(a, b, c, Uv(a), Uv(b), Uv(c));
            mesh.Triangle(b, d, c, Uv(b), Uv(d), Uv(c));
        }
        return mesh.Build(smooth: true);

        Vector3 Point(int ring, int side)
        {
            float a = side % sides * MathF.Tau / sides;
            var profile = rings[ring];
            float r = profile.Radius * IslandOutline(a);
            float lean = 1 - profile.Radius;
            return new(MathF.Cos(a) * r + lean * 0.09f, profile.Y, MathF.Sin(a) * r - lean * 0.06f);
        }
        static Vector2 Uv(Vector3 p) => new(0.5f + p.X * 0.4f, 1 + p.Y);
    }

    private static MeshData CreateDisc()
    {
        var mesh = new MeshBuilder();
        const int sides = 48;
        (float Radius, float Y)[] rings = [(0, 0.035f), (0.85f, 0.035f), (0.96f, 0.018f), (1, -0.025f), (1, -0.055f)];
        for (int ring = 0; ring < rings.Length - 1; ring++)
        for (int i = 0; i < sides; i++)
        {
            Vector3 a = Point(ring, i), b = Point(ring, i + 1);
            Vector3 c = Point(ring + 1, i), d = Point(ring + 1, i + 1);
            mesh.Triangle(a, b, c, Uv(a), Uv(b), Uv(c));
            mesh.Triangle(b, d, c, Uv(b), Uv(d), Uv(c));
        }
        return mesh.Build(smooth: true);

        Vector3 Point(int ring, int side)
        {
            float a = side % sides * MathF.Tau / sides;
            var profile = rings[ring];
            float r = profile.Radius * IslandOutline(a);
            return new(MathF.Cos(a) * r, profile.Y, MathF.Sin(a) * r);
        }
        static Vector2 Uv(Vector3 p) => new(p.X / 2 + 0.5f, p.Z / 2 + 0.5f);
    }

    private static float IslandOutline(float angle) =>
        1 + 0.045f * MathF.Sin(angle * 3 + 0.4f) + 0.025f * MathF.Sin(angle * 5 - 0.3f);

    private static MeshData CreateTrunk()
    {
        var mesh = new MeshBuilder();
        const int sides = 16, sections = 12;
        for (int section = 0; section < sections; section++)
        for (int side = 0; side < sides; side++)
        {
            float lo = (float)section / sections, hi = (float)(section + 1) / sections;
            Vector3 a = Point(lo, side), b = Point(lo, side + 1), c = Point(hi, side + 1), d = Point(hi, side);
            mesh.Quad(a, d, c, b, Vector3.Normalize(Vector3.Cross(d - a, c - a)), new(0, lo), new(1, hi));
        }
        return mesh.Build(smooth: true);

        static Vector3 Point(float height, int side)
        {
            float a = side % sides * MathF.Tau / sides, r = 0.13f - height * 0.065f;
            return new(0.34f * height * height + MathF.Cos(a) * r, height, MathF.Sin(a) * r);
        }
    }

    private static MeshData CreateFrond()
    {
        var mesh = new MeshBuilder();
        const int sections = 28, sides = 8;
        for (int i = 0; i < sections; i++)
        for (int side = 0; side < sides; side++)
        {
            float a = (float)i / sections, b = (float)(i + 1) / sections;
            Vector3 p = Point(a, side), q = Point(a, side + 1), r = Point(b, side + 1), s = Point(b, side);
            mesh.Triangle(p, q, r);
            mesh.Triangle(p, r, s);
        }
        return mesh.Build(smooth: true);

        static Vector3 Point(float t, int side)
        {
            float angle = side % sides * MathF.Tau / sides;
            float swell = t is 0 or 1 ? 0 : MathF.Pow(MathF.Sin(t * MathF.PI), 0.8f);
            float width = swell * 0.18f * (1 - 0.13f * MathF.Pow(MathF.Sin(t * MathF.PI * 9), 4));
            float center = MathF.Sin(t * MathF.PI) * 0.32f - t * t * 0.4f;
            return new(t, center + MathF.Cos(angle) * swell * 0.028f, MathF.Sin(angle) * width);
        }
    }

    private static MeshData CreateRoof()
    {
        var mesh = new MeshBuilder();
        const int sides = 48, rings = 10;
        for (int ring = 0; ring < rings; ring++)
        for (int side = 0; side < sides; side++)
        {
            Vector3 a = Point(ring, side), b = Point(ring, side + 1);
            Vector3 c = Point(ring + 1, side), d = Point(ring + 1, side + 1);
            mesh.Triangle(a, b, c, Uv(a), Uv(b), Uv(c));
            mesh.Triangle(b, d, c, Uv(b), Uv(d), Uv(c));
        }
        return mesh.Build(smooth: true);

        static Vector3 Point(int ring, int side)
        {
            if (ring == 0) return new(0, 0.7f, 0);
            float r = (float)ring / rings, angle = side % sides * MathF.Tau / sides;
            float x = MathF.CopySign(MathF.Pow(MathF.Abs(MathF.Cos(angle)), 0.4f), MathF.Cos(angle));
            float z = MathF.CopySign(MathF.Pow(MathF.Abs(MathF.Sin(angle)), 0.4f), MathF.Sin(angle));
            return new(x * r, 0.7f * (1 - MathF.Pow(r, 1.5f)) + 0.055f * MathF.Pow(r, 8), z * r);
        }
        static Vector2 Uv(Vector3 p) => new(p.X / 2 + 0.5f, p.Z / 2 + 0.5f);
    }

    private static MeshData CreateSoftBox()
    {
        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        float[] coordinates = [-0.5f, -0.48f, -0.45f, -0.4f, 0, 0.4f, 0.45f, 0.48f, 0.5f];
        (Vector3 Normal, Vector3 U, Vector3 V)[] faces =
        [
            (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY), (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
            (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ), (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
            (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY),
        ];
        foreach (var face in faces)
        {
            uint first = (uint)vertices.Count;
            for (int y = 0; y < coordinates.Length; y++)
            for (int x = 0; x < coordinates.Length; x++)
            {
                Vector3 p = face.Normal * 0.5f + face.U * coordinates[x] + face.V * coordinates[y];
                Vector3 core = Vector3.Clamp(p, new(-0.4f), new(0.4f));
                Vector3 normal = Vector3.Normalize(p - core);
                vertices.Add(new(core + normal * 0.1f, new(coordinates[x] + 0.5f, coordinates[y] + 0.5f), normal));
            }
            for (int y = 0; y < coordinates.Length - 1; y++)
            for (int x = 0; x < coordinates.Length - 1; x++)
            {
                uint a = first + (uint)(y * coordinates.Length + x), b = a + (uint)coordinates.Length;
                indices.AddRange([a, a + 1, b + 1, a, b + 1, b]);
            }
        }
        return new MeshData(vertices, indices);
    }

    private static MeshData CreateCoin()
    {
        var mesh = new MeshBuilder();
        const int sides = 64;
        for (int i = 0; i < sides; i++)
        {
            float a = i * MathF.Tau / sides, b = (i + 1) * MathF.Tau / sides;
            Vector3 p = new(MathF.Cos(a), MathF.Sin(a), 0);
            Vector3 q = new(MathF.Cos(b), MathF.Sin(b), 0);
            foreach (int face in new[] { -1, 1 })
            {
                Vector3 front = new(0, 0, face * 0.12f);
                Vector3 innerP = p * 0.92f + front, innerQ = q * 0.92f + front;
                Vector3 outerP = p * 0.98f + front * 0.625f, outerQ = q * 0.98f + front * 0.625f;
                if (face == 1)
                {
                    mesh.Triangle(front, innerP, innerQ, new(0.5f), Uv(innerP), Uv(innerQ));
                    mesh.Triangle(innerP, outerP, outerQ, Uv(innerP), Uv(outerP), Uv(outerQ));
                    mesh.Triangle(innerP, outerQ, innerQ, Uv(innerP), Uv(outerQ), Uv(innerQ));
                }
                else
                {
                    mesh.Triangle(front, innerQ, innerP, new(0.5f), Uv(innerQ), Uv(innerP));
                    mesh.Triangle(innerP, outerQ, outerP, Uv(innerP), Uv(outerQ), Uv(outerP));
                    mesh.Triangle(innerP, innerQ, outerQ, Uv(innerP), Uv(innerQ), Uv(outerQ));
                }
            }
            Vector3 depth = new(0, 0, 0.075f);
            mesh.Quad(p * 0.98f - depth, q * 0.98f - depth, q * 0.98f + depth, p * 0.98f + depth,
                Vector3.Normalize(p + q), new(0.5f, 0.96f), new(0.51f, 0.98f));
        }
        // Рельефная звезда остаётся частью того же объекта и скрывается вместе с монетой.
        for (int i = 0; i < 10; i++)
        foreach (int face in new[] { -1, 1 })
        {
            Vector3 p = Star(i, 0.127f), q = Star(i + 1, 0.127f);
            Vector3 a = p * new Vector3(0.94f, 0.94f, 0) + new Vector3(0, 0, 0.148f);
            Vector3 b = q * new Vector3(0.94f, 0.94f, 0) + new Vector3(0, 0, 0.148f);
            Vector3 sign = new(1, 1, face), center = new(0, 0, face * 0.148f);
            Vector2 uv = new(0.5f);
            if (face == 1)
            {
                mesh.Triangle(center, a, b, uv, uv, uv);
                mesh.Triangle(p, q, b, uv, uv, uv);
                mesh.Triangle(p, b, a, uv, uv, uv);
            }
            else
            {
                mesh.Triangle(center, b * sign, a * sign, uv, uv, uv);
                mesh.Triangle(p * sign, b * sign, q * sign, uv, uv, uv);
                mesh.Triangle(p * sign, a * sign, b * sign, uv, uv, uv);
            }
        }
        return mesh.Build();
        static Vector2 Uv(Vector3 p) => new(p.X / 1.96f + 0.5f, p.Y / 1.96f + 0.5f);
        static Vector3 Star(int i, float z)
        {
            float angle = MathF.PI / 2 + i * MathF.PI / 5, radius = i % 2 == 0 ? 0.69f : 0.31f;
            return new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, z);
        }
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vertex> _vertices = [];
        private readonly List<uint> _indices = [];

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2? ua = null, Vector2? ub = null, Vector2? uc = null)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.LengthSquared() < 1e-14f) return;
            Vector3 normal = Vector3.Normalize(cross);
            uint first = (uint)_vertices.Count;
            _vertices.Add(new(a, ua ?? Vector2.Zero, normal));
            _vertices.Add(new(b, ub ?? Vector2.UnitX, normal));
            _vertices.Add(new(c, uc ?? Vector2.UnitY, normal));
            _indices.AddRange([first, first + 1, first + 2]);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
            Vector2? uv0 = null, Vector2? uv1 = null)
        {
            Vector2 lo = uv0 ?? Vector2.Zero, hi = uv1 ?? Vector2.One;
            uint first = (uint)_vertices.Count;
            _vertices.Add(new(a, lo, normal));
            _vertices.Add(new(b, new(hi.X, lo.Y), normal));
            _vertices.Add(new(c, hi, normal));
            _vertices.Add(new(d, new(lo.X, hi.Y), normal));
            _indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }

        public MeshData Build(bool smooth = false)
        {
            if (!smooth) return new(_vertices, _indices);
            // Сумма площадей граней даёт единый плавный свет на стыках общих позиций.
            var normals = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < _indices.Count; i += 3)
            {
                Vector3 a = _vertices[(int)_indices[i]].Position;
                Vector3 b = _vertices[(int)_indices[i + 1]].Position;
                Vector3 c = _vertices[(int)_indices[i + 2]].Position;
                Vector3 normal = Vector3.Cross(b - a, c - a);
                foreach (var position in new[] { a, b, c })
                    normals[position] = normals.GetValueOrDefault(position) + normal;
            }
            return new(_vertices.Select(v => v with { Normal = Vector3.Normalize(normals[v.Position]) }).ToArray(), _indices);
        }
    }
}
