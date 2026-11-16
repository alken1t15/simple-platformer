using Engine3D.Audio;
using Platformer.Core;

namespace Platformer.App;

public interface IGameSoundscape
{
    bool SetMix(float music,float effects);
    void UpdateSoundscape(LevelDefinition level,SessionSnapshot? snapshot,AppScreen screen,UserSettings settings);
    void PauseSoundscape();
}

public sealed partial class NativeAudioPlayback
{
    private float musicGain=1,effectsGain=1;
    private readonly Dictionary<string,AudioClip> atmosphere=new();
    private readonly Queue<AudioVoice> sceneEffects=new();
    private AudioVoice? ambience;
    private string ambienceKey="";
    private SessionSnapshot? previousSound;

    public bool SetMix(float music,float effects)
    {
        if(!float.IsFinite(music)||!float.IsFinite(effects)||music is <0 or >1||effects is <0 or >1)
            throw new ArgumentOutOfRangeException(nameof(music));
        bool changed=musicGain!=music;
        if(effectsGain!=effects)PauseSoundscape();
        musicGain=music;effectsGain=effects;return changed;
    }
    private AudioClip Atmosphere(string name)
    {
        if(!atmosphere.TryGetValue(name,out var clip))
            atmosphere[name]=clip=device.LoadWave(Path.Combine(audioDirectory,name+".wav"));
        return clip;
    }
    public void UpdateSoundscape(LevelDefinition level,SessionSnapshot? snapshot,AppScreen screen,UserSettings settings)
    {
        if(snapshot is null||screen!=AppScreen.Playing){PauseSoundscape();return;}
        bool newRun=previousSound?.SessionId!=snapshot.SessionId;
        if(newRun){PauseSoundscape();previousSound=null;}
        bool winter=GameChapters.For(level.Id)==GameChapter.Winter;
        string chapter=winter?"winter-air":"tropical-air";
        if(ambienceKey!=chapter||ambience is null)
        {
            ambience?.Dispose();ambienceKey=chapter;
            ambience=device.Play(Atmosphere(chapter),loop:true,volume:0.14f*effectsGain);
        }
        var ball=snapshot.Ball;
        if(previousSound is { } before&&before.Tick<snapshot.Tick)
        {
            if(before.Ball.Grounded&&ball.Velocity.Y>8)PlaySpringSound();
        }
        previousSound=snapshot;
    }
    private void PlaySpringSound()
    {
        while(sceneEffects.Count>=8)sceneEffects.Dequeue().Dispose();
        sceneEffects.Enqueue(device.Play(effects[GameEventKind.Jumped],volume:0.65f*effectsGain));
    }
    public void PauseSoundscape()
    {
        ambience?.Dispose();ambience=null;ambienceKey="";
        while(sceneEffects.TryDequeue(out var voice))voice.Dispose();
    }
}
