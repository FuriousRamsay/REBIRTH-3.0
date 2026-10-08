using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

#nullable disable

/// <summary>
/// Persistent position-based names for ordinary world storage that cannot safely receive
/// a new TileEntityComposite feature.  Player-owned/generated storage continues to use
/// TEFeatureRebirthContainerName so its name travels with the recovered block; this store
/// exists specifically for persistent POI/world containers whose composite schema must not
/// be changed after a save already exists.
/// </summary>
public static class RebirthContainerNameRegistry
{
    private const string FileName = "RebirthContainerNames.xml";
    private const int SnapshotEntriesPerPackage = 128;

    public sealed class Entry
    {
        public Vector3i Position;
        public string BlockName;
        public string Name;
    }

    private static readonly Dictionary<Vector3i, Entry> names =
        new Dictionary<Vector3i, Entry>();
    private static bool loaded;
    private static bool serverAuthority;

    public static void Reset(bool asServer)
    {
        names.Clear();
        loaded = false;
        serverAuthority = asServer;
    }

    public static string Get(WorldBase world, Vector3i position)
    {
        if (world == null)
            return string.Empty;

        EnsureLoaded();
        position = Canonicalize(world, position);

        Entry entry;
        if (!names.TryGetValue(position, out entry) || entry == null)
            return string.Empty;

        BlockValue current = world.GetBlock(position);
        if (current.isair || current.Block == null)
            return string.Empty;

        string currentBlockName = current.Block.GetBlockName() ?? string.Empty;

        // Do not let a stale position-based name migrate onto a different block after
        // destruction, a POI reset, or some other replacement at the same coordinate.
        if (!string.IsNullOrEmpty(entry.BlockName) &&
            !string.Equals(entry.BlockName, currentBlockName, StringComparison.Ordinal))
            return string.Empty;

        return entry.Name ?? string.Empty;
    }

    public static bool SetServer(World world, Vector3i position, string requestedName)
    {
        if (!serverAuthority || world == null)
            return false;

        EnsureLoaded();
        position = Canonicalize(world, position);
        string name = RebirthContainerRenameService.NormalizeName(requestedName);
        BlockValue blockValue = world.GetBlock(position);
        string blockName = blockValue.Block != null
            ? blockValue.Block.GetBlockName() ?? string.Empty
            : string.Empty;

        Entry previous;
        bool hadPrevious = names.TryGetValue(position, out previous);
        if (string.IsNullOrEmpty(name))
            names.Remove(position);
        else
            names[position] = new Entry
            {
                Position = position,
                BlockName = blockName,
                Name = name
            };

        if (!Save())
        {
            if (hadPrevious) names[position] = previous; else names.Remove(position);
            return false;
        }
        BroadcastUpdate(position, blockName, name);
        return true;
    }

    public static Entry[] Snapshot()
    {
        EnsureLoaded();
        Entry[] result = new Entry[names.Count];
        int index = 0;
        foreach (KeyValuePair<Vector3i, Entry> pair in names)
        {
            Entry value = pair.Value;
            result[index++] = new Entry
            {
                Position = pair.Key,
                BlockName = value != null ? value.BlockName ?? string.Empty : string.Empty,
                Name = value != null ? value.Name ?? string.Empty : string.Empty
            };
        }
        return result;
    }

