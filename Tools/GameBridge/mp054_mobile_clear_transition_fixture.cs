// AUTHORED_NOT_RUN. Actual terminal dispatch gate, with native save/release
// effects excluded. The real partial-carryover matrix covers those effects.
using System;
internal static partial class RebirthProductiveEarnedCoordinator{
 class MobileSuccessor{internal bool Returned;}
 class MobileTerminal{internal bool Clearing;}
 class Attempt{internal MobileSuccessor MobileSuccessor;internal MobileTerminal MobileTerminal;}
 // ACTUAL_CLEAR_GATE
 public static void Main(){int checks=0;Action<bool> need=value=>{if(!value)throw new Exception("Mobile clear transition "+checks);checks++;};
  var original=new Attempt{MobileSuccessor=new MobileSuccessor{Returned=true},MobileTerminal=new MobileTerminal()};
  need(MobileSuccessorNeedsFinish(original));original.MobileTerminal.Clearing=true;need(!MobileSuccessorNeedsFinish(original));
  original.MobileTerminal.Clearing=false;original.MobileSuccessor.Returned=false;need(!MobileSuccessorNeedsFinish(original));
  original.MobileSuccessor=null;need(!MobileSuccessorNeedsFinish(original));need(!MobileSuccessorNeedsFinish(null));
  Console.WriteLine("PASS "+checks+" actual mobile terminal clear dispatch assertions; native save/release excluded");}
}
