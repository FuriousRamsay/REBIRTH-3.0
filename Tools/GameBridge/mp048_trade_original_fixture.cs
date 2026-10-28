// AUTHORED_NOT_RUN. Native frame and identity adapters; production original and
// planner bodies are injected. No authenticated peer, disk or game authority.
static class ThreadManager {public static bool IsMainThread()=>true;}
static class RebirthRemoteResourceDurableJournal
{
 public static string Digest(string text){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
}
static class RebirthNativeItemCodec {public static string Encode(ItemValue value)=>value.type+"|"+value.Rich;}
static class RebirthCamperInventorySnapshot
{
 public static byte[] Capture(ItemStack[] slots){using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory)){writer.Write(1);writer.Write(slots.Length);foreach(var cell in slots){writer.Write(cell.count);writer.Write(cell.itemValue.type);writer.Write(cell.itemValue.Rich);}writer.Flush();return memory.ToArray();}}
 public static ItemStack[] Restore(byte[] bytes){using(var memory=new MemoryStream(bytes))using(var reader=new BinaryReader(memory)){if(reader.ReadInt32()!=1)throw new InvalidDataException();int count=reader.ReadInt32();if(count<0||count>4096)throw new InvalidDataException();var result=new ItemStack[count];for(int i=0;i<count;i++){int amount=reader.ReadInt32(),type=reader.ReadInt32();string rich=reader.ReadString();if(amount<0)throw new InvalidDataException();result[i]=new ItemStack(new ItemValue{type=type,Rich=rich},amount);}if(memory.Position!=memory.Length)throw new InvalidDataException();return result;}}
}
// ACTUAL_TRADE_ORIGINAL
class OriginalFixture
{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static ItemStack S(int type,int count,string rich="")=>new ItemStack(new ItemValue{type=type,Rich=rich},count);
 static void Refuse(Action action,string message){try{action();}catch(InvalidDataException){return;}throw new Exception(message);}
 static string Frame(params ItemStack[] slots)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(slots));
 public static void Main()
 {
  Fixture.PlannerMain();
  var quote=new RebirthNpcTradeQuote();var bag=new[]{S(2,6),S(3,100,"untouched native metadata")};var belt=new[]{S(0,1),S(2,7)};
  Need(RebirthNpcTradeOwnerPlan.TryCreate(quote,bag,belt,1,(a,i)=>false,out var plan),"original plan unavailable");
  var original=RebirthNpcTradeOriginal.Freeze(quote,plan,"owner","creation",Guid.NewGuid().ToString(),0,1);
  TradeUnusedHistoryFixture.Run(original);
  TradePrefixFixture.Run(original);
  Need(RebirthNpcTradeOriginal.Read(original.Image).Digest==original.Digest&&original.MatchesSlots(bag,belt,false)&&original.MatchesSlots(plan.AfterBag,plan.AfterBelt,true),"full frozen original roundtrip lost exact native images");
  var detached=original.CopySlots(true,true);detached[0].count=99;Need(original.CopySlots(true,true)[0].count==2,"caller mutated frozen original");
  Need(original.MatchesQuote(quote)&&original.MatchesQuote(original.CopyQuote()),"original lost complete quote binding");
  Action<RebirthNpcTradeQuote>[] changes={q=>q.QuoteId=Guid.NewGuid(),q=>q.ActorId+="x",q=>q.EndpointId+="x",q=>q.Direction=RebirthNpcTradeDirection.SellToNpc,
   q=>q.ItemKey+="x",q=>q.CurrencyItemKey+="x",q=>q.Quantity++,q=>q.UnitPrice++,q=>q.TotalPrice++,q=>q.ExpectedNpcInventoryRevision++,q=>q.RecoveryGeneration++,q=>q.ExpiresClockTicks++};
  foreach(var change in changes){var altered=original.CopyQuote();change(altered);Need(!original.MatchesQuote(altered),"altered authority quote matched frozen original");}
  Need(!original.MatchesQuote(null)&&original.MatchesQuote(quote),"detached quote mutation changed original");
  var legacyQuote=original.CopyQuote();legacyQuote.RecoveryGeneration=0;
  var legacyOriginal=RebirthNpcTradeOriginal.Freeze(legacyQuote,plan,original.Account,original.Creation,original.World,original.Deaths,original.NativeId);
  Need(legacyOriginal.MatchesQuote(legacyQuote)&&RebirthNpcTradeOriginal.Read(legacyOriginal.Image).CopyQuote().RecoveryGeneration==0,"valid legacy generation zero rejected");
  var wire=new Dictionary<string,string>{{"quote",quote.QuoteId.ToString("N")},{"npc","npc"},{"actor","actor"},{"endpoint","player%3A1"},
   {"direction","BuyFromNpc"},{"item","goods"},{"currency","currency"},{"quantity","2"},{"unitPrice","3"},{"total","6"},
   {"inventoryRevision","1"},{"recoveryGeneration","1"},{"expiresUtcTicks",quote.ExpiresClockTicks.ToString(CultureInfo.InvariantCulture)}};
  Need(RebirthNpcTradeQuoteWire.TryRead(wire,"actor","npc","player:1",out var decoded)&&original.MatchesQuote(decoded),"wire quote lost frozen binding");
  var zeroGeneration=new Dictionary<string,string>(wire);zeroGeneration["recoveryGeneration"]="0";
  Need(RebirthNpcTradeQuoteWire.TryRead(zeroGeneration,"actor","npc","player:1",out var legacyDecoded)&&legacyOriginal.MatchesQuote(legacyDecoded),"wire rejected known legacy zero generation");
  foreach(var field in new[]{"actor","npc","endpoint","direction","quantity","unitPrice","total","inventoryRevision","recoveryGeneration","expiresUtcTicks"})
  {var changed=new Dictionary<string,string>(wire);changed[field]="invalid";Need(!RebirthNpcTradeQuoteWire.TryRead(changed,"actor","npc","player:1",out _),"malformed wire field accepted: "+field);}
  var missing=new Dictionary<string,string>(wire);missing.Remove("recoveryGeneration");Need(!RebirthNpcTradeQuoteWire.TryRead(missing,"actor","npc","player:1",out _),"missing authority generation accepted");
  var escaped=new Dictionary<string,string>(wire);escaped["endpoint"]="player%3a1";Need(!RebirthNpcTradeQuoteWire.TryRead(escaped,"actor","npc","player:1",out _),"noncanonical wire escape accepted");
  Need(!RebirthNpcTradeQuoteWire.TryRead(wire,"different owner","npc","player:1",out _)&&!RebirthNpcTradeQuoteWire.TryRead(wire,"actor","npc","player:2",out _),"foreign current UI scope accepted");
  var root=XElement.Parse(original.Image);root.SetAttributeValue("total",7);Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"altered original quote arithmetic accepted");
  root=XElement.Parse(original.Image);root.Element("afterBag").Value=Frame(S(1,3),S(3,100,"untouched native metadata"));Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"extra native trade goods minted");
  root=XElement.Parse(original.Image);root.Element("afterBag").Value=Frame(S(1,2),S(3,100,"rewritten metadata"));Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"unrelated native value rewritten");
  root=XElement.Parse(original.Image);root.Element("beforeBag").Value=Frame(S(2,6,"rich currency"),S(3,100,"untouched native metadata"));Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"rich debit erased at abstract NPC boundary");
  root=XElement.Parse(original.Image);root.Element("afterBag").Value=Frame(S(1,1),S(3,100,"untouched native metadata"));root.Element("afterBelt").Value=Frame(S(0,1),S(1,1));Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"unowned native belt acquired trade mutation");
  root=XElement.Parse(original.Image);var attribute=root.Attribute("version");attribute.Remove();root.Add(attribute);Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"noncanonical attribute order accepted");
  root=XElement.Parse(original.Image);root.Add(new XComment("unknown original node"));Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"unknown original node accepted");
  root=XElement.Parse(original.Image);root.Element("beforeBag").Value+=" ";Refuse(()=>RebirthNpcTradeOriginal.Read(root.ToString(SaveOptions.DisableFormatting)),"noncanonical native frame accepted");
  QuoteSourceFixture.Run();NpcPlanFixture.Run(original);RebirthNpcInventoryPersistenceStore.Run(original);StoreFixture.Run();RebirthNpcInteractionNetworkClient.RunPurposeFixture(original);
  Console.WriteLine("PASS frozen trade description/native delta/store source; native codecs, authenticated permission and native persistence NOT certified");
 }
}
