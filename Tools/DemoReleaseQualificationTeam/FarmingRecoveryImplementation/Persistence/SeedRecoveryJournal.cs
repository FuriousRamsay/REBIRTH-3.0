using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

public enum SeedRecoveryEffect { PredictionCorrection, DebitCompensation }
public enum SeedRecoveryPhase { Retained, SettlementReserved, Consumed }
// Disk DTO does not reconstruct ephemeral native custody identities or authority.
public sealed class SeedRecoveryRecord
{
    public int Version {get;set;}=1;
    public Guid WorldId {get;set;} public Guid CharacterId {get;set;}
    public ulong Epoch {get;set;} public ulong Nonce {get;set;}
    public int Actor {get;set;} public int Slot {get;set;} public int OriginalCount {get;set;}
    public byte Flags {get;set;} public sbyte Density {get;set;} public long Texture {get;set;}
    public int X {get;set;} public int Y {get;set;} public int Z {get;set;}
    public uint TargetRaw {get;set;} public int TargetDamage {get;set;}
    public uint OldRaw {get;set;} public int OldDamage {get;set;}
    public byte[] Seed {get;set;}
    public SeedDebitState Debit {get;set;}
    public SeedPlacementOutcome Outcome {get;set;}=SeedPlacementOutcome.Unknown;
    public bool NoWorldEffectProven {get;set;} public bool PartialMutation {get;set;}
    public SeedRecoveryPhase Phase {get;set;}=SeedRecoveryPhase.Retained;
    public Guid ReservationId {get;set;}
    public SeedRecoveryPhase CorrectionPhase {get;set;}=SeedRecoveryPhase.Retained;
    public Guid CorrectionReservation {get;set;}
    public bool Conflict {get;set;}
}
public interface ISeedRecoveryOwnerAdmission
{
    // MUST independently validate current native session/context/receipt and full original command.
    // Historical disk record and matching byte arrays alone never authorize reconnect effects.
    bool Current(SeedRecoveryRecord retained, SeedDebitReceipt liveReceipt);
    bool AcceptConflict(SeedRecoveryRecord retained, SeedDebitReceipt liveReceipt);
    bool CanCorrectPrediction(SeedRecoveryRecord retained, SeedDebitReceipt liveReceipt);
    bool AcceptEvidence(SeedRecoveryRecord retained, SeedDebitReceipt liveReceipt, SeedPlacementOutcome outcome, bool noWorldEffect, bool partialMutation);
}
public sealed class SeedRecoveryJournal
{
    private readonly string directory;
    private readonly object gate=new object();
    private const int MaxDiskBytes=32768;
    [DataContract] private sealed class Envelope {[DataMember] public string Payload {get;set;} [DataMember] public string Hash {get;set;}}
    public SeedRecoveryJournal(string ownDirectory)
    { directory=Path.GetFullPath(ownDirectory??throw new ArgumentNullException(nameof(ownDirectory)));Directory.CreateDirectory(directory); }
    private static void Valid(SeedRecoveryRecord r)
    {
        if(r==null || r.Version!=1 || r.WorldId==Guid.Empty || r.CharacterId==Guid.Empty || r.Epoch==0 || r.Nonce==0 ||
            r.Actor<=0 || r.Slot<0 || r.OriginalCount<=0 || r.OriginalCount>1000000 || (r.Flags!=0x15 && r.Flags!=0x11 && r.Flags!=0x21) ||
            ((r.Flags & 4)==0 && r.Density!=0) || ((r.Flags & 32)==0 && r.Texture!=0) || r.Seed==null || r.Seed.Length==0 || r.Seed.Length>8192 ||
            !Enum.IsDefined(typeof(SeedDebitState),r.Debit) || !Enum.IsDefined(typeof(SeedPlacementOutcome),r.Outcome) ||
            !Enum.IsDefined(typeof(SeedRecoveryPhase),r.Phase) || !Enum.IsDefined(typeof(SeedRecoveryPhase),r.CorrectionPhase) ||
            (r.CorrectionPhase==SeedRecoveryPhase.Retained && r.CorrectionReservation!=Guid.Empty) ||
            (r.CorrectionPhase!=SeedRecoveryPhase.Retained && r.CorrectionReservation==Guid.Empty) ||
            (r.Phase==SeedRecoveryPhase.Retained && r.ReservationId!=Guid.Empty) ||
            (r.Phase!=SeedRecoveryPhase.Retained && r.ReservationId==Guid.Empty))throw new InvalidDataException("Invalid retained seed record.");
    }
    private string FileFor(Guid w,Guid c,ulong epoch,ulong nonce)
    {if(w==Guid.Empty||c==Guid.Empty||epoch==0||nonce==0)throw new ArgumentException("Invalid retained key.");return Path.Combine(directory,w.ToString("N")+"_"+c.ToString("N")+"_"+epoch+"_"+nonce+".json");}
    private static string Hash(string value)=>PortableHash(value);
    private static string PortableHash(string value)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
    private static string Encode<T>(T value)
    {using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
    private static T Decode<T>(string value)
    {using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(value)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
    private static SeedRecoveryRecord Copy(SeedRecoveryRecord r)=>Decode<SeedRecoveryRecord>(Encode(r));
    private SeedRecoveryRecord Read(string path)
    {
        if(!File.Exists(path))return null;
        var info=new FileInfo(path);if(info.Length<=0||info.Length>MaxDiskBytes)throw new InvalidDataException("Oversize retained record.");
        var e=Decode<Envelope>(File.ReadAllText(path));
        if(e==null||e.Payload==null||e.Hash!=Hash(e.Payload))throw new InvalidDataException("Retained checksum mismatch.");
        var r=Decode<SeedRecoveryRecord>(e.Payload);Valid(r);
        if(!string.Equals(path,FileFor(r.WorldId,r.CharacterId,r.Epoch,r.Nonce),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Retained key mismatch.");
        return r;
    }
    private void Write(string path,SeedRecoveryRecord r)
    {
        Valid(r);string payload=Encode(r);byte[] data=Encoding.UTF8.GetBytes(Encode(new Envelope{Payload=payload,Hash=Hash(payload)}));
        if(data.Length>MaxDiskBytes)throw new InvalidDataException("Retained record exceeds bound.");
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        // Flush file before atomic same-directory replacement. Directory fsync/platform powerloss remains unqualified.
        using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
        {stream.Write(data,0,data.Length);stream.Flush(true);}
        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
    private FileStream Lease()=>new FileStream(Path.Combine(directory,"journal.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    private static bool SameCommand(SeedRecoveryRecord a,SeedRecoveryRecord b)
    {var x=Copy(a);var y=Copy(b);x.Debit=y.Debit=SeedDebitState.Captured;x.Outcome=y.Outcome=SeedPlacementOutcome.Unknown;
     x.NoWorldEffectProven=y.NoWorldEffectProven=false;x.Conflict=y.Conflict=false;x.PartialMutation=y.PartialMutation=false;x.Phase=y.Phase=SeedRecoveryPhase.Retained;
     x.ReservationId=y.ReservationId=Guid.Empty;x.CorrectionPhase=y.CorrectionPhase=SeedRecoveryPhase.Retained;x.CorrectionReservation=y.CorrectionReservation=Guid.Empty;return Encode(x)==Encode(y);}
    private static bool Matches(SeedRecoveryRecord r,SeedDebitReceipt receipt)
    {if(receipt==null||r.Epoch!=receipt.Epoch||r.Nonce!=receipt.Nonce||r.Slot!=receipt.OriginalSlot||r.OriginalCount!=receipt.OriginalCount)return false;
     byte[] seed=receipt.CanonicalSeed;if(seed.Length!=r.Seed.Length)return false;for(int i=0;i<seed.Length;i++)if(seed[i]!=r.Seed[i])return false;return true;}
    public SeedRecoveryRecord Find(Guid world,Guid character,ulong epoch,ulong nonce)
    {lock(gate){using(var lease=Lease())return CopyOrNull(Read(FileFor(world,character,epoch,nonce)));}}
    private static SeedRecoveryRecord CopyOrNull(SeedRecoveryRecord r)=>r==null?null:Copy(r);
    public bool Retain(SeedRecoveryRecord original,SeedDebitReceipt liveReceipt)
    {
        lock(gate){using(var lease=Lease()){
            Valid(original);if(original.Phase!=SeedRecoveryPhase.Retained || original.Outcome!=SeedPlacementOutcome.Unknown || original.NoWorldEffectProven || original.PartialMutation || original.Conflict || !Matches(original,liveReceipt) || original.Debit!=liveReceipt.State)return false;
            var path=FileFor(original.WorldId,original.CharacterId,original.Epoch,original.Nonce);var existing=Read(path);
            if(existing!=null)return SameCommand(existing,original); // Never rewrite settlement or unknown custody on duplicate.
            Write(path,Copy(original));return true;
        }}
    }
    public bool RecordEvidence(Guid world,Guid character,ulong epoch,ulong nonce,SeedDebitReceipt liveReceipt,
        SeedPlacementOutcome outcome,bool noWorldEffect,bool partial,ISeedRecoveryOwnerAdmission owner)
    {
        lock(gate){using(var lease=Lease()){
            var path=FileFor(world,character,epoch,nonce);var r=Read(path);
            if(r==null || r.Conflict || r.Phase!=SeedRecoveryPhase.Retained || !Matches(r,liveReceipt) || owner==null || !owner.Current(Copy(r),liveReceipt))return false;
            if((noWorldEffect && (outcome!=SeedPlacementOutcome.RejectedBeforeMutation || partial)) ||
                (outcome==SeedPlacementOutcome.RejectedBeforeMutation && !noWorldEffect) ||
                !Enum.IsDefined(typeof(SeedPlacementOutcome),outcome) || !owner.AcceptEvidence(Copy(r),liveReceipt,outcome,noWorldEffect,partial))return false;
            // Contradictory/uncertain data may only tighten custody; never overwrite an already proven terminal outcome.
            if(r.Outcome!=SeedPlacementOutcome.Unknown && (outcome!=r.Outcome || partial))return false;
            if(r.Debit==SeedDebitState.HeldUncertain && liveReceipt.State!=SeedDebitState.HeldUncertain)return false;
            if(r.Debit==SeedDebitState.ExactDebitObserved && liveReceipt.State!=SeedDebitState.ExactDebitObserved)return false;
            r.Debit=liveReceipt.State;r.Outcome=outcome;r.NoWorldEffectProven=noWorldEffect;r.PartialMutation|=partial;
            Write(path,r);return true;
        }}
    }
    // Authenticated contradictory ACK latches hold while preserving original terminal/effect history.
    public bool RecordConflict(Guid world,Guid character,ulong epoch,ulong nonce,SeedDebitReceipt liveReceipt,ISeedRecoveryOwnerAdmission owner)
    {
        lock(gate){using(var lease=Lease()){
            var path=FileFor(world,character,epoch,nonce);var r=Read(path);
            if(r==null || !Matches(r,liveReceipt) || owner==null || !owner.AcceptConflict(Copy(r),liveReceipt))return false;
            if(r.Conflict)return true;r.Conflict=true;Write(path,r);return true;
        }}
    }
    public bool TryReserveSettlement(Guid world,Guid character,ulong epoch,ulong nonce,SeedDebitReceipt liveReceipt,
        ISeedRecoveryOwnerAdmission owner,out Guid reservation)
    {
        reservation=Guid.Empty;
        lock(gate){using(var lease=Lease()){
            var path=FileFor(world,character,epoch,nonce);var r=Read(path);
            if(r==null || r.Conflict || r.Phase!=SeedRecoveryPhase.Retained || r.CorrectionPhase!=SeedRecoveryPhase.Consumed || r.Debit!=SeedDebitState.ExactDebitObserved || !Matches(r,liveReceipt) ||
                owner==null || !owner.Current(Copy(r),liveReceipt) || liveReceipt.Resolve(r.Outcome,r.NoWorldEffectProven,r.PartialMutation).Decision!=SeedDebitDecision.RefundEligible)return false;
            r.Phase=SeedRecoveryPhase.SettlementReserved;r.ReservationId=Guid.NewGuid();Write(path,r);reservation=r.ReservationId;return true;
        }}
    }
    // Separate claim never grants debit compensation. Owner proves exact unchanged prediction and no future setter.
    public bool TryReserveCorrection(Guid world,Guid character,ulong epoch,ulong nonce,SeedDebitReceipt liveReceipt,
        ISeedRecoveryOwnerAdmission owner,out Guid reservation)
    {
        reservation=Guid.Empty;
        lock(gate){using(var lease=Lease()){
            var path=FileFor(world,character,epoch,nonce);var r=Read(path);
            if(r==null || r.Conflict || r.CorrectionPhase!=SeedRecoveryPhase.Retained || r.Outcome!=SeedPlacementOutcome.RejectedBeforeMutation ||
                !r.NoWorldEffectProven || r.PartialMutation || r.Debit==SeedDebitState.HeldUncertain || !Matches(r,liveReceipt) ||
                owner==null || !owner.Current(Copy(r),liveReceipt) || !owner.CanCorrectPrediction(Copy(r),liveReceipt))return false;
            r.CorrectionPhase=SeedRecoveryPhase.SettlementReserved;r.CorrectionReservation=Guid.NewGuid();Write(path,r);reservation=r.CorrectionReservation;return true;
        }}
    }
    // Mark completed AFTER owner's single native effect. Never automatic replay if crash occurs between phases.
    public bool CompleteReservation(Guid world,Guid character,ulong epoch,ulong nonce,Guid reservation,SeedRecoveryEffect kind=SeedRecoveryEffect.DebitCompensation)
    {
        lock(gate){using(var lease=Lease()){
            var path=FileFor(world,character,epoch,nonce);var r=Read(path);
            if(reservation==Guid.Empty||r==null||!Enum.IsDefined(typeof(SeedRecoveryEffect),kind))return false;
            if(kind==SeedRecoveryEffect.PredictionCorrection){if(r.CorrectionPhase!=SeedRecoveryPhase.SettlementReserved||r.CorrectionReservation!=reservation)return false;r.CorrectionPhase=SeedRecoveryPhase.Consumed;}
            else {if(r.Phase!=SeedRecoveryPhase.SettlementReserved||r.ReservationId!=reservation)return false;r.Phase=SeedRecoveryPhase.Consumed;}
            Write(path,r);return true;
        }}
    }
}






