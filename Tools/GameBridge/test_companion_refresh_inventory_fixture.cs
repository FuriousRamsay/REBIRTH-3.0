using System;using System.Collections.Generic;
struct RebirthNpcStableId {public int Value;}
class RebirthDogInventorySnapshot {public int Revision;}
static class RebirthDogInventoryService {public static int Calls,Revision;public static RebirthDogInventorySnapshot GetSnapshot(RebirthNpcStableId id){Calls++;return new RebirthDogInventorySnapshot {Revision=Revision};}}
class Subject {
// HELPER
}
class Check {static void Main(){var a=new RebirthNpcStableId {Value=1};var b=new RebirthNpcStableId {Value=2};var cache=new Dictionary<RebirthNpcStableId,RebirthDogInventorySnapshot>();var first=Subject.GetDogInventoryForRefresh(a,cache);var same=Subject.GetDogInventoryForRefresh(a,cache);if(!object.ReferenceEquals(first,same)||RebirthDogInventoryService.Calls!=1)throw new Exception("duplicate decode");Subject.GetDogInventoryForRefresh(b,cache);if(RebirthDogInventoryService.Calls!=2)throw new Exception("identity collision");RebirthDogInventoryService.Revision=9;var next=Subject.GetDogInventoryForRefresh(a,new Dictionary<RebirthNpcStableId,RebirthDogInventorySnapshot>());if(next.Revision!=9||object.ReferenceEquals(first,next))throw new Exception("later refresh stale");var direct=Subject.GetDogInventoryForRefresh(a,null);var direct2=Subject.GetDogInventoryForRefresh(a,null);if(object.ReferenceEquals(direct,direct2)||RebirthDogInventoryService.Calls!=5)throw new Exception("uncached caller stale");Console.WriteLine("PASS one decode per identity/refresh, separate identity, fresh later revision, uncached callers");}}
