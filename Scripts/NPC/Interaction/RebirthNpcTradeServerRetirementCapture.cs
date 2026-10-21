using System;
using System.Linq;

public static partial class RebirthNpcTradeService
{
    internal static RebirthNpcTradeIssuedRetirement CaptureServerIssuedRetirement(World originalWorld)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=false||!Guid.TryParse(originalWorld.worldState?.Guid,out var world)||
                Quotes.Count>MaxQuotes||tradeWorld!=null&&!ReferenceEquals(tradeWorld,originalWorld)||tradeWorld==null&&Quotes.Count!=0||
                Quotes.Any(pair=>pair.Value?.Quote==null||pair.Value.Quote.QuoteId!=pair.Key||pair.Value.OwnerPreparation==null&&
                    (pair.Value.Issued?.Quote==null||pair.Value.Issued.Quote.QuoteId!=pair.Key||
                        !Guid.TryParse(pair.Value.Issued.World,out var issuedWorld)||issuedWorld!=world)))
                throw new InvalidOperationException("Exact outgoing server issued quotations required");
            return new RebirthNpcTradeIssuedRetirement(Quotes.Values.Where(value=>value.OwnerPreparation==null).Select(value=>value.Issued).ToArray());
        }
    }
    internal static RebirthNpcTradeServerRetirement CaptureServerPreparationRetirement(World originalWorld)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=false||
                !Guid.TryParse(originalWorld.worldState?.Guid,out var worldId)||Quotes.Count>MaxQuotes||
                tradeWorld!=null&&!ReferenceEquals(tradeWorld,originalWorld)||tradeWorld==null&&Quotes.Count!=0||Quotes.Values.Any(value=>value==null||
                    value.OwnerPreparation!=null&&(value.OwnerPreparation.Original==null||
                        !Guid.TryParse(value.OwnerPreparation.Original.World,out var savedWorld)||savedWorld!=worldId)))
                throw new InvalidOperationException("Exact outgoing server trade preparations required");
            var preparations=Quotes.Values.Where(value=>value.OwnerPreparation!=null).Select(value=>value.OwnerPreparation).ToArray();
            var retirement=new RebirthNpcTradeServerRetirement(preparations.Select(value=>value.Original).ToArray());
            // Completion may have been saved before this bundle existed. Seed
            // only from the retained typed closure's observed saved readback;
            // the runtime Resolution flag alone is never completion evidence.
            foreach(var preparation in preparations)
                if(preparation.ClosedRecord!=null&&ReferenceEquals(preparation.ClosedRecord.Prepared,preparation))
                    retirement.ObserveCapturedCompletion(preparation.ClosedRecord);
            return retirement;
        }
    }
}
