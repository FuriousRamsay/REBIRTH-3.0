using System;

// Positive incoming native owner lineage. This is a read-only correlation
// source, separate from any grant, saved NPC backing or lifecycle admission.
internal static partial class RebirthNpcTradeLocalOwnerLoad
{
    internal sealed class NativeReentry
    {
        private readonly RebirthNpcTradeLocalOwnerLoad.Source source;
        private readonly RebirthNpcTradeOriginal original;
        internal readonly World World;
        internal readonly string Directory;
        internal Guid Operation=>original.Operation;
        internal int Step { get; }
        internal int Phase { get; }
        private NativeReentry(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original,string directory,int step,int phase)
        {this.source=source;this.original=original;World=source.Player.world;Directory=directory;Step=step;Phase=phase;}
        internal bool Current()=>source.Current(source.Player)&&ReferenceEquals(source.Player.world,World)&&
            RebirthNpcSaveQuarantineScope.SameDirectory(Directory,GameIO.GetSaveGameDir())&&source.Matches(original)&&
            source.TryStage(out int step,out int phase)&&step==Step&&phase==Phase;
        internal bool Matches(RebirthNpcTradeOriginal candidate)=>candidate!=null&&candidate.Image==original.Image&&Current();
        // Existing continuation still requires its own current saved NPC plan,
        // native hold and backing checks. This witness supplies none of them.
        internal bool TryContinue()=>Current()&&RebirthNpcTradeService.ResumeLocalLoaded(source);
        internal static NativeReentry Capture(RebirthNpcTradeLocalOwnerLoad.Source source)
        {
            try
            {
                if(source==null||!source.Current(source.Player)||
                    !RebirthNpcTradeOwnerOriginalStore.TryReadRetained(source.Player,source.Operation,out var original)||
                    !source.Matches(original)||!source.TryStage(out int step,out int phase))return null;
                string directory=RebirthNpcSaveQuarantineScope.ScopeDirectory(GameIO.GetSaveGameDir());
                var value=new NativeReentry(source,original,directory,step,phase);
                return value.Current()?value:null;
            }
            catch{return null;}
        }
    }
}
