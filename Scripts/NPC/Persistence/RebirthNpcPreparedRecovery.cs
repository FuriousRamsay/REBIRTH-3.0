using System;
using System.Threading;

// A typed adapter prepares its complete detached service generation before
// constructing this one-use publication. This is not a reset/load delegate.
internal sealed class RebirthNpcPreparedRecovery
{
    internal readonly string FileName;
    private readonly Func<bool> current;
    private readonly World preparedWorld;
    private readonly string preparedDirectory;
    private Func<bool> commit;private readonly System.Xml.XmlDocument publication;
    internal System.Xml.XmlDocument PublicationDocument=>publication==null?null:(System.Xml.XmlDocument)publication.CloneNode(true);
    internal RebirthNpcPreparedRecovery(string fileName,Func<bool> current,Func<bool> commit,System.Xml.XmlDocument publication=null)
    {FileName=fileName;this.current=current??throw new ArgumentNullException(nameof(current));this.commit=commit??throw new ArgumentNullException(nameof(commit));this.publication=publication==null?null:(System.Xml.XmlDocument)publication.CloneNode(true);preparedWorld=GameManager.Instance?.World;preparedDirectory=preparedWorld==null?null:GameIO.GetSaveGameDir();}
    private bool ScopeCurrent=>ReferenceEquals(preparedWorld,GameManager.Instance?.World)&&(preparedWorld==null||preparedDirectory==GameIO.GetSaveGameDir());
    internal bool IsCurrent=>commit!=null&&ScopeCurrent&&current();
    internal bool Commit(){var publish=Interlocked.Exchange(ref commit,null);return publish!=null&&ScopeCurrent&&publish();}
}
