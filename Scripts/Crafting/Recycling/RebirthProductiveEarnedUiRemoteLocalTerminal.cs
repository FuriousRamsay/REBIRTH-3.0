using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiRemote
{
    internal static bool AcceptLocalOwnerTerminal(RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication proof)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||proof?.Current()!=true||proof.World?.IsRemote()!=false||
                !ReferenceEquals(GameManager.Instance?.World,proof.World))return false;
            var contributions=proof.Contributions;
            if(contributions==null||contributions.Length>4||contributions.Distinct().Count()!=contributions.Length)return false;
            var receipts=new System.Collections.Generic.Dictionary<Registration,byte[]>();
            var cached=new System.Collections.Generic.List<RebirthWorkstationCompletionPipeline.CachedHostOriginal>();
            var loaded=new System.Collections.Generic.List<RebirthProductiveEarnedUiDeferredTransport.AuthorityLocalLoadedSource>();
            foreach(var source in contributions)
            {
                if(source?.AuthorityLoadedOriginal!=null)
                {
                    if(!proof.IncludesLoadedAuthorityOriginal(source.AuthorityLoadedOriginal)||!source.TrySealStation())return false;
                    loaded.Add(source.AuthorityLoadedOriginal);continue;
                }
                if(source?.CachedOriginal!=null)
                {
                    if(!proof.IncludesLoadedCachedOriginal(source.CachedOriginal)||!source.TrySealStation())return false;
                    cached.Add(source.CachedOriginal);continue;
                }
                if(source==null||!source.TrySealStation()||!ReferenceEquals(source.World,proof.World)||!ReferenceEquals(source.Station,proof.Station)||
                    !registrations.TryGetValue(source.Operation,out var original)||!ReferenceEquals(source.Original,original)||!ReferenceEquals(original.Deferred,source)||
                    original.Nonce!=source.Nonce||original.Frame.Birth!=source.Birth||original.StationPublication?.Current()!=true)return false;
                var receipt=new XElement("uiNativeLocalOwnerTerminalSaved",new XAttribute("operation",source.Operation.ToString("N")),
                    new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("birth",source.Birth.ToString("N")),
                    new XAttribute("group",proof.Operation.ToString("N")),new XAttribute("uio",original.Hash),new XAttribute("nativeOwnerTerminal",proof.Path));
                receipts.Add(original,Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting)));
            }
            // Freeze every group's immutable member receipt before retiring
            // any original. A partial filesystem failure remains retained.
            foreach(var member in receipts)if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(member.Key.Path+".TerminalSaved",member.Value))return false;
            foreach(var source in cached)if(!source.TrySaveLocalOwnerTerminal(proof))return false;
            foreach(var source in loaded)if(!source.SaveTerminal(proof))return false;
            if(!proof.Current())return false;
            foreach(var member in receipts)member.Key.TerminalReceipt=member.Value;
            foreach(var member in receipts)
            {
                var original=member.Key;if(original.Completed)continue;
                if(!RebirthProductiveEarnedRemoteHost.RetireUiTerminal(new TerminalPublication(terminalIssuer,original,proof)))return false;
                original.Completed=true;original.Memory?.RetainCompletedAllocation();RebirthProductiveEarnedUiRemoteBefore.Release(original.Frame.Operation);
            }
            return true;
        }
        catch{return false;}
    }
}

