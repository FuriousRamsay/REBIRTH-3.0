using System;using System.Collections.Generic;
class ItemClass {public static int Calls;public string Name;public static ItemClass GetItemClass(string k){Calls++;return k.StartsWith("missing")?null:new ItemClass {Name=k.EndsWith("0")?"SAME":k.EndsWith("1")?"same":k.EndsWith("2")?null:"Display "+k};}public string GetLocalizedItemName(){return Name;}}
class Snapshot {public Dictionary<string,int> Quantities=new Dictionary<string,int>();}
class Check {static List<string> Baseline(Snapshot snapshot){
        List<string> keys = new List<string>(snapshot.Quantities.Keys);
        keys.Sort(delegate(string a, string b)
        {
            ItemClass ia = ItemClass.GetItemClass(a);
            ItemClass ib = ItemClass.GetItemClass(b);
            return string.Compare(ia != null ? ia.GetLocalizedItemName() : a,
                                  ib != null ? ib.GetLocalizedItemName() : b,
                                  StringComparison.OrdinalIgnoreCase);
        });
return keys;}
static List<string> Updated(Snapshot snapshot){
// UPDATED
return keys;}
static void Main(){var random=new Random(42);for(int n=0;n<300;n++){var s=new Snapshot();for(int i=0;i<n;i++){string k=(i%7==0?"missing":"item")+random.Next().ToString()+"_"+i;s.Quantities[k]=1;}var a=Baseline(s);ItemClass.Calls=0;var b=Updated(s);if(ItemClass.Calls!=n||string.Join("|",a)!=string.Join("|",b))throw new Exception("sort mismatch "+n);}Console.WriteLine("PASS 300 sort equivalence cases, missing/null/tied names; one lookup per entry");}}
