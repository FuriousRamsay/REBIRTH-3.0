using System;
class Storage {
// PRODUCTION_CLASS
internal static string End(string failure,bool dead,bool remaining)=>CompletionFailure(failure,dead,remaining);
}
class Check {static void Assert(bool v,string reason){if(!v)throw new Exception(reason);}static void Main(){Assert(Storage.End(null,false,false)==null,"Alive empty cargo succeeds");Assert(Storage.End(null,true,false)=="death interrupted storage deposit","Death-cleared backpack cannot report success");Assert(Storage.End(null,true,true)=="death interrupted storage deposit","Death with cargo reports interruption");Assert(Storage.End(null,false,true)=="cargo remains after storage deposit","Partial deposit cannot succeed");Assert(Storage.End("conservation failure",true,false)=="conservation failure","Original failure retained");Console.WriteLine("PASS actual POI storage completion predicate: alive complete, death-cleared/death-partial, remaining cargo and prior failure precedence");}}
