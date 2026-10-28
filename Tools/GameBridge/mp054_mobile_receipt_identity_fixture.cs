// AUTHORED_NOT_RUN. Actual complete mobile receipt identity gate. Native RCJ
// producer, saved owner and vehicle authority remain separate requirements.
using System;using System.Linq;
class RebirthCamperTransferJournal{internal class Record{internal Guid Operation,Vehicle;internal string OwnerCreation;internal byte[] Before,After;}}
static class RebirthProductiveEarnedMobileOrigin{internal static byte[] Digest(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return sha.ComputeHash(bytes);}}
internal static partial class RebirthProductiveEarnedCoordinator{
 internal class MobileRemoteTools{internal Guid Operation;internal string Account,Creation;internal byte[] TransferBefore,TransferAfter;}
 static MobileRemoteTools source;static RebirthCamperTransferJournal.Record receipt;static Guid vehicle;static int checks;
 static void Fresh(){vehicle=Guid.NewGuid();source=new MobileRemoteTools{Operation=Guid.NewGuid(),Account="owner",Creation="creation",TransferBefore=RebirthProductiveEarnedMobileOrigin.Digest(new byte[]{1}),TransferAfter=RebirthProductiveEarnedMobileOrigin.Digest(new byte[]{2})};receipt=new RebirthCamperTransferJournal.Record{Operation=source.Operation,Vehicle=vehicle,OwnerCreation="remote:owner:creation",Before=new byte[]{1},After=new byte[]{2}};}
 static void Need(bool value){if(!value)throw new Exception("Mobile original receipt "+checks);checks++;}
 public static void Main(){Fresh();Need(MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();receipt.Operation=Guid.NewGuid();Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();receipt.Vehicle=Guid.NewGuid();Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();receipt.OwnerCreation="remote:other:creation";Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();receipt.Before=new byte[]{3};Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();receipt.After=new byte[]{3};Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();Need(!MatchesMobileRemoteToolReceipt(null,receipt,vehicle));Need(!MatchesMobileRemoteToolReceipt(source,null,vehicle));
  Fresh();Need(!MatchesMobileRemoteToolReceipt(source,receipt,Guid.Empty));
  Fresh();receipt.Before=null;Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();source.Operation=receipt.Operation=Guid.Empty;Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Fresh();source.Account="";Need(!MatchesMobileRemoteToolReceipt(source,receipt,vehicle));
  Console.WriteLine("PASS "+checks+" actual complete mobile receipt identity assertions; native producer excluded");}
}
// ACTUAL_MOBILE_RECEIPT
