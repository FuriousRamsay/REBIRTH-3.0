using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceBlockRejection
{
    internal static string IssuedProof(RebirthRemoteResourceClientOriginal.Intent original,string binding,XElement action)
        =>RebirthRemoteResourceDurableJournal.Digest("native-block-issued-never-entered|"+original.Digest+"|"+binding+"|"+action.ToString(SaveOptions.DisableFormatting));
    private static RebirthRemoteResourceClientOriginalJournal IssuedJournal(string name)=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name));
    private static XElement IssuedBody(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,XElement action,bool observed)
        =>new XElement(observed?"blockIssuedNoUse":"blockIssuedCancellation",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),
            new XAttribute("binding",root.Binding),new XAttribute("nativePhase",observed?5:1),RebirthRemoteResourceBlockOriginal.PreparedRejectionBody(original,action));
    private static bool SaveIssued(string name,RebirthRemoteResourceClientOriginal.Intent original,XElement body)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>24*1024*1024+4096)return false;var journal=IssuedJournal(name);
        var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static bool ReadIssued(string name,RebirthRemoteResourceClientOriginal.Intent original,XElement expected)
    {
        var record=IssuedJournal(name).Read(original.Operation);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==original.Operation&&record.OwnerCreation==original.Creation&&record.Revision==0&&
            record.Before.Length<=24*1024*1024+4096&&record.Before.SequenceEqual(record.After)&&record.Before.SequenceEqual(Encoding.UTF8.GetBytes(expected.ToString(SaveOptions.DisableFormatting)));
    }
    internal static bool ValidateIssuedRootProof(XElement image)
    {
        try
        {
            if(!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var source)||!RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(source,out var original))return false;
            var root=new RebirthRemoteResourceDurableJournal.Record(source,0,image);
            return root.Owner==RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner)&&root.Creation==original.Creation&&
                root.Operation==RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,original.Creation,original.Epoch,original.Request,original.Kind)&&
                (int?)image.Attribute("deaths")==original.Deaths&&(int?)image.Attribute("kind")== (int)original.Kind&&
                RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root)&&RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action)&&
                RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original)&&ReadIssued("RemoteBlockIssuedCancellation",original,IssuedBody(original,root,action,false))&&
                ReadIssued("RemoteBlockIssuedNoUse",original,IssuedBody(original,root,action,true))&&(string)image.Attribute("ownerNoUseProof")==IssuedProof(original,root.Binding,action);
        }
        catch{return false;}
    }
    internal static bool RejectIssued(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,XElement localPreparedRejection,bool observe,out string proof)
    {
        proof=null;
        try
        {
            if(!Read(actor,original,out var identity,out var root)||root.Phase!=2||!RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root)||
                !RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action)||!XNode.DeepEquals(localPreparedRejection,RebirthRemoteResourceBlockOriginal.PreparedRejectionBody(original,action))||
                !RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value).Matches(actual)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present)return false;
            bool Current()=>Read(actor,original,out _,out var current)&&current.Operation==root.Operation&&current.Binding==root.Binding&&current.Phase==2&&
                RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original)&&RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,current);
            if(!observe)
            {
                if(phase!=1f)return false;
                if(!SaveIssued("RemoteBlockIssuedCancellation",original,IssuedBody(original,root,action,false)))return false;
                if((int?)root.Copy().Attribute("ownerRejecting")==1)return true;
                return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.SetAttributeValue("ownerRejecting",1),Current,out _);
            }
            if(phase!=5f||(int?)root.Copy().Attribute("ownerRejecting")!=1||!ReadIssued("RemoteBlockIssuedCancellation",original,IssuedBody(original,root,action,false))||
                !SaveIssued("RemoteBlockIssuedNoUse",original,IssuedBody(original,root,action,true)))return false;
            string expected=IssuedProof(original,root.Binding,action);
            if(root.Copy().Attribute("ownerNoUseProof")!=null){proof=(string)root.Copy().Attribute("ownerNoUseProof");return proof==expected;}
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.SetAttributeValue("ownerNoUseProof",expected),Current,out _))return false;
            proof=expected;return true;
        }
        catch{proof=null;return false;}
    }
}
