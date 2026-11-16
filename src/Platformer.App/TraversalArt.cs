using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

public sealed partial class GameScenePresenter
{
    private static readonly MaterialData SpringDeck = CreateSpringDeck();
    private static readonly MeshData SpringArrow = CreateSpringArrow();
    private readonly List<(SceneObject Item,Vector3 Origin,Quaternion Rotation,int Index)> _windArt = [];
    private readonly List<(SceneObject Item,SurfaceDefinition Surface,Vector3 Local)> _ferryArt=[];
    private UserSettings? _preferences;

    public void SetPreferences(UserSettings settings)
    {
        _preferences = settings;
        _visual?.SetPreferences(settings);
        _visual?.ConfigureWeather(_level.Id,_level.Surfaces);
    }

    private void CreateTraversalArt()
    {
        var mint = new MaterialData { BaseColor=new(0.49f,0.87f,0.78f) };
        foreach(var pad in _level.Springs)
        {
            var deck=Add(GameMeshes.Box,SpringDeck);
            deck.Transform.Position=pad.Bounds.Center+Vector3.UnitY*(pad.Bounds.HalfExtents.Y+0.008f);
            deck.Transform.Scale=new(pad.Bounds.HalfExtents.X*2,0.012f,pad.Bounds.HalfExtents.Z*2);
            foreach(int side in new[]{-1,1})
            {
                var edge=Add(GameMeshes.Box,mint);
                edge.Transform.Position=deck.Transform.Position+new Vector3(0,0.009f,side*(pad.Bounds.HalfExtents.Z-0.08f));
                edge.Transform.Scale=new(pad.Bounds.HalfExtents.X*2-0.12f,0.009f,0.045f);
                var end=Add(GameMeshes.Box,mint);
                end.Transform.Position=deck.Transform.Position+new Vector3(side*(pad.Bounds.HalfExtents.X-0.08f),0.009f,0);
                end.Transform.Scale=new(0.045f,0.009f,pad.Bounds.HalfExtents.Z*2-0.12f);
            }
            var arrow=Add(SpringArrow,mint);
            arrow.Transform.Position=deck.Transform.Position+Vector3.UnitY*0.012f;
        }
        foreach(var ferry in _level.Surfaces.Where(s=>s.Motion is not null))
        foreach(int side in new[]{-1,1})
        {
            var rail=Add(GameMeshes.Box,new MaterialData{BaseColor=new(0.94f,0.77f,0.35f)});
            Vector3 local=new(0,ferry.Bounds.HalfExtents.Y+0.016f,side*(ferry.Bounds.HalfExtents.Z-0.09f));
            rail.Transform.Scale=new(ferry.Bounds.HalfExtents.X*2-0.15f,0.025f,0.08f);
            _ferryArt.Add((rail,ferry,local));
        }
        foreach(var wind in _level.Winds)
        {
            var support=_level.Surfaces.Where(s=>!s.IsBarrier&&s.Motion is null)
                .OrderBy(s=>MathF.Abs(s.Bounds.Center.X-wind.Bounds.Center.X)).First();
            Vector3 foot=support.Bounds.ToWorld(new(Math.Clamp(wind.Bounds.Center.X-support.Bounds.Center.X,
                -support.Bounds.HalfExtents.X+.2f,support.Bounds.HalfExtents.X-.2f),support.Bounds.HalfExtents.Y,
                -support.Bounds.HalfExtents.Z+.12f));
            var pole=Add(GameMeshes.Box,new MaterialData{BaseColor=new(.34f,.48f,.55f)});
            pole.Transform.Position=foot+Vector3.UnitY*.8f;pole.Transform.Scale=new(.055f,1.6f,.055f);
            Vector3 direction=Vector3.Normalize(wind.Acceleration);
            Quaternion rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-direction.Z,direction.X));
            if(MathF.Abs(direction.Y)>.8f)rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,MathF.PI/2);
            for(int i=0;i<3;i++)
            {
                var item=Add(GameMeshes.Box,new MaterialData{BaseColor=i%2==0?new(.96f,.7f,.31f):new(.94f,.97f,.94f)});
                Vector3 origin=foot+Vector3.UnitY*1.48f+Vector3.Transform(new Vector3(.16f+i*.24f,0,0),rotation);
                item.Transform.Position=origin;item.Transform.Scale=new(.25f,.18f-i*.035f,.035f);
                item.Transform.Rotation=rotation;_windArt.Add((item,origin,rotation,i));
            }
        }
    }

    private void AnimateTraversal(SessionSnapshot snapshot)
    {
        foreach(var surface in _level.Surfaces.Where(s=>s.Motion is not null))
            SetBounds(_surfaces[surface.Id],surface.BoundsAt(snapshot.SimulationSeconds));
        foreach(var (item,surface,local) in _ferryArt)
        {
            var bounds=surface.BoundsAt(snapshot.SimulationSeconds);
            item.Transform.Position=bounds.ToWorld(local);item.Transform.Rotation=bounds.Orientation;
        }
        foreach(var (item,origin,rotation,index) in _windArt)
        {
            float phase=(float)Math.IEEERemainder(snapshot.SimulationSeconds*2.1,Math.Tau);
            float wave=_preferences?.ReducedEffects==true?0:MathF.Sin(phase-index*.7f)*.025f*index;
            item.Transform.Position=origin+Vector3.UnitY*wave;
        }
    }

    private static MaterialData CreateSpringDeck()
    {
        const int size=128;
        var pixels=new byte[size*size*4];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            bool seam=x%16==0||y%16==0;
            int grain=(x*17+y*31)%5;
            int i=(y*size+x)*4;
            pixels[i]=(byte)(seam?29:43+grain);
            pixels[i+1]=(byte)(seam?61:78+grain);
            pixels[i+2]=(byte)(seam?70:87+grain);
            pixels[i+3]=255;
        }
        return new(){Texture=new(size,size,pixels)};
    }

    private static MeshData CreateSpringArrow()
    {
        Vector2[] outline=[new(.7f,0),new(.05f,-.55f),new(.05f,-.22f),new(-.55f,-.22f),
            new(-.55f,.22f),new(.05f,.22f),new(.05f,.55f)];
        var vertices=new List<Vertex>();var indices=new List<uint>();
        for(int i=0;i<outline.Length;i++)
        {
            uint start=(uint)vertices.Count;
            foreach(var point in new[]{Vector2.Zero,outline[i],outline[(i+1)%outline.Length]})
                vertices.Add(new(new(point.X,0,point.Y),Vector2.Zero,Vector3.UnitY));
            indices.AddRange([start,start+1,start+2]);
        }
        return new(vertices,indices);
    }
}