    public static void SendSnapshot(ClientInfo clientInfo)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!serverAuthority || connection == null || !connection.IsServer || clientInfo == null)
            return;

        Entry[] snapshot = Snapshot();
        if (snapshot.Length == 0)
        {
            clientInfo.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthContainerNameSync>()
                    .Setup(true, new Entry[0]));
            return;
        }

        for (int offset = 0; offset < snapshot.Length; offset += SnapshotEntriesPerPackage)
        {
            int count = Math.Min(SnapshotEntriesPerPackage, snapshot.Length - offset);
            Entry[] chunk = new Entry[count];
            Array.Copy(snapshot, offset, chunk, 0, count);
            clientInfo.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthContainerNameSync>()
                    .Setup(offset == 0, chunk));
        }
    }

    public static void ApplyNetworkSnapshot(bool replace, Entry[] entries)
    {
        // Network names are a client projection, never authoritative save data.
        if (serverAuthority) return;
        // A delta can arrive before the initial snapshot. Mark it initialized so
        // the first Get/Snapshot does not clear the accepted update in EnsureLoaded.
        loaded = true;
        if (replace) names.Clear();

        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (entry == null)
                continue;

            string name = RebirthContainerRenameService.NormalizeName(entry.Name);
            if (string.IsNullOrEmpty(name))
                names.Remove(entry.Position);
            else
                names[entry.Position] = new Entry
                {
                    Position = entry.Position,
                    BlockName = entry.BlockName ?? string.Empty,
                    Name = name
                };
        }
    }

    private static void BroadcastUpdate(Vector3i position, string blockName, string name)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
            return;

        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthContainerNameSync>()
                .Setup(false, new[]
                {
                    new Entry
                    {
                        Position = position,
                        BlockName = blockName ?? string.Empty,
                        Name = name ?? string.Empty
                    }
                }));
    }

    private static Vector3i Canonicalize(WorldBase world, Vector3i position)
    {
        TileEntity tileEntity = QuickStackAcceptedCategoryRegistry.ResolveTileEntity(world, position);
        return tileEntity != null ? tileEntity.ToWorldPos() : position;
    }

    private static string PathName
    {
        get { return Path.Combine(GameIO.GetSaveGameDir(), FileName); }
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        names.Clear();
        if (!serverAuthority) return;

        string path = PathName;
        Dictionary<Vector3i, Entry> staged;
        string error;
        if (!TryLoadFile(path, out staged, out error))
        {
            string primaryError = error;
            if (!TryLoadFile(path + ".bak", out staged, out error))
            {
                if (File.Exists(path) || File.Exists(path + ".bak"))
                    Log.Warning("[REBIRTH ContainerName] persistence load failed: primary=" + primaryError + " backup=" + error);
                return;
            }
            Log.Warning("[REBIRTH ContainerName] recovered persistence from backup after primary=" + primaryError);
        }
        foreach (KeyValuePair<Vector3i, Entry> pair in staged) names[pair.Key] = pair.Value;
    }

    private static bool TryLoadFile(string path, out Dictionary<Vector3i, Entry> staged, out string error)
    {
        staged = new Dictionary<Vector3i, Entry>(); error = string.Empty;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) { error = "missing"; return false; }
        try
        {
            XmlDocument document = new XmlDocument(); document.Load(path);
            XmlElement root=document.DocumentElement;if(root==null||root.Name!="containerNames")throw new InvalidDataException("invalid root");
            XmlNodeList nodes=root.SelectNodes("container");
            if(nodes!=null)foreach(XmlNode node in nodes)
            {
                XmlAttributeCollection a=node.Attributes;int x,y,z;
                if(a==null||a["x"]==null||!int.TryParse(a["x"].Value,out x)||a["y"]==null||!int.TryParse(a["y"].Value,out y)||a["z"]==null||!int.TryParse(a["z"].Value,out z))
                    throw new InvalidDataException("invalid container coordinate");
                string name=RebirthContainerRenameService.NormalizeName(a["name"]!=null?a["name"].Value:string.Empty);if(string.IsNullOrEmpty(name))continue;
                Vector3i pos=new Vector3i(x,y,z);staged[pos]=new Entry{Position=pos,BlockName=a["block"]!=null?a["block"].Value??string.Empty:string.Empty,Name=name};
            }
            return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;staged.Clear();return false;}
    }

    private static bool Save()
    {
        if (!serverAuthority) return false;
        string path = PathName; if (string.IsNullOrEmpty(path)) return false;
        string temp=path+".tmp";
        try
        {
            string dir=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(dir))Directory.CreateDirectory(dir);
            XmlWriterSettings settings=new XmlWriterSettings{Indent=true};
            using(FileStream stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(XmlWriter writer=XmlWriter.Create(stream,settings))
            {
                writer.WriteStartDocument();writer.WriteStartElement("containerNames");
                foreach(KeyValuePair<Vector3i,Entry> pair in names){Entry value=pair.Value;if(value==null||string.IsNullOrEmpty(value.Name))continue;writer.WriteStartElement("container");writer.WriteAttributeString("x",pair.Key.x.ToString());writer.WriteAttributeString("y",pair.Key.y.ToString());writer.WriteAttributeString("z",pair.Key.z.ToString());writer.WriteAttributeString("block",value.BlockName??string.Empty);writer.WriteAttributeString("name",value.Name??string.Empty);writer.WriteEndElement();}
                writer.WriteEndElement();writer.WriteEndDocument();writer.Flush();stream.Flush(true);
            }
            string publishError;if(!RebirthDurableFileCommit.TryPublish(temp,path,out publishError))throw new IOException("durable publication failed: "+publishError);
            return true;
        }
        catch(Exception ex){Log.Warning("[REBIRTH ContainerName] persistence save failed: "+ex.Message);return false;}
    }

}
