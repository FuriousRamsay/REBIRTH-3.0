using System;
// AUTHORED_NOT_RUN. Actual local saved-owner predicate; native reader adapters.
internal sealed class EntityPlayerLocal {internal bool IdentityAvailable=true;}
internal sealed class RebirthStablePlayerIdentity {internal static bool TryResolveServerEntity(EntityPlayerLocal p,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return p.IdentityAvailable;}}
internal sealed class RebirthCamperEnergyActorImage {internal string Rich="rich-original";internal bool Matches(RebirthCamperEnergyActorImage other)=>other!=null&&Rich==other.Rich;}
internal sealed class PlayerFile {internal int deaths=2;}
internal static class RebirthCamperEnergySavedOwner {
 internal static bool Available=true;internal static PlayerFile File;internal static RebirthCamperEnergyActorImage Image;
 internal static bool TryRead(RebirthStablePlayerIdentity owner,out RebirthCamperEnergyActorImage image,out PlayerFile file){image=Image;file=File;return Available;}
}
internal static class RebirthCookingRemoteAdmissionBackend {
 internal static bool Available=true,Present=true;internal static float Phase=2;internal static string ExpectedReceipt;
 internal static bool NativePhase(PlayerFile file,string receipt,out float phase,out bool present){if(receipt!=ExpectedReceipt)throw new Exception("wrong receipt");phase=Phase;present=Present;return Available;}
}
internal static class RebirthCookingGridCustody {
 private sealed class Intent {internal EntityPlayerLocal Player=new EntityPlayerLocal();internal int Deaths=2;internal Guid Operation=Guid.NewGuid();}
 private static string Receipt(Guid operation)=>"rbCookingGrid_"+operation.ToString("N");
// ACTUAL_SAVED
 internal static void Exercise(){
  for(int variant=0;variant<8;variant++){
   var intent=new Intent();var expected=new RebirthCamperEnergyActorImage();
   RebirthCamperEnergySavedOwner.Available=true;RebirthCamperEnergySavedOwner.File=new PlayerFile();RebirthCamperEnergySavedOwner.Image=new RebirthCamperEnergyActorImage();
   RebirthCookingRemoteAdmissionBackend.Available=RebirthCookingRemoteAdmissionBackend.Present=true;RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthCookingRemoteAdmissionBackend.ExpectedReceipt=Receipt(intent.Operation);
   if(!Saved(intent,2,expected))throw new Exception("matching saved owner rejected");
   switch(variant){case 0:RebirthCamperEnergySavedOwner.File.deaths=1;break;case 1:RebirthCamperEnergySavedOwner.File.deaths=3;break;
    case 2:RebirthCamperEnergySavedOwner.Image.Rich="foreign-rich";break;case 3:RebirthCookingRemoteAdmissionBackend.Phase=1;break;
    case 4:RebirthCookingRemoteAdmissionBackend.Present=false;break;case 5:RebirthCookingRemoteAdmissionBackend.Available=false;break;
    case 6:RebirthCamperEnergySavedOwner.Available=false;break;case 7:intent.Player.IdentityAvailable=false;break;}
   if(Saved(intent,2,expected))throw new Exception("unqualified saved owner accepted "+variant);
  }
  Console.WriteLine("PASS actual saved cooking owner life/receipt/image predicate; native save and topology NOT RUN");
 }
}
internal static class Program {public static void Main()=>RebirthCookingGridCustody.Exercise();}
