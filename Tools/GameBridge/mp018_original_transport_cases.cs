// AUTHORED_NOT_RUN. Actual hydration wire codec and shared fragment assembly.
  RebirthRemoteResourceFragments.ClearTransient();var transportBag=Cells(44);transportBag[3]=new Stack{Count=3,ItemData=Convert.ToBase64String(new byte[40000])};
  RebirthHydrationTransferOriginal transportOriginal=null;
  Need(RebirthHydrationTransferPlan.TryCreate(transportBag,Cells(20),null,4,Cell(),true,3,out var transportPlan)&&RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),world,"account",creation,2,8,4,null,transportPlan,out transportOriginal),"large exact original unavailable");
  var transport=RebirthHydrationOriginalTransport.Create(transportOriginal);Need(transport.Count>1&&transport.Hash==transportOriginal.Digest,"original transport digest/frame shape drifted");
  RebirthHydrationTransferOriginal assembled=null;
  for(int index=transport.Count-1;index>=0;index--){Need(RebirthHydrationOriginalTransport.TryReceive("actual-peer-A",transportOriginal.Operation,transport.Hash,transport.Length,transport.Count,index,transport.Part(index),1d,out var piece),"out-of-order original fragment refused");if(piece!=null)assembled=piece;}
  Need(assembled!=null&&assembled.Digest==transportOriginal.Digest&&assembled.TryGetPlan(out var transported)&&transported.GearAfter.ItemData==transportPlan.GearAfter.ItemData,"fragmentation changed full native frame");
  RebirthRemoteResourceFragments.ClearTransient();
  Need(RebirthHydrationOriginalTransport.TryReceive("peer-A",transportOriginal.Operation,transport.Hash,transport.Length,transport.Count,0,transport.Part(0),2d,out assembled)&&assembled==null,"first fragment created complete original");
  for(int index=1;index<transport.Count;index++)Need(RebirthHydrationOriginalTransport.TryReceive("peer-B",transportOriginal.Operation,transport.Hash,transport.Length,transport.Count,index,transport.Part(index),2d,out assembled)&&assembled==null,"different native scope completed original");
  RebirthRemoteResourceFragments.ClearTransient();Guid wrongOuter=Guid.NewGuid();
  for(int index=0;index<transport.Count;index++){bool accepted=RebirthHydrationOriginalTransport.TryReceive("peer-A",wrongOuter,transport.Hash,transport.Length,transport.Count,index,transport.Part(index),3d,out assembled);Need(index==transport.Count-1?!accepted&&assembled==null:accepted&&assembled==null,"assembled original accepted wrong outer operation");}
  var canonical=System.Text.Encoding.UTF8.GetBytes(transportOriginal.Write().ToString(SaveOptions.DisableFormatting));
  Need(RebirthHydrationOriginalTransport.TryDecode(canonical,out assembled)&&assembled.Digest==transportOriginal.Digest,"strict original wire decode refused canonical image");
  Need(!RebirthHydrationOriginalTransport.TryDecode(canonical.Concat(new byte[]{32}).ToArray(),out _)&&!RebirthHydrationOriginalTransport.TryDecode(new byte[]{255,254,255},out _)&&!RebirthHydrationOriginalTransport.TryDecode(System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE hydrationTransfer [<!ENTITY x 'bad'>]><hydrationTransfer>&x;</hydrationTransfer>"),out _),"noncanonical/invalid UTF8/DTD wire accepted");
  RebirthRemoteResourceFragments.ClearTransient();
