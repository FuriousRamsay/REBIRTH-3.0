using System;using System.Collections.Generic;
class Check {
// METHODS
 static void Main(){var random=new Random(17);for(int count=0;count<=10;count++)for(int trial=0;trial<1000;trial++){var labels=new List<string>();for(int i=0;i<count;i++)labels.Add(new string('X',random.Next(0,200)));var widths=BreadcrumbWidths(labels);int sum=Math.Max(0,count-1)*2;if(widths.Length!=count)throw new Exception("Count");foreach(int w in widths){if(w<60||w>200)throw new Exception("Width");sum+=w;}if(sum>700)throw new Exception("Header overflow");}Console.WriteLine("PASS: actual breadcrumb allocator,11000 seeded paths length0..10; total including gaps<=700 and widths60..200.");}
}
