using System.Numerics;
using Engine3D.Core;
using Engine3D.OpenGL;

namespace Platformer.App;

/// <summary>Компактный итог главы; те же bounds используются для изображения и ввода.</summary>
internal static class ChapterCompletionLayout
{
    internal static MenuView Build(MenuPage page,int selectedIndex,Vector2? pointer,string? pressedId,float width,float height)
    {
        float scale=MathF.Min(width/1280,height/720);
        float left=(width-1280*scale)/2,top=(height-720*scale)/2;
        ColorRgba ink=new(.94f,.98f,1),muted=new(.72f,.85f,.87f),accent=new(.15f,.65f,1);
        var controls=new List<MenuControl>();var panels=new List<MenuPanel>();
        var texts=new List<MenuText>();var images=new List<MenuImage>();
        MenuRect Rect(float x,float y,float w,float h)=>new(left+x*scale,top+y*scale,w*scale,h*scale);
        void Text(string text,float x,float y,float size,ColorRgba color)=>
            texts.Add(new(new(left+x*scale,top+y*scale),text,size*scale,color));
        panels.Add(new(new(0,0,width,height),new(.015f,.025f,.045f,.65f),MenuPanelKind.Backdrop));
        panels.Add(new(Rect(340,200,600,320),new(.035f,.1f,.14f,.96f),MenuPanelKind.Sheet));
        images.Add(new(Rect(372,228,56,56),DialogArtwork.Finish));
        Text(page.Title,448,234,28,ink);
        Text("CHAPTER CLEARED",448,270,13,muted);
        panels.Add(new(Rect(372,306,536,76),new(.05f,.19f,.22f),MenuPanelKind.Flat));
        images.Add(new(Rect(390,324,38,38),MenuArtwork.FilledStar));
        Text(page.Details[0],448,330,22,ink);
        Text(page.Details[1],372,401,16,muted);
        for(int i=0;i<page.Options.Count;i++)
        {
            var option=page.Options[i];var bounds=Rect(372+i*276,444,260,44);
            bool selected=selectedIndex==i;
            controls.Add(new(option.Id,option.Label,option.Kind,bounds,selected,
                pointer is {} position&&bounds.Contains(position),pressedId==option.Id,Enabled:option.Enabled));
            panels.Add(new(bounds,selected?new(.12f,.28f,.34f):new(.06f,.16f,.21f),MenuPanelKind.Control));
            if(selected)panels.Add(new(new(bounds.X,bounds.Y+8*scale,3*scale,bounds.Height-16*scale),accent,MenuPanelKind.Focus));
            float font=20*scale;var metrics=OverlayTextMetrics.MeasureInkBounds(option.Label,font);
            texts.Add(new(new(bounds.X+20*scale,bounds.Y+(bounds.Height-metrics.Height)/2-metrics.Y),option.Label,font,ink));
        }
        return new(AppScreen.GameComplete,new(width,height),controls,panels,texts,images);
    }
}
