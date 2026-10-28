// AUTHORED_NOT_RUN. Complete production filename classifier. Actual immutable
// records, owner proofs and native loads remain separate production checks.
using System;
internal static partial class RebirthProductiveEarnedCoordinator{
 public static void Main(){int checks=0;Action<bool> need=value=>{if(!value)throw new Exception("Mobile tools files "+checks);checks++;};
  string prefix="original.mobile-remote-tools.";var id=Guid.NewGuid();string primary=prefix+id.ToString("N");
  need(TryClassifyMobileRemoteToolFile(prefix,primary,out var operation,out bool companion)&&operation==id&&!companion);
  foreach(string suffix in new[]{".owner",".saved",".reentry",".reentry.invoking"})need(TryClassifyMobileRemoteToolFile(prefix,primary+suffix,out operation,out companion)&&operation==id&&companion);
  need(!TryClassifyMobileRemoteToolFile(prefix,primary+".reentry.invoking.tmp",out _,out _));
  need(!TryClassifyMobileRemoteToolFile(prefix,primary+".other",out _,out _));
  need(!TryClassifyMobileRemoteToolFile(prefix,"foreign."+id.ToString("N"),out _,out _));
  need(!TryClassifyMobileRemoteToolFile(prefix,prefix+Guid.Empty.ToString("N"),out _,out _));
  need(!TryClassifyMobileRemoteToolFile(prefix,null,out _,out _));
  need(!TryClassifyMobileRemoteToolFile(prefix,prefix+"123",out _,out _));
  Console.WriteLine("PASS "+checks+" actual mobile tools file classification assertions; native source excluded");}
}
// ACTUAL_TOOL_FILES
