using System;
using System.Collections.Generic;
public class RebirthNpcInventorySnapshot {
 public Dictionary<string,int> Quantities = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,int> Reservations = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
}
public static class Check {
// METHODS
 static void Expect(int actual,int expected) { if(actual!=expected) throw new Exception(actual+" != "+expected); }
 public static void Main() {
 var s=new RebirthNpcInventorySnapshot();
 Expect(GetAvailableQuantity(s,"wood"),0);
 s.Quantities["wood"]=10; Expect(GetAvailableQuantity(s,"wood"),10);
 s.Reservations["wood"]=3; Expect(GetAvailableQuantity(s,"WOOD"),7);
 s.Reservations["wood"]=10; Expect(GetAvailableQuantity(s,"wood"),0);
 s.Reservations["wood"]=11; Expect(GetAvailableQuantity(s,"wood"),0);
 s.Quantities["currency"]=100; s.Reservations["currency"]=90;
 Expect(GetAvailableQuantity(s,"currency"),10);
 Expect(s.Quantities["wood"],10); Expect(s.Reservations["currency"],90);
 Console.WriteLine("PASS trade availability: missing, unreserved, partial, full, defensive clamp, currency, no mutation");
 }
}
