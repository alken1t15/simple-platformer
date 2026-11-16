using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

/// <summary>Три протяжённых маршрута: освоение пружин, паромов и воздушных потоков.</summary>
public static class WinterExpeditionLevels
{
    private static SpringPad Spring(string id, float x, float top, float z = 0) =>
        new(id, new(new(x, top - 0.04f, z), new(1, 0.04f, 1.3f), Quaternion.Identity), 13.5f);
    private static SurfaceDefinition Ferry(string id, float x) =>
        new(id, new(new(x, -0.2f, 0), new(2, 0.2f, 2.5f), Quaternion.Identity), motion: new(new(4, 0, 0), 20));
    private static HazardDefinition Press(string id,float x,float top,float phase=0)=>
        new(id,new(x,top+2.5f,0),Vector3.UnitY,Vector3.Zero,new(.6f,.65f,2.1f),Quaternion.Identity,
            1.1f,phase,HazardResponse.Fail,HazardMotionKind.Shuttle,1.8f);

    public static LevelDefinition SpringTrail() => new("winter-spring-trail", new(0, 0.501f, 0), -10,
        Finish(new(156, 9, 0), true),
        [Flat("departure", 14, 0, 0, 36, 6), Flat("ledge-one", 49, 3, 0, 30, 5),
         Flat("ledge-two", 81, 6, 0, 30, 5), Flat("summit", 115, 9, 0, 34, 5),
         Flat("finish-deck", 146, 9, 0, 24, 6)],
        Coins(new(0,0,0),new(8,0,0),new(13,0,0),new(25,0,0),new(28,0,0),
            new(37,3,0),new(44,3,0),new(55,3,0),new(61,3,0),new(69,6,0),new(76,6,0),
            new(87,6,0),new(92,6,0),new(101,9,0),new(112,9,0),new(124,9,0),new(138,9,0),new(153,9,0)),
        [Press("spring-press-one",16,0),Press("spring-press-two",52,3,1.3f),
         Bumper("spring-sweeper-one",new(80,6,0),1.6f,.8f),Bumper("spring-sweeper-two",new(145,9,0),1.8f,.9f)],
        springs: [Spring("spring-one",30,0),Spring("spring-two",62,3),Spring("spring-three",94,6)],
        winds:[new("spring-breeze",new(new(72,7,0),new(5,4,5),Quaternion.Identity),new(0,0,1.2f))]);

    public static LevelDefinition GlacierFerries() => new("winter-glacier-ferries", new(0, 0.501f, 0), -10,
        Finish(new(174, 0, 0), true),
        [Flat("departure",10,0,0,28,7), Ferry("ferry-one",30),
         Flat("rest-one",55,0,0,36,6), Ferry("ferry-two",79),
         Flat("rest-two",104,0,0,36,6), Ferry("ferry-three",128),
         Flat("home",157,0,0,44,7)],
        Coins(new(0,0,0),new(8,0,0),new(16,0,0),new(22,0,0),new(39,0,0),new(47,0,0),
            new(53,0,0),new(65,0,0),new(71,0,0),new(88,0,0),new(96,0,0),new(108,0,0),
            new(114,0,0),new(120,0,0),new(137,0,0),new(146,0,0),new(160,0,0),new(171,0,0)),
        [Press("ferry-press-one",12,0),Bumper("ferry-sweeper-one",new(56,0,0),1.9f,.7f),
         Press("ferry-press-two",104,0,1.6f),Bumper("ferry-sweeper-two",new(156,0,0),2.1f,.8f)],
        winds:[new("ferry-breeze",new(new(79,2,0),new(6,4,5),Quaternion.Identity),new(0,0,.8f))]);

    public static LevelDefinition WindwardJourney() => new("winter-windward", new(0, 0.501f, 0), -12,
        Finish(new(174, 6, 0), true),
        [Flat("start",16,0,0,40,6),Flat("spring-approach",40,0,0,8,5),
         Flat("high-ridge",61,3,0,30,4),Flat("wind-ridge-a",82,3,0,12,2.4f),
         Flat("wind-ridge-b",96,3,0,12,2.4f),Flat("wind-ridge-c",110,3,0,12,2.4f),
         Flat("spring-rest",121,3,0,10,6),Flat("last-ridge",143,6,0,30,4),
         new("wind-ferry",new(new(161,5.8f,0),new(1.5f,0.2f,2),Quaternion.Identity),motion:new(new(2,0,0),16)),
         Flat("home",170,6,0,12,6)],
        Coins(new(0,0,0),new(12,0,0),new(26,0,0),new(41,0,0),new(49,3,0),new(58,3,0),
            new(70,3,0),new(80,3,0),new(86,3,0),new(98,3,0),new(108,3,0),new(122,3,0),
            new(131,6,0),new(141,6,0),new(152,6,0),new(163,6,0),new(171,6,0)),
        [Press("wind-press-one",20,0),Bumper("wind-sweeper-one",new(62,3,0),1.3f,.7f),
         Press("wind-press-two",144,6,1.2f),Bumper("wind-sweeper-two",new(168,6,0),1.6f,.9f)],
        springs:[Spring("wind-spring-one",42,0),Spring("wind-spring-two",124,3)],
        winds:[new("crosswind",new(new(96,6,0),new(18,5,6),Quaternion.Identity),new(0,0,2.5f)),
               new("updraft",new(new(44,3,0),new(3,4,4),Quaternion.Identity),new(0,1.5f,0))]);
}
