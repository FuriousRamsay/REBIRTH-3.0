using System;
public struct RebirthNpcStableId{
 public Guid Value;public bool IsEmpty=>Value==Guid.Empty;public static RebirthNpcStableId NewId()=>new RebirthNpcStableId{Value=Guid.NewGuid()};
}
public class RebirthNpcRuntimeState{public string ProfileId;public RebirthNpcStableId StableId;}
public static class RebirthNpcRuntimeRegistry{
 public static RebirthNpcRuntimeState Current;public static int Canonical=1;public static bool Reverse=true,Lookup=true,Throws;
 public static RebirthNpcRuntimeState Register(int entity,string profile,RebirthNpcStableId requested){if(Throws)throw new Exception();return Current??(Current=new RebirthNpcRuntimeState{ProfileId=profile,StableId=requested});}
 public static bool TryGetEntityId(RebirthNpcStableId stable,out int entity){entity=Canonical;return Reverse;}
 public static bool TryGet(int entity,out RebirthNpcRuntimeState runtime){runtime=Current;return Lookup;}
}
public static class SpawnIdentityFixture{
 static int checks;static void Check(bool test,string label){if(!test)throw new Exception(label);checks++;}
 static void Reset(){RebirthNpcRuntimeRegistry.Current=null;RebirthNpcRuntimeRegistry.Canonical=1;RebirthNpcRuntimeRegistry.Reverse=RebirthNpcRuntimeRegistry.Lookup=true;RebirthNpcRuntimeRegistry.Throws=false;}
 public static string Run(){RebirthNpcStableId id;string reason;Reset();Check(RebirthNpcSpawnIdentity.TryBind(1,"survivor.ambient",out id,out reason)&&!id.IsEmpty,"fresh binding failed");var original=id;Check(RebirthNpcSpawnIdentity.TryBind(1,"SURVIVOR.AMBIENT",out id,out reason)&&id.Value==original.Value,"existing canonical identity replaced");Check(!RebirthNpcSpawnIdentity.TryBind(1,"bandit.standard",out id,out reason)&&id.IsEmpty,"foreign profile accepted");Reset();RebirthNpcRuntimeRegistry.Canonical=2;Check(!RebirthNpcSpawnIdentity.TryBind(1,"survivor.ambient",out id,out reason),"wrong reverse entity accepted");Reset();RebirthNpcRuntimeRegistry.Reverse=false;Check(!RebirthNpcSpawnIdentity.TryBind(1,"survivor.ambient",out id,out reason),"missing reverse accepted");Reset();RebirthNpcRuntimeRegistry.Lookup=false;Check(!RebirthNpcSpawnIdentity.TryBind(1,"survivor.ambient",out id,out reason),"missing current accepted");Reset();RebirthNpcRuntimeRegistry.Throws=true;Check(!RebirthNpcSpawnIdentity.TryBind(1,"survivor.ambient",out id,out reason),"throw escaped");Reset();Check(!RebirthNpcSpawnIdentity.TryBind(0,"survivor.ambient",out id,out reason),"invalid entity accepted");Check(!RebirthNpcSpawnIdentity.TryBind(1," ",out id,out reason),"empty profile accepted");return "PASS "+checks+" actual spawn identity helper checks with explicit registry/stable-ID doubles; native spawn/persistence not exercised";}
}