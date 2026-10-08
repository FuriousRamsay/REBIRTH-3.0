using System;
// Native peer/session authority ONLY. Never a complete command or terminal authorization.
internal sealed class FarmingOutcomePeerReceipt {
 internal readonly object NativeFrame,ClientSession;
 internal readonly World World;internal readonly EntityPlayerLocal Player;internal readonly object WorldState;
 internal readonly Guid Creation,ServerWorld;internal readonly ulong Epoch,Nonce;internal readonly int Actor;
 bool consumed,consuming;SeedOutgoingEnrollment paired;FarmingOutcomeEnrollmentReceipt retained;
 internal FarmingOutcomePeerReceipt(object frame,object session,World world,EntityPlayerLocal player,object state,Guid creation,Guid serverWorld,ulong epoch,ulong nonce,int actor){NativeFrame=frame;ClientSession=session;World=world;Player=player;WorldState=state;Creation=creation;ServerWorld=serverWorld;Epoch=epoch;Nonce=nonce;Actor=actor;}
 internal bool IsAuthenticatedOutcomeCurrent()=>FarmingSessionTransport.IsAuthenticatedOutcomeCurrent(this);
 internal bool TryConsume(){bool entered=false;try{if(consumed||consuming)return false;consuming=true;entered=true;if(consumed||!IsAuthenticatedOutcomeCurrent())return false;consumed=true;return true;}catch{return false;}finally{if(entered)consuming=false;}}
 internal bool TryConsumeForEnrollment(SeedOutgoingEnrollment enrollment,out SeedMatched matched){matched=null;bool entered=false;try{if(enrollment==null||consumed||consuming)return false;consuming=true;entered=true;if(!MatchesEnrollment(enrollment)||!IsAuthenticatedOutcomeCurrent()||consumed||!MatchesEnrollment(enrollment)||!IsAuthenticatedOutcomeCurrent())return false;var candidate=new SeedMatched(this,enrollment);if(!MatchesEnrollment(enrollment)||!IsAuthenticatedOutcomeCurrent()||consumed)return false;var durable=new FarmingOutcomeEnrollmentReceipt(this,enrollment,ClientSession);if(!MatchesEnrollment(enrollment)||!IsAuthenticatedOutcomeCurrent()||consumed||!EnrollmentScalars(enrollment))return false;paired=enrollment;retained=durable;consumed=true;matched=candidate;return true;}catch{return false;}finally{if(entered)consuming=false;}}
 internal bool EnrollmentScalars(SeedOutgoingEnrollment e)=>e!=null&&e.Pending!=null&&ReferenceEquals(e.Pending.Original,e.Original)&&e.Epoch==Epoch&&e.Nonce==Nonce&&e.Actor==Actor&&e.Creation==Creation&&e.ServerWorld==ServerWorld&&ReferenceEquals(e.World,World)&&ReferenceEquals(e.Player,Player)&&ReferenceEquals(e.WorldState,WorldState);
 bool MatchesEnrollment(SeedOutgoingEnrollment e)=>e!=null&&e.Pending!=null&&ReferenceEquals(e.Pending.Original,e.Original)&&e.Epoch==Epoch&&e.Nonce==Nonce&&e.Actor==Actor&&e.Creation==Creation&&e.ServerWorld==ServerWorld&&ReferenceEquals(e.World,World)&&ReferenceEquals(e.Player,Player)&&ReferenceEquals(e.WorldState,WorldState)&&e.IsRegisteredCurrent()&&ReferenceEquals(e.Pending.Original,e.Original)&&e.Epoch==Epoch&&e.Nonce==Nonce&&e.Actor==Actor&&e.Creation==Creation&&e.ServerWorld==ServerWorld&&ReferenceEquals(e.World,World)&&ReferenceEquals(e.Player,Player)&&ReferenceEquals(e.WorldState,WorldState); internal bool MatchesRetained(FarmingOutcomeEnrollmentReceipt receipt,SeedOutgoingEnrollment enrollment)=>consumed&&ReferenceEquals(paired,enrollment)&&ReferenceEquals(retained,receipt);
 internal bool TryGetRetainedAuthentication(SeedOutgoingEnrollment enrollment,out FarmingOutcomeEnrollmentReceipt receipt){receipt=null;if(!ReferenceEquals(paired,enrollment)||retained==null||!retained.IsAuthenticatedEnrollmentCurrent())return false;receipt=retained;return true;}
 internal bool IsAuthenticatedEnrollmentCurrent(SeedOutgoingEnrollment enrollment)=>ReferenceEquals(paired,enrollment)&&retained!=null&&retained.IsAuthenticatedEnrollmentCurrent();
 internal bool IsConsumed=>consumed;
}
// Recorded one-use native authentication tied to SAME pending operation; not a terminal/effect witness.
internal sealed class FarmingOutcomeEnrollmentReceipt {
 internal readonly FarmingOutcomePeerReceipt Peer;internal readonly SeedOutgoingEnrollment Enrollment;internal readonly object ClientSession;
 internal FarmingOutcomeEnrollmentReceipt(FarmingOutcomePeerReceipt peer,SeedOutgoingEnrollment enrollment,object session){Peer=peer;Enrollment=enrollment;ClientSession=session;}
 internal bool IsAuthenticatedEnrollmentCurrent()=>FarmingSessionTransport.IsRetainedOutcomeCurrent(this);
}