using System.Collections.Concurrent;
using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Миниатюра настоящей геометрии маршрута сверху; создаётся один раз, без окна/GPU.</summary>
internal static class LevelThumbnails
{
    private static readonly ConcurrentDictionary<string,TextureData> Cache=new();
    internal static TextureData For(string id)=>Cache.GetOrAdd(id,Make);
    private static TextureData Make(string id)
    {
        var level=GameLevels.CreateCatalog().Get(id);
        const int width=160,height=96;
        var pixels=new byte[width*height*4];
        var points=level.Surfaces.SelectMany(s=>new[]{s.Bounds.Center-new Vector3(s.Bounds.HalfExtents.X,0,s.Bounds.HalfExtents.Z),
            s.Bounds.Center+new Vector3(s.Bounds.HalfExtents.X,0,s.Bounds.HalfExtents.Z)}).ToArray();
        float minX=points.Min(p=>p.X)-2,maxX=points.Max(p=>p.X)+2,minZ=points.Min(p=>p.Z)-2,maxZ=points.Max(p=>p.Z)+2;
        float pitch=MathF.Max((maxX-minX)/(width-12),(maxZ-minZ)/(height-12));
        float cx=(minX+maxX)/2,cz=(minZ+maxZ)/2;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            var world=new Vector3(cx+(x-width/2)*pitch,0,cz+(y-height/2)*pitch);
            var color=GameChapters.For(id)==GameChapter.Winter?new Vector3(24,54,79):new Vector3(26,86,100);
            foreach(var surface in level.Surfaces)
            {
                var local=surface.Bounds.ToLocal(world with {Y=surface.Bounds.Center.Y});
                if(MathF.Abs(local.X)<=surface.Bounds.HalfExtents.X&&MathF.Abs(local.Z)<=surface.Bounds.HalfExtents.Z)
                    color=surface.IsBarrier?new(83,112,129):surface.Motion is not null?new(84,224,214)
                        :GameChapters.For(id)==GameChapter.Winter?new(205,233,244):new(179,211,147);
            }
            if(level.Springs.Any(p=>Vector2.Distance(new(world.X,world.Z),new(p.Bounds.Center.X,p.Bounds.Center.Z))<pitch*2))color=new(82,238,133);
            if(Vector2.Distance(new(world.X,world.Z),new(level.Finish.Center.X,level.Finish.Center.Z))<pitch*3)color=new(255,208,77);
            int i=(y*width+x)*4;pixels[i]=(byte)color.X;pixels[i+1]=(byte)color.Y;pixels[i+2]=(byte)color.Z;pixels[i+3]=255;
        }
        return new(width,height,pixels);
    }
}
