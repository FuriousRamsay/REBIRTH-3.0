using System;

/// <summary>Single size-aware label layout for every selected-item and equipment preview.</summary>
public static class RebirthItemPreviewLayout
{
    public readonly struct Label
    {
        public readonly float X,Y,Scale;
        public readonly int FontSize;
        public Label(float x,float y,float scale,int font){X=x;Y=y;Scale=scale;FontSize=font;}
    }
    public static Label Place(float sourceIconX,float sourceIconY,float sourceIconWidth,
        float destinationIconX,float destinationIconY,float destinationIconWidth,
        float sourceLabelX,float sourceLabelY,int sourceFont,bool quality,bool volume,
        bool nested,float parentX=0,float parentY=0,float railX=float.NaN,float railWidth=0,float labelWidth=0)
    {
        float ratio=destinationIconWidth/Math.Max(1f,sourceIconWidth);
        if((quality||volume)&&!float.IsNaN(railX))sourceLabelX=railX+(railWidth-labelWidth)*.5f;
        float x=destinationIconX+(sourceLabelX-sourceIconX)*ratio;
        float y=destinationIconY+(sourceLabelY-sourceIconY)*ratio;
        if(nested){x=(x-parentX)/ratio;y=(y-parentY)/ratio;}
        int font=Math.Max(1,(int)Math.Round(sourceFont*(volume?.8f:1f)/(quality?Math.Max(.01f,ratio):1f)));
        return new Label(x,y,nested?1f:ratio,font);
    }
}