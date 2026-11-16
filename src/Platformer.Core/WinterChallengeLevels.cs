using System.Numerics;
using static Platformer.Core.ChallengeGeometry;
namespace Platformer.Core;
public static class WinterChallengeLevels
{
    private static SurfaceDefinition Ice(string id, float x, float top, float width, float depth) =>
        new(id,new(new(x,top-0.2f,0),new(width/2,0.2f,depth/2),Quaternion.Identity),traction:0.12f);
    private static HazardDefinition Press(string id,float x,float top,float phase) =>
        new(id,new(x,top+2.5f,0),Vector3.UnitY,Vector3.Zero,new(0.7f,0.65f,2.1f),Quaternion.Identity,
            1.1f,phase,HazardResponse.Fail,HazardMotionKind.Shuttle,1.8f);
    public static LevelDefinition CrystalRun() => new("winter-crystal",new(-1,0.501f,0),-8,Finish(new(72,0,0),true),
        [Flat("start",0,0,0,6,6),Ice("lake-one",12,0,18,4.5f),Flat("rest",25,0,0,8,6),
         Ice("lake-two-a",33,0,8,3.2f),Flat("sweeper-island",41,0,0,8,8),Ice("lake-two-b",49,0,8,3.2f),Flat("runup",56,0,0,6,5),Flat("landing",70,0,0,8,5)],
        Coins(new(0,0,0),new(7,0,0),new(12,0,0),new(18,0,0),new(25,0,0),new(32,0,0),
            new(38,0,0),new(44,0,0),new(50,0,0),new(55,0,0),new(68,0,0),new(70.5f,0,0)),
        [Bumper("ice-sweeper",new(41,0,0),2.2f,0.8f),Press("crystal-press",16,0,0.4f)],
        [new("speed-one",new(1,0.8f,0),PowerupKind.Speed,12),new("speed-two",new(26.3f,0.8f,0),PowerupKind.Speed,12),
         new("jump-gap",new(56.3f,0.8f,0),PowerupKind.Jump,12)]);
    public static LevelDefinition GiftAscent() => new("winter-gifts",new(-1,0.501f,0),-8,Finish(new(63,6,0),true),
        [Flat("start",0,0,0,8,6),Flat("terrace-one",10,2,0,8,5),Flat("terrace-two",20,4,0,8,5),
         Flat("terrace-three",30,6,0,8,5),Ice("ridge",35,6,2,3.5f),Flat("press-island",40,6,0,8,6),Ice("ridge-end",45,6,2,3.5f),Flat("runup",49,6,0,6,5),Flat("finish",62,6,0,6,5)],
        Coins(new(0,0,0),new(3,0,0),new(8,2,0),new(12,2,0),new(18,4,0),new(22,4,0),
            new(28,6,0),new(32,6,0),new(38,6,0),new(44,6,0),new(49,6,0),new(61,6,0)),
        [Press("gift-press",40,6,1.5f)],
        [new("jump-start",new(2,0.8f,0),PowerupKind.Jump,12),new("jump-rest",new(10.2f,2.8f,0),PowerupKind.Jump,12),
         new("jump-upper",new(20.2f,4.8f,0),PowerupKind.Jump,12),new("jump-finish",new(50.2f,6.8f,0),PowerupKind.Jump,12)]);
    public static LevelDefinition MidnightTrial() => new("winter-midnight",new(-1,0.501f,0),-8,Finish(new(90,0,0),true),
        [Flat("start",0,0,0,6,6),Ice("ice-one",5,0,4,4),Flat("press-deck",16,0,0,18,6),Flat("rest-one",29,0,0,8,6),
         Flat("bridge",41,0,0,16,4.5f),Flat("rest-two",53,0,0,8,6),Ice("ice-two",58,0,2,3),Flat("sweeper-rest",65,0,0,12,8),Ice("ice-three",72,0,2,3),
         Flat("runup",76,0,0,6,5),Flat("finish",89,0,0,6,5), ..ExpertGeometry.Gantry("midnight-arch",41,0,0)],
        Coins(new(0,0,0),new(8,0,0),new(16,0,0),new(23,0,0),new(29,0,0),new(37,0,0),
            new(45,0,0),new(53,0,0),new(61,0,0),new(69,0,0),new(76,0,0),new(88,0,0)),
        [Press("midnight-press-a",12,0,0),Press("midnight-press-b",20,0,2),
         new("pendulum",new(41,4.8f,0),Vector3.UnitX,new(0,-4.25f,0),new(0.68f),Quaternion.Identity,1.35f,0,HazardResponse.Knockback,HazardMotionKind.Pendulum,0.95f),
         Bumper("final-sweeper",new(65,0,0),2.3f,1.2f)],
        [new("speed-start",new(1,0.8f,0),PowerupKind.Speed,12),new("speed-rest",new(30.3f,0.8f,0),PowerupKind.Speed,12),
         new("speed-upper",new(54.3f,0.8f,0),PowerupKind.Speed,12),new("jump-finish",new(77.3f,0.8f,0),PowerupKind.Jump,12)]);
}
