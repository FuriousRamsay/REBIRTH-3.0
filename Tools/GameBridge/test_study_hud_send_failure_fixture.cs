using System;
enum RebirthStudyHudMode:byte {None,Reading,Audiobook}
class EntityPlayer {public int entityId=7;}
class EntityPlayerLocal:EntityPlayer {}
class SingletonMonoBehaviour<T>{public static T Instance;}
class ConnectionManager {
 public bool IsServer=true,ThrowSend;public int Sent,Owner;public ulong Sequence;
 public void SendPackage(NetPackageRebirthStudyHudSnapshot p,int _attachedToEntityId){
  if(ThrowSend)throw new Exception("send");Sent++;Owner=_attachedToEntityId;Sequence=p.Sequence;
 }
}
class NetPackageManager {
 public static bool ThrowFactory,ReturnNull;
 public static T GetPackage<T>()where T:class,new(){if(ThrowFactory)throw new Exception("factory");return ReturnNull?null:new T();}
}
class NetPackageRebirthStudyHudSnapshot {
 public static bool ThrowSetup;public ulong Sequence;
 public NetPackageRebirthStudyHudSnapshot Setup(RebirthStudyHudMode m,string id,float p,float r,bool slow,ulong seq){
  if(ThrowSetup)throw new Exception("setup");Sequence=seq;return this;
 }
}
class Check {
 static ulong nextSequence;
 // SOURCE
 static void Main(){
  var c=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=c;
  var owner=new EntityPlayer();
  Send(null,RebirthStudyHudMode.Reading,"book",.5f,10,true);
  Send(new EntityPlayerLocal(),RebirthStudyHudMode.Reading,"book",.5f,10,true);
  c.IsServer=false;Send(owner,RebirthStudyHudMode.Reading,"book",.5f,10,true);c.IsServer=true;
  if(c.Sent!=0||nextSequence!=0)throw new Exception("authority guard");
  for(int failure=0;failure<4;failure++){
   NetPackageManager.ThrowFactory=failure==0;NetPackageManager.ReturnNull=failure==1;
   NetPackageRebirthStudyHudSnapshot.ThrowSetup=failure==2;c.ThrowSend=failure==3;
   int before=c.Sent;Send(owner,RebirthStudyHudMode.Reading,"book",.5f,10,true);
   if(c.Sent!=before)throw new Exception("failed send");
   NetPackageManager.ThrowFactory=false;NetPackageManager.ReturnNull=false;
   NetPackageRebirthStudyHudSnapshot.ThrowSetup=false;c.ThrowSend=false;
   Send(owner,RebirthStudyHudMode.Audiobook,"book",.6f,8,false);
   if(c.Sent!=before+1||c.Owner!=7||c.Sequence!=nextSequence)throw new Exception("recovery");
  }
  ulong last=c.Sequence;Send(owner,RebirthStudyHudMode.None,"",0,0,false);
  if(c.Sequence<=last)throw new Exception("nonmonotonic clear");
  Console.WriteLine("PASS actual HUD Send: authority, factory/null/setup/send containment, owner recovery and monotonic clear");
 }
}