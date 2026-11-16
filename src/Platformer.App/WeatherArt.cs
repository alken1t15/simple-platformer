using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

internal sealed partial class VisualScene
{
    private static readonly MaterialData EveningWinterSky=Textured(Texture(8,256,(_,y)=>
        Vector3.Lerp(new(14,23,57),new(158,151,187),y/255f)));
    private UserSettings? _preferences;
    private readonly List<(SceneObject Item,Vector3 Position,Quaternion Rotation,Vector3 Pivot)> _breezeArt=[];
    private bool _evening;
    private bool _gusty;

    public void SetPreferences(UserSettings preferences)
    {
        _preferences=preferences;
        if(_breezeArt.Count>0)return;
        foreach(var item in _scene.Objects.Where(o=>ReferenceEquals(o.Mesh,Cone)||ReferenceEquals(o.Material,Leaves)
            ||ReferenceEquals(o.Material,YoungLeaves)||ReferenceEquals(o.Material,Banner)))
        {
            var pivot=item.Transform.Position;
            if(ReferenceEquals(item.Mesh,Cone))pivot-=Vector3.UnitY*item.Transform.Scale.Y*0.05f;
            _breezeArt.Add((item,item.Transform.Position,item.Transform.Rotation,pivot));
        }
    }
    public void ConfigureWeather(string levelId,IReadOnlyList<SurfaceDefinition> surfaces)
    {
        _evening=levelId is "winter-midnight" or "winter-aurora" or "winter-glacier-ferries";
        _gusty=levelId is "winter-windward" or "winter-crystal";
        if(_winter&&_evening)
        {
            foreach(var sky in _scene.Objects.Where(o=>ReferenceEquals(o.Mesh,Sky)))sky.Material=EveningWinterSky;
            _scene.Lighting.AmbientColor=new(0.54f,0.61f,0.79f);
            _scene.Lighting.DirectionalColor=new(0.55f,0.40f,0.29f);
        }
    }
    private void AnimateEnvironment(double seconds)
    {
        float sway=_preferences?.ReducedEffects==true?0:0.014f*MathF.Sin((float)Math.IEEERemainder(seconds*0.9,Math.Tau));
        foreach(var (item,origin,rotation,pivot) in _breezeArt)
        {
            var breeze=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,sway);
            item.Transform.Position=pivot+Vector3.Transform(origin-pivot,breeze);
            item.Transform.Rotation=breeze*rotation;
        }
    }
}
