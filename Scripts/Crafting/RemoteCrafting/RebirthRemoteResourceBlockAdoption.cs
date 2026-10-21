using System;
using System.Linq;
using System.Xml.Linq;

// Existing Block original settlement after its owner life ended. It cannot
// issue another block command, old actor inventory or new source withdrawal.
internal static partial class RebirthRemoteResourceBlockAdoption
{
    internal static bool QueryScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
        =>RebirthRemoteResourceClientOriginal.Current(player,original)||original!=null&&
            (original.Kind==RemoteResourceClientOperation.BlockRepair||original.Kind==RemoteResourceClientOperation.BlockUpgrade)&&
            player?.IsSpawned()==true&&!player.IsDead()&&player.Died>original.Deaths&&RebirthRemoteResourceBlockOriginal.TerminalScope(player,original);
    internal static bool HasSavedAdmission(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
    {
        try{return ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&original!=null&&root!=null&&
            (original.Kind==RemoteResourceClientOperation.BlockRepair||original.Kind==RemoteResourceClientOperation.BlockUpgrade)&&
            actor.IsSpawned()&&!actor.IsDead()&&actor.Died>=original.Deaths&&actor.world.worldState.Guid==original.World&&
            ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==original.Owner&&root.Owner==owner.StorageKey&&
            RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)&&RemoteResourceWorldIdentity.TryGet(actor.world,out string world)&&world==root.World&&
            root.Creation==original.Creation&&(int)root.Copy().Attribute("deaths")==original.Deaths&&(int)root.Copy().Attribute("kind")==(int)original.Kind&&
            root.Operation==RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)&&
            RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(root.Operation,out var saved)&&saved.Digest==original.Digest&&
            XNode.DeepEquals(saved.Copy(),original.Copy())&&RebirthRemoteResourceBlockAdmission.HasSavedTarget(original);}
        catch{return false;}
    }
    internal static bool Read(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthRemoteResourceDurableJournal.Record result)
    {
        result=null;
        try
        {
            if(actor==null||original==null||actor.Died<=original.Deaths||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,operation,out var root,out bool unknown)||unknown||!HasSavedAdmission(actor,original,root))return false;
            if(root.HasOwnerAction)
            {
                var saved=root.Copy();
                if(saved.Attribute("ownerNoUseProof")!=null&&(RebirthRemoteResourceBlockRejection.ValidateIssuedRootProof(saved)||RebirthRemoteResourceBlockRejection.ValidateDeclinedRootProof(saved)))
                {result=root;return true;}
                if(saved.Attribute("ownerNoUseProof")!=null&&ValidateEndedOwnerRoot(saved)){result=root;return true;}
                if(TryAdoptSavedEndedOwner(actor,original,root,out result))return true;
                if(root.Phase!=2||!RebirthRemoteResourceBlockCommand.TryReadDeclined(original,root,out string declined)||
                    !RebirthRemoteResourceBlockCommand.TryRead(original,root,out _)||!RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root))return false;
                string noUse=RebirthRemoteResourceDurableJournal.Digest("native-block-declined-unused|"+original.Digest+"|"+root.Binding+"|"+declined);
                bool DeclineCurrent()=>HasSavedAdmission(actor,original,root)&&actor.Died>original.Deaths&&RebirthRemoteResourceBlockCommand.TryReadDeclined(original,root,out var actual)&&actual==declined;
                return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("ownerRejecting",1);value.SetAttributeValue("ownerNoUseProof",noUse);},DeclineCurrent,out result);
            }
            if(!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root))return false;
            if(root.Phase<=1)
            {
                if(RebirthRemoteResourceDurableOutcome.TryReconcileBlockSources(actor,original,root,out var next)&&next!=null)root=next;
                result=root;return true; // One existing source metadata/effect checkpoint per query.
            }
            if(root.Phase==3&&RebirthRemoteResourceDurableOutcome.IsRestoredBlockRefusal(root))
            {
                if(RebirthRemoteResourceDurableOutcome.AcknowledgeBlockNoPayment(actor,original,root.Operation,root.Binding)&&
                    RebirthRemoteResourceDurableJournal.TryRead(root.Owner,root.Operation,out var terminal,out unknown)&&!unknown&&terminal!=null)root=terminal;
                result=root;return true;
            }
            var body=root.Copy();
            if(root.Phase==4){result=root;return true;}
            bool paid=root.Phase==2&&(int)body.Attribute("outcome")==1||root.Phase==3&&(int)body.Attribute("outcome")==2;
            if(!paid||RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("removed").Value)).Length==0||
                body.Element("sources").Elements("source").Any(source=>(int)source.Attribute("attempted")!=0&&
                    ((int)source.Attribute("published")!=1||((int?)source.Attribute("restored")??0)!=0)))return false;
            string proof=RebirthRemoteResourceBlockRejection.UnissuedProof(original,root.Binding);
            if(body.Attribute("ownerNoUseProof")!=null)
            {if((string)body.Attribute("ownerNoUseProof")!=proof||(int?)body.Attribute("ownerRejecting")!=1)return false;result=root;return true;}
            bool Current()=>HasSavedAdmission(actor,original,root)&&actor.Died>original.Deaths&&
                RebirthRemoteResourceDurableJournal.TryRead(root.Owner,root.Operation,out var live,out bool unavailable)&&!unavailable&&live!=null&&
                live.Revision==root.Revision&&live.Binding==root.Binding&&!live.HasOwnerAction&&RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(live);
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("ownerRejecting",1);value.SetAttributeValue("ownerNoUseProof",proof);},Current,out result);
        }
        catch{result=null;return false;}
    }
}
