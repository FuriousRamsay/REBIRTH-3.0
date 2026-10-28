// AUTHORED_NOT_RUN. Actual request codec; canonical legacy creation is covered by scope tests.
  Need(RebirthHydrationIntentFrame.TryCreate(operation,creation,2,true,3,false,new string('0',44),out var intentFrame),"exact hydration request codec refused");var intentBytes=intentFrame.Encode();
  Need(RebirthHydrationIntentFrame.TryDecode(intentBytes,out var intentDecoded)&&intentDecoded.Operation==operation&&intentDecoded.Index==3&&intentDecoded.Creation==creation,"request description roundtrip changed selection/scope");
  for(int variant=0;variant<7;variant++){
   var bad=(byte[])intentBytes.Clone();switch(variant){case 0:bad=bad.Take(bad.Length-1).ToArray();break;case 1:bad=bad.Concat(new byte[]{0}).ToArray();break;case 2:bad[0]=2;break;case 3:for(int n=1;n<=16;n++)bad[n]=0;break;case 4:bad[18]=255;break;case 5:bad[bad.Length-1]=(byte)'x';break;case 6:bad[1+16+1+creation.Length+4]=2;break;}
   Need(!RebirthHydrationIntentFrame.TryDecode(bad,out intentDecoded)&&intentDecoded==null,"malformed request accepted "+variant);
  }
  Need(!RebirthHydrationIntentFrame.TryCreate(operation,creation,2,true,-1,false,new string('0',44),out _)&&!RebirthHydrationIntentFrame.TryCreate(operation,creation,2,false,18,false,new string('0',44),out _)&&!RebirthHydrationIntentFrame.TryCreate(operation,creation,2,false,-1,true,new string('0',44),out _),"invalid equip/return/owned-slot request shape accepted");
