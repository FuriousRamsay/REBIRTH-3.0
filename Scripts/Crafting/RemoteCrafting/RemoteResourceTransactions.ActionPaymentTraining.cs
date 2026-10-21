using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private const string ActionTrainingPrefix="electrical-action:";
    private static string ActionTrainingReceipt(Guid operation)=>ActionTrainingPrefix+operation.ToString("N");
    private static XElement ActionTrainingImage(Guid operation,float raw,bool required=true)=>new XElement("training",
        new XAttribute("receipt",ActionTrainingReceipt(operation)),new XAttribute("raw",raw),new XAttribute("required",required));
    private static float ReadActionTraining(XElement original,Guid operation)
        =>ReadActionTraining(original,operation,out _);
    private static float ReadActionTraining(XElement original,Guid operation,out bool required)
    {
        required=false;
        var rows=original?.Elements("training").ToArray();
        if(rows==null||rows.Length!=1||rows[0].HasElements||rows[0].Attributes().Count()!=3||
            (string)rows[0].Attribute("receipt")!=ActionTrainingReceipt(operation))throw new InvalidDataException("Original electrical training is unavailable");
        float raw=(float)rows[0].Attribute("raw");
        required=(bool)rows[0].Attribute("required");
        if(float.IsNaN(raw)||float.IsInfinity(raw)||raw<0f||raw>10f||!required&&raw!=0f)throw new InvalidDataException("Original electrical training amount is invalid");
        return raw;
    }
    private static bool CompleteActionTraining(ActionPayment payment)
    {
        if(payment?.TargetCurrent()!=true||payment.State!=ActionPaymentState.Paid||!payment.PublicationReturned||!payment.ServiceAfterSaved||!payment.TargetAfterSaved||
            !payment.OwnerAfterSaved||payment.Legs.Any(leg=>!leg.AfterSaved))return false;
        if(!payment.TrainingRequired){ActionRecord(payment,"training-not-required",ActionTrainingImage(payment.Operation,0f,false));return true;}
        if(payment.Events.ContainsKey("training-saved"))return true;
        long now=System.Diagnostics.Stopwatch.GetTimestamp();
        if(now<payment.TrainingNextAttempt)return false;
        payment.TrainingNextAttempt=now+System.Diagnostics.Stopwatch.Frequency;
        var detail=ActionTrainingImage(payment.Operation,payment.TrainingRaw);
        ActionRecord(payment,"training-intent",detail);
        // A retry enters only the award service's original durable receipt. It
        // cannot repeat native payment or publish the electrical target again.
        if(!RebirthPhase9TechnicalTrainingService.AwardElectricalOriginal(payment.Actor,payment.TrainingRaw,
            payment.ServiceCandidate.ClrIdx+":"+payment.ServiceCandidate.Position.x+":"+payment.ServiceCandidate.Position.y+":"+payment.ServiceCandidate.Position.z,
            ActionTrainingReceipt(payment.Operation)))return false;
        if(!payment.TargetCurrent()||!HasActionTrainingReceipt(payment))return false;
        ActionRecord(payment,"training-saved",detail);return true;
    }

    private static bool HasActionTrainingReceipt(ActionPayment payment)
    {
        return ThreadManager.IsMainThread()&&payment!=null&&payment.Save==Path.GetFullPath(GameIO.GetSaveGameDir())&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(payment.Actor,out var owner)&&owner.CanonicalId==payment.Account&&
            RebirthCamperCharacterAccess.CurrentCreation(payment.Actor)==payment.Creation&&
            RebirthWorldCharacterRepository.HasSavedElectricalActionTraining(owner,payment.Creation,payment.Operation);
    }

    // Receipt pruning may run before any cold action has been reconstructed.
    // Only an exact saved terminal original can release its award receipt.
    private static ISet<string> ProtectActionTrainingReceipts(HashSet<string> receipts)
    {
        var protectedIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(string receipt in receipts)
        {
            if(receipt==null||!receipt.StartsWith(ActionTrainingPrefix,StringComparison.Ordinal))continue;
            protectedIds.Add(receipt);
            if(!ThreadManager.IsMainThread()||!Guid.TryParseExact(receipt.Substring(ActionTrainingPrefix.Length),"N",out var operation))continue;
            try
            {
                lock(actionOwnerGate)if(actionPayments.Any(payment=>payment.Operation==operation&&!payment.Terminal))continue;
                string directory=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","ActionPayments",operation.ToString("N"));
                string path=Path.Combine(directory,"current.xml");ActionPath(path);
                if(File.Exists(path+".tmp"))continue;
                long size=new FileInfo(path).Length;
                if(size<1||size>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*size+65536),out var memory))continue;
                using(memory)
                {
                    byte[] bytes=ActionBytes(path,(int)size);var head=ReadActionBoundedXml(bytes,24*1024*1024);
                    int sequence=(int?)head.Attribute("sequence")??-1;
                    if(head.Name!="actionPaymentEvent"||(int?)head.Attribute("version")!=1||(string)head.Attribute("stage")!="used"||sequence<0||sequence>=256)continue;
                    string originalText=head.Element("original")?.Value;var original=ReadRemoteActionOriginal(originalText);
                    if(original.Name!="resourceActionOriginal"||(string)original.Attribute("operation")!=operation.ToString("N")||
                        (string)original.Attribute("save")!=Path.GetFullPath(GameIO.GetSaveGameDir()))continue;
                    ReadActionTraining(original,operation,out bool trainingRequired);
                    if(!trainingRequired)continue;
                    string terminal=Path.Combine(directory,sequence.ToString("D6")+"-used.xml");
                    if(!ActionBytes(terminal,bytes.Length).SequenceEqual(bytes))continue;
                    protectedIds.Remove(receipt);
                }
            }
            catch{/* Missing, replaced or unknown original is retained custody. */}
        }
        return protectedIds;
    }
}
