using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Адаптация CPU-макета уровня к публичным типам движка без создания GPU-контекста.</summary>
public static class PreviewScene
{
    public static SceneSnapshot Create()
    {
        var scene = new Scene();
        var mesh = PrimitiveFactory.CreateCube();
        var material = new MaterialData { BaseColor = new ColorRgba(0.25f, 0.7f, 0.85f) };
        foreach (var platform in PreviewLevel.Platforms)
        {
            var item = new SceneObject(mesh, material);
            item.Transform.Position = platform.Center;
            item.Transform.Scale = platform.Size;
            scene.Add(item);
        }

        var camera = new Camera
        {
            Position = new Vector3(3f, 1f, 10f),
            Target = new Vector3(3f, 1f, 0f),
        };
        return new SceneSnapshot(scene, camera);
    }
}
