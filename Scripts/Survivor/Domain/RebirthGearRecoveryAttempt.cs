using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Saved BEFORE native spawning. An existing attempt may only be reconciled, never respawned.
public sealed class RebirthGearRecoveryAttempt
{
    public Guid PublicationId{get;private set;}
    public int EntityId{get;private set;}public int OriginalOwnerEntityId{get;private set;}
    public float X{get;private set;}public float Y{get;private set;}public float Z{get;private set;}public float Yaw{get;private set;}
    public int LifetimeSeconds{get;private set;}public ulong WorldTime{get;private set;}
    private RebirthGearRecoveryAttempt(){}
    public static bool TryCreate(Guid publication,int entity,float x,float y,float z,float yaw,int lifetime,ulong time,out RebirthGearRecoveryAttempt attempt)
    {
        attempt=null;if(publication==Guid.Empty||entity<=0||entity==int.MaxValue||!Finite(x)||!Finite(y)||!Finite(z)||!Finite(yaw)||(lifetime!=1800&&lifetime!=3600))return false;
        attempt=new RebirthGearRecoveryAttempt{PublicationId=publication,EntityId=entity,X=x,Y=y,Z=z,Yaw=yaw,LifetimeSeconds=lifetime,WorldTime=time};return true;
    }
    public static bool TryCreate(Guid publication,int entity,float x,float y,float z,float yaw,int lifetime,ulong time,int originalOwnerEntityId,out RebirthGearRecoveryAttempt attempt)
    {
        attempt=null;if(originalOwnerEntityId<=0||originalOwnerEntityId==int.MaxValue||!TryCreate(publication,entity,x,y,z,yaw,lifetime,time,out var candidate))return false;
        candidate.OriginalOwnerEntityId=originalOwnerEntityId;attempt=candidate;return true;
    }
    public XElement ToXml(){return new XElement("attempt",new XAttribute("id",PublicationId.ToString("N")),new XAttribute("entity",EntityId),OriginalOwnerEntityId>0?new XAttribute("ownerEntity",OriginalOwnerEntityId):null,new XAttribute("x",X.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("y",Y.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("z",Z.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("yaw",Yaw.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("lifetime",LifetimeSeconds),new XAttribute("worldTime",WorldTime));}
    public static bool TryRead(XElement value,out RebirthGearRecoveryAttempt attempt)
    {
        attempt=null;Guid id;int entity,lifetime;float x,y,z,yaw;ulong time;
        if(value==null||value.Name!="attempt"||(value.Attributes().Count()!=8&&value.Attributes().Count()!=9)||value.Nodes().Any()||!Guid.TryParseExact((string)value.Attribute("id"),"N",out id)||(string)value.Attribute("id")!=id.ToString("N")||!int.TryParse((string)value.Attribute("entity"),NumberStyles.None,CultureInfo.InvariantCulture,out entity)||!int.TryParse((string)value.Attribute("lifetime"),NumberStyles.None,CultureInfo.InvariantCulture,out lifetime)||!ulong.TryParse((string)value.Attribute("worldTime"),NumberStyles.None,CultureInfo.InvariantCulture,out time)||!Float(value,"x",out x)||!Float(value,"y",out y)||!Float(value,"z",out z)||!Float(value,"yaw",out yaw))return false;
        var owner=value.Attribute("ownerEntity");
        if(owner!=null){int originalOwner;if(!int.TryParse(owner.Value,NumberStyles.None,CultureInfo.InvariantCulture,out originalOwner))return false;return TryCreate(id,entity,x,y,z,yaw,lifetime,time,originalOwner,out attempt);}
        if(value.Attributes().Count()!=8)return false;
        return TryCreate(id,entity,x,y,z,yaw,lifetime,time,out attempt);
    }
    internal static bool ValidateSet(XElement value,RebirthGearRecoveryManifest manifest)
    {
        if(value==null)return true;
        if(manifest==null||value.Name!="recoveryAttempts"||value.Attributes().Count()!=1||(string)value.Attribute("version")!="1"||value.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        var attempts=value.Elements().Take(190).ToArray();if(attempts.Length==0||attempts.Length>189)return false;
        var ids=new System.Collections.Generic.HashSet<Guid>();var entities=new System.Collections.Generic.HashSet<int>();var publications=manifest.ToXml().Elements().ToArray();
        foreach(var xml in attempts){if(!TryRead(xml,out var attempt)||!ids.Add(attempt.PublicationId)||!entities.Add(attempt.EntityId))return false;var publication=publications.SingleOrDefault(p=>(string)p.Attribute("id")==attempt.PublicationId.ToString("N"));if(publication==null||attempt.LifetimeSeconds!=((string)publication.Attribute("kind")=="backpack"?3600:1800))return false;}
        return true;
    }
    private static bool Float(XElement value,string key,out float result){return float.TryParse((string)value.Attribute(key),NumberStyles.Float,CultureInfo.InvariantCulture,out result)&&Finite(result);}
    private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
}