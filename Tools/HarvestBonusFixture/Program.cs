using System;
class Program {
 static int checks;
 static void Check(int actual,int expected,string label){if(actual!=expected)throw new Exception(label+": got "+actual+", expected "+expected);checks++;Console.WriteLine("PASS "+label);}
 static void Main(){
 RebirthAttackHarvestBonusPolicy.Disable();Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(int.MaxValue),int.MaxValue,"disabled preserves native count");
 RebirthAttackHarvestBonusPolicy.EnableManualTest(500,500,true,true);
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(int.MaxValue),10000,"maximum input clamps before narrowing");
 Check(RebirthAttackHarvestBonusPolicy.ApplyDestroyBonus(715827883),10000,"wraparound near uint boundary clamps");
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(2000),10000,"ordinary output maximum");
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(1),6,"positive multiplier");
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(0),0,"zero preserved");
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(int.MinValue),int.MinValue,"negative native count preserved");
 RebirthAttackHarvestBonusPolicy.EnableManualTest(-90,-90,true,true);
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(1),1,"positive native minimum preserved");
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(19),1,"fraction rounds down");
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(int.MaxValue),10000,"negative multiplier high input still caps");
 RebirthAttackHarvestBonusPolicy.EnableManualTest(500,500,false,true);
 Check(RebirthAttackHarvestBonusPolicy.ApplyDestroyBonus(5),5,"destroy toggle preserves native");
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(5),30,"harvest independent toggle");
 RebirthAttackHarvestBonusPolicy.EnableManualTest(0,0,true,true);
 Check(RebirthAttackHarvestBonusPolicy.ApplyHarvestBonus(int.MaxValue),int.MaxValue,"zero modifier preserves native count");
 Console.WriteLine("PASS all "+checks+" actual-source arithmetic cases; no native/gameplay claim");
 }
}
