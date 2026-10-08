using System;

#nullable disable

public static class RebirthNpcProgressionAiIntegration
{
    public static float EvaluateMoveSpeed(RebirthNpcStableId npcId,float requestedSpeed){return RebirthNpcProgressionModifierService.ScaleMoveSpeed(npcId,requestedSpeed);}
    public static float EvaluateDecisionCadenceSeconds(RebirthNpcStableId npcId,float baseSeconds){return Math.Max(0.05f,baseSeconds/RebirthNpcProgressionModifierService.GetResponsiveness(npcId));}
    public static bool EvaluateDodge(RebirthNpcStableId npcId,Guid attackId)
    {
        byte[] b=attackId.ToByteArray();int roll=((b[0]<<8)|b[1])%10000;return RebirthNpcProgressionModifierService.RollDodge(npcId,roll);
    }
    public static int EvaluateIncomingPhysicalDamage(RebirthNpcStableId npcId,int damage,Guid attackId){return EvaluateDodge(npcId,attackId)?0:Math.Max(1,damage);}
}
