using System.Collections.Concurrent;
using System.Numerics;
using Engine3D.Core;
namespace Platformer.App;

internal enum MachineFinish { Steel, Bumper, Danger }

/// <summary>Индивидуальные UV шести граней: детали измерены в единицах мира, не растянуты вдоль балки.</summary>
internal static class MachineArt
{
    private static readonly ConcurrentDictionary<(Vector3, MachineFinish), (MeshData Mesh, MaterialData Material)> Cache = new();
    internal static (MeshData Mesh, MaterialData Material) Box(Vector3 size, MachineFinish finish) => Cache.GetOrAdd((size, finish), key => Create(key.Item1, key.Item2));
    private static (MeshData, MaterialData) Create(Vector3 size, MachineFinish finish)
    {
        const int tile = 256, width = tile * 3, height = tile * 2;
        var pixels = new byte[width * height * 4];
        var vertices = new List<Vertex>(); var indices = new List<uint>();
        (Vector3 N, Vector3 U, Vector3 V)[] faces = [(Vector3.UnitX,-Vector3.UnitZ,Vector3.UnitY),(-Vector3.UnitX,Vector3.UnitZ,Vector3.UnitY),
            (Vector3.UnitY,Vector3.UnitX,-Vector3.UnitZ),(-Vector3.UnitY,Vector3.UnitX,Vector3.UnitZ),
            (Vector3.UnitZ,Vector3.UnitX,Vector3.UnitY),(-Vector3.UnitZ,-Vector3.UnitX,Vector3.UnitY)];
        for (int face=0; face<6; face++)
        {
            var (n,u,v)=faces[face]; float su=Vector3.Abs(u*size).Length(), sv=Vector3.Abs(v*size).Length();
            int ox=face%3*tile, oy=face/3*tile;
            for(int y=0;y<tile;y++) for(int x=0;x<tile;x++)
            {
                float a=(float)x/(tile-1), b=(float)y/(tile-1), worldU=a*su, worldV=b*sv;
                float edge=MathF.Min(MathF.Min(worldU,su-worldU),MathF.Min(worldV,sv-worldV));
                float grain=MathF.Sin(worldU*163+worldV*39)*2+MathF.Sin(worldV*390)*1.5f;
                Vector3 color = finish == MachineFinish.Bumper ? new(40,117,122) : new(83,105,122);
                color += new Vector3(grain + 6*MathF.Sin(worldV*13));
                bool band = (worldU+worldV)%0.72f < 0.32f;
                if(edge<0.07f && finish!=MachineFinish.Steel) color=band?new(239,174,57):new(29,38,47);
                else if(edge<0.035f) color=new(42,57,69);
                float boltU=MathF.Min(MathF.Abs(worldU-0.095f),MathF.Abs(worldU-(su-0.095f)));
                float boltV=MathF.Min(MathF.Abs(worldV-0.095f),MathF.Abs(worldV-(sv-0.095f)));
                if(su>0.25f && sv>0.25f && boltU*boltU+boltV*boltV<0.001f) color=new(193,210,216);
                if(finish==MachineFinish.Danger && edge>0.07f) color=Vector3.Lerp(color,new(137,48,40),0.6f);
                int p=((oy+y)*width+ox+x)*4;
                pixels[p]=(byte)Math.Clamp(color.X,0,255); pixels[p+1]=(byte)Math.Clamp(color.Y,0,255);pixels[p+2]=(byte)Math.Clamp(color.Z,0,255);pixels[p+3]=255;
            }
            uint first=(uint)vertices.Count;
            foreach(var corner in new[]{new Vector2(-1,-1),new(1,-1),new(1,1),new(-1,1)})
            {
                Vector3 p=n*0.5f+u*corner.X*0.5f+v*corner.Y*0.5f;
                float tx=(corner.X+1)*0.5f, ty=(corner.Y+1)*0.5f;
                vertices.Add(new(p,new((ox+0.5f+tx*(tile-1))/width,1-(oy+0.5f+(1-ty)*(tile-1))/height),n));
            }
            indices.AddRange([first,first+1,first+2,first,first+2,first+3]);
        }
        return (new(vertices.ToArray(),indices),new MaterialData{Texture=new TextureData(width,height,pixels)});
    }

    internal static MeshData Spikes(Vector3 size)
    {
        var vertices=new List<Vertex>(); var indices=new List<uint>();
        bool alongZ=size.Z>=size.X; float length=alongZ?size.Z:size.X;
        int count=Math.Max(1,(int)MathF.Ceiling(length/0.38f));
        float bottom=-0.5f, baseY=-0.25f;
        for(int i=0;i<count;i++)
        {
            float a=-0.5f+(float)i/count, b=-0.5f+(float)(i+1)/count;
            Vector3 P(float x,float y,float z)=>alongZ?new(x,y,z):new(z,y,x);
            var corners=new[]{P(-0.5f,baseY,a),P(0.5f,baseY,a),P(0.5f,baseY,b),P(-0.5f,baseY,b)};
            Vector3 tip=P(0,0.5f,(a+b)/2);
            for(int j=0;j<4;j++) Triangle(corners[j],tip,corners[(j+1)%4],P(0,baseY,(a+b)/2));
        }
        // Низкая металлическая кассета внутри исходного OBB.
        var box=GameMeshes.Box;
        foreach(uint index in box.Indices)
        {
            var v=box.Vertices[(int)index]; uint next=(uint)vertices.Count;
            vertices.Add(new(new(v.Position.X,bottom+(v.Position.Y+0.5f)*0.25f,v.Position.Z),v.Uv,v.Normal)); indices.Add(next);
        }
        return new(vertices.ToArray(),indices);
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 origin)
        {
            // CCW наружу; порядок проверяется относительно центра пирамиды.
            var normal=Vector3.Normalize(Vector3.Cross(b-a,c-a));
            Vector3 center=(a+b+c)/3;
            if(Vector3.Dot(normal,center-origin)<0){(b,c)=(c,b);normal=-normal;}
            uint first=(uint)vertices.Count;
            vertices.Add(new(a,new(0,0),normal));vertices.Add(new(b,new(0.5f,1),normal));vertices.Add(new(c,new(1,0),normal));indices.AddRange([first,first+1,first+2]);
        }
    }
}