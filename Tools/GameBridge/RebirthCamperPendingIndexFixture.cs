using System;
using System.IO;

// PREPARED, NOT RUN. Compile with the actual journal/index in an isolated fixture.
internal static class RebirthCamperPendingIndexFixture
{
    internal static void Verify()
    {
        string root=Path.Combine(Path.GetTempPath(),"rebirth-index-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var journal=new RebirthCamperTransferJournal(root);var vehicle=Guid.NewGuid();
            for(int i=0;i<64;i++)
            {
                var op=Guid.NewGuid();journal.Prepare(op,vehicle,"fixture",0,new byte[]{1},new byte[]{2});
                journal.Advance(op,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                journal.Advance(op,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            }
            using(var index=new RebirthCamperTransferPendingIndex(journal))
            {
                if(index.FindPending(vehicle)!=null)throw new Exception("Completed history retained as pending.");
                int appliedCount=0;foreach(var receipt in index.FindApplied(vehicle,"fixture"))appliedCount++;
                if(appliedCount!=64)throw new Exception("Applied metadata bootstrap incomplete.");
                foreach(var receipt in index.FindApplied(vehicle,"another owner"))throw new Exception("Owner filter leaked receipts.");
                foreach(var receipt in index.FindApplied(Guid.NewGuid(),"fixture"))throw new Exception("Vehicle filter leaked receipts.");
                // Exclusive handles prove warmed empty lookup does not reopen receipts.
                var files=Directory.GetFiles(root);var locks=new System.Collections.Generic.List<FileStream>();
                try{foreach(var path in files)locks.Add(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None));
                    for(int i=0;i<100;i++)if(index.FindPending(vehicle)!=null)throw new Exception("Empty lookup changed.");}
                finally{foreach(var handle in locks)handle.Dispose();}
                var op=Guid.NewGuid();journal.Prepare(op,vehicle,"fixture",1,new byte[]{3},new byte[]{4});
                if(index.FindPending(vehicle)?.Operation!=op)throw new Exception("Prepare not indexed.");
                journal.Advance(op,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
                if(index.FindPending(vehicle)!=null)throw new Exception("Rejected not retired.");
                int rejectedCount=0;foreach(var receipt in index.FindRejected(vehicle,"fixture")){rejectedCount++;if(receipt.Operation!=op)throw new Exception("Wrong rejected original.");}
                if(rejectedCount!=1)throw new Exception("Rejected publication not indexed.");
                foreach(var receipt in index.FindRejected(vehicle,"another owner"))throw new Exception("Rejected owner filter leaked.");
                using(var coldRejected=new RebirthCamperTransferPendingIndex(journal))
                {rejectedCount=0;foreach(var receipt in coldRejected.FindRejected(vehicle,"fixture"))rejectedCount++;if(rejectedCount!=1)throw new Exception("Cold rejected history lost.");}
                var paid=Guid.NewGuid();journal.Prepare(paid,vehicle,"fixture",2,new byte[]{5},new byte[]{6});
                journal.Advance(paid,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                if(index.FindPending(vehicle)?.State!=RebirthCamperTransferJournal.Phase.Uncertain)throw new Exception("Paid recovery phase lost.");
                using(var reloaded=new RebirthCamperTransferPendingIndex(journal))
                    if(reloaded.FindPending(vehicle)?.Operation!=paid)throw new Exception("Cold paid recovery lost.");
                journal.Advance(paid,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                if(index.FindPending(vehicle)!=null)throw new Exception("Applied receipt not retired.");
                appliedCount=0;foreach(var receipt in index.FindApplied(vehicle,"fixture"))appliedCount++;
                if(appliedCount!=65)throw new Exception("Applied publication not indexed.");
                var first=Guid.NewGuid();journal.Prepare(first,vehicle,"fixture",2,new byte[]{5},new byte[]{6});
                journal.Prepare(Guid.NewGuid(),vehicle,"fixture",3,new byte[]{7},new byte[]{8});
                Refuses(()=>index.FindPending(vehicle));
            }
            using(var cold=new RebirthCamperTransferPendingIndex(journal))Refuses(()=>cold.FindPending(vehicle));
            var disposed=new RebirthCamperTransferPendingIndex(journal);disposed.Dispose();
            Refuses(()=>disposed.FindPending(vehicle));
            string malformed=Path.Combine(root,"malformed");var malformedJournal=new RebirthCamperTransferJournal(malformed);
            File.WriteAllText(Path.Combine(malformed,Guid.NewGuid().ToString("N")+".0.rcj.leftover.tmp"),"uncertain");
            using(var cold=new RebirthCamperTransferPendingIndex(malformedJournal))Refuses(()=>cold.FindPending(vehicle));
            string oversized=Path.Combine(root,"oversized");var oversizedJournal=new RebirthCamperTransferJournal(oversized);
            var oversizedOperation=Guid.NewGuid();
            using(var file=new FileStream(Path.Combine(oversized,oversizedOperation.ToString("N")+".0.rcj"),FileMode.CreateNew))
                file.SetLength(128L*1024*1024+8193);
            Refuses(()=>oversizedJournal.Read(oversizedOperation));
            string separate=Path.Combine(root,"separate");var other=new RebirthCamperTransferJournal(separate);
            using(var index=new RebirthCamperTransferPendingIndex(other))
            {
                if(index.FindPending(vehicle)!=null)throw new Exception("Foreign-root pending operation leaked.");var op=Guid.NewGuid();
                // Force immutable publication collision after Read sees no receipt.
                Action<string> collision=path=>{if(path==other.DirectoryPath)File.WriteAllText(Path.Combine(path,op.ToString("N")+".0.rcj"),"collision");};
                RebirthCamperTransferJournal.PublicationStarting+=collision;
                try{Refuses(()=>other.Prepare(op,vehicle,"fixture",0,new byte[]{1},new byte[]{2}));}
                finally{RebirthCamperTransferJournal.PublicationStarting-=collision;}
                Refuses(()=>index.FindPending(vehicle));
            }
        }
        finally{Directory.Delete(root,true);}
    }
    private static void Refuses(Action action)
    {bool failed=false;try{action();}catch{failed=true;}if(!failed)throw new Exception("Unsafe operation accepted.");}
}
