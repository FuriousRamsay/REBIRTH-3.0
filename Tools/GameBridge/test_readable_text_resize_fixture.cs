using System;
class Base {public virtual void Update(float dt){}}
class Size {public int x=200,y=48;}
class Label {public string Text="same";public Size Size=new Size();}
class Scroll {public int Invalidations,Resets;public void InvalidateBounds(){Invalidations++;}public void ResetPosition(){Resets++;}}
class View {public Size Size=new Size();public Scroll scrollView=new Scroll();}
class Test:Base {
 Label text=new Label();View viewport=new View();string previous;int resetFrames;bool resetOnTextChange=true,resetPosition;int previousTextWidth=-1,previousViewportWidth=-1,previousViewportHeight=-1;
 // SOURCE
 static void Main(){var t=new Test();t.Update(0);t.Update(0);if(t.viewport.scrollView.Invalidations!=1||t.viewport.scrollView.Resets!=1)throw new Exception("initial reset");t.text.Size.x=100;t.Update(0);t.Update(0);if(t.viewport.scrollView.Invalidations!=2||t.viewport.scrollView.Resets!=1)throw new Exception("resize lost position or bounds");t.Update(0);t.Update(0);if(t.viewport.scrollView.Invalidations!=2)throw new Exception("idle reflow");Console.WriteLine("PASS: initial text resets; width-only resize invalidates bounds without resetting; unchanged frames do no extra work");}
}
