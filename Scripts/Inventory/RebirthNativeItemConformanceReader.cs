using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Canonical-v9 reader used by RebirthNativeItemCodec. No native hydration Harmony patch.
// Limits define the supported domain, not native gameplay limits. Legacy migration is separate.
internal static class RebirthNativeItemConformanceReader
{
    internal const int MaximumEncodedCharacters = 262144;
    internal const int MaximumDepth = 64;
    internal const int MaximumNodes = 4096;
    internal const int MaximumChildSlots = 8192;
    internal const int MaximumStringBytes = 65536;
    internal const int MaximumAggregateStringBytes = 131072;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private sealed class Node
    {
        internal int Type;
        internal ushort Seed;
        internal ItemClass RegistryClass;
        internal ItemClass ReadClass;
        internal ItemClass FinalClass;
        internal Node[] Mods;
        internal Node[] Cosmetics;
        internal long Texture;
        internal bool Modifier;
    }
    private sealed class Frame
    {
        internal Node Node;
        internal int Depth;
        internal int Phase;
        internal int Index;
    }
    private sealed class Cursor
    {
        internal readonly byte[] Bytes;
        internal int Position;
        internal int Strings;
        internal int Nodes;
        internal int Slots;
        internal readonly ItemClass[] Registry;
        internal readonly int ItemStart;
        internal Cursor(byte[] bytes)
        {
            Bytes = bytes; Registry = ItemClass.list; ItemStart = Block.ItemsStartHere;
            if (Registry == null || ItemStart <= 0) throw new InvalidDataException();
        }
        internal void Need(int count)
        {
            if (count < 0 || count > Bytes.Length - Position) throw new InvalidDataException();
        }
        internal byte Byte() { Need(1); return Bytes[Position++]; }
        internal bool Boolean() { byte b = Byte(); if (b > 1) throw new InvalidDataException(); return b == 1; }
        internal ushort U16() { Need(2); int n = Bytes[Position] | Bytes[Position + 1] << 8; Position += 2; return (ushort)n; }
        internal int I32() { Need(4); int n = Bytes[Position] | Bytes[Position + 1] << 8 | Bytes[Position + 2] << 16 | Bytes[Position + 3] << 24; Position += 4; return n; }
        internal long I64() { uint low = unchecked((uint)I32()); uint high = unchecked((uint)I32()); return unchecked((long)((ulong)low | (ulong)high << 32)); }
        internal string String()
        {
            uint length = 0; int groups = 0; byte b;
            do
            {
                b = Byte();
                if (groups == 4 && (b & 0xF8) != 0) throw new InvalidDataException();
                length |= (uint)(b & 127) << (groups * 7); groups++;
                if (groups == 5 && (b & 128) != 0) throw new InvalidDataException();
            } while ((b & 128) != 0);
            if (groups > 1 && (b & 127) == 0) throw new InvalidDataException();
            if (length > MaximumStringBytes || length > MaximumAggregateStringBytes - Strings) throw new InvalidDataException();
            Need((int)length);
            string text = StrictUtf8.GetString(Bytes, Position, (int)length);
            Position += (int)length; Strings += (int)length;
            return text;
        }
        internal Node[] Children() { int count = Byte(); if (count > MaximumChildSlots - Slots) throw new InvalidDataException(); Slots += count; return new Node[count]; }
        internal Node Header(int depth)
        {
            if (depth > MaximumDepth || ++Nodes > MaximumNodes || Byte() != 9) throw new InvalidDataException();
            byte flags = Byte(); if ((flags & ~3) != 0) throw new InvalidDataException();
            int type = U16(); if ((flags & 1) != 0) type = checked(type + ItemStart);
            if (type <= 0 || type >= Registry.Length || Registry[type] == null) throw new InvalidDataException();
            ItemClass registered = Registry[type];
            // Native ReadData resolves ItemClass before reading Seed (new node seed is zero).
            ItemClass readClass = registered is ItemClassQuest ? ItemClassQuest.GetItemQuestById(0) : registered;
            if (readClass == null) throw new InvalidDataException();
            var node = new Node { Type = type, RegistryClass = registered, ReadClass = readClass, Modifier = readClass is ItemClassModifier };
            I32(); U16(); U16(); // native Single UseTimes bits, Quality, Meta
            int metadata = Byte(); var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < metadata; i++)
            {
                if (!keys.Add(String())) throw new InvalidDataException();
                int tag = I32();
                if (tag == 1 || tag == 2) I32();
                else if (tag == 3) String();
                else throw new InvalidDataException(); // null/unknown metadata is not canonical writer output
            }
            if ((flags & 2) != 0) { int stats = Byte(); Need(stats * 5); Position += stats * 5; }
            if (!node.Modifier) node.Mods = Children();
            return node;
        }
        internal void Tail(Node node)
        {
            Byte(); Byte(); node.Seed = U16();
            bool texture = Boolean(); node.Texture = texture ? I64() : 0L;
            if (texture && node.Texture == 0L) throw new InvalidDataException();
            node.FinalClass = node.RegistryClass is ItemClassQuest ? ItemClassQuest.GetItemQuestById(node.Seed) : node.RegistryClass;
            if (node.FinalClass == null || (node.FinalClass is ItemClassModifier) != node.Modifier) throw new InvalidDataException();
        }
    }
    private static Node Preflight(Cursor cursor, bool requireEnd = true)
    {
        Node root = cursor.Header(1);
        var stack = new Stack<Frame>(); stack.Push(new Frame { Node = root, Depth = 1 });
        while (stack.Count != 0)
        {
            Frame frame = stack.Peek();
            if (frame.Node.Modifier) { cursor.Tail(frame.Node); stack.Pop(); continue; }
            Node[] children = frame.Phase == 0 ? frame.Node.Mods : frame.Node.Cosmetics;
            if (frame.Index < children.Length)
            {
                int index = frame.Index++;
                if (cursor.Boolean())
                {
                    Node child = cursor.Header(frame.Depth + 1); children[index] = child;
                    stack.Push(new Frame { Node = child, Depth = frame.Depth + 1 });
                }
                continue;
            }
            if (frame.Phase == 0) { frame.Phase = 1; frame.Index = 0; frame.Node.Cosmetics = cursor.Children(); }
            else { cursor.Tail(frame.Node); stack.Pop(); }
        }
        if (requireEnd && cursor.Position != cursor.Bytes.Length) throw new InvalidDataException();
        return root;
    }
    private static bool RegistryCurrent(Cursor cursor, Node root)
    {
        if (!ReferenceEquals(cursor.Registry, ItemClass.list) || cursor.ItemStart != Block.ItemsStartHere) return false;
        var nodes = new Stack<Node>(); nodes.Push(root);
        while (nodes.Count != 0)
        {
            Node node = nodes.Pop();
            if (!ReferenceEquals(cursor.Registry[node.Type], node.RegistryClass)) return false;
            if (node.RegistryClass is ItemClassQuest &&
                (!ReferenceEquals(ItemClassQuest.GetItemQuestById(0), node.ReadClass) ||
                 !ReferenceEquals(ItemClassQuest.GetItemQuestById(node.Seed), node.FinalClass))) return false;
            foreach (Node[] children in new[] { node.Mods, node.Cosmetics })
                if (children != null) foreach (Node child in children) if (child != null) nodes.Push(child);
        }
        return true;
    }
    private sealed class Binding { internal Node Plan; internal ItemValue Value; }
    private static bool Hydrate(Node root, ItemValue decoded)
    {
        var work = new Stack<Binding>(); var bindings = new List<Binding>(); work.Push(new Binding { Plan = root, Value = decoded });
        while (work.Count != 0)
        {
            Binding binding = work.Pop(); Node plan = binding.Plan; ItemValue value = binding.Value;
            if (value == null || value.IsEmpty() || value.type != plan.Type || value.Seed != plan.Seed ||
                !ReferenceEquals(value.ItemClass, plan.FinalClass)) return false;
            if (!plan.Modifier)
            {
                if (value.ModificationCount != plan.Mods.Length || value.CosmeticModCount != plan.Cosmetics.Length) return false;
                for (int group = 0; group < 2; group++)
                {
                    Node[] children = group == 0 ? plan.Mods : plan.Cosmetics;
                    for (int i = 0; i < children.Length; i++)
                    {
                        ItemValue child = group == 0 ? value.GetModification(i) : value.GetCosmeticMod(i);
                        if (children[i] == null) { if (child != null && !child.IsEmpty()) return false; }
                        else work.Push(new Binding { Plan = children[i], Value = child });
                    }
                }
            }
            bindings.Add(binding);
        }
        // All detached nodes bind before any texture assignment. Shared empty sentinels are never touched.
        foreach (Binding binding in bindings)
            binding.Value.TextureFullArray = new TextureFullArray(binding.Plan.Texture);
        return true;
    }
    private static byte[] EncodeDetached(ItemValue value)
    {
        using (var stream = new MemoryStream())
        using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous = writer.Encoding;
            try { writer.Encoding = new UTF8Encoding(false, false); writer.SetBaseStream(stream); ItemValue.Write(value, writer); writer.Flush(); return stream.ToArray(); }
            finally { writer.Encoding = previous; }
        }
    }
    internal static bool TryDecodeCanonicalV9(string encoded, out ItemValue value)
    {
        value = null;
        if (string.IsNullOrEmpty(encoded) || encoded.Length > MaximumEncodedCharacters) return false;
        try
        {
            byte[] bytes = Convert.FromBase64String(encoded);
            if (bytes.Length == 0 || Convert.ToBase64String(bytes) != encoded) return false;
            var cursor = new Cursor(bytes); Node plan = Preflight(cursor);
            if (!RegistryCurrent(cursor, plan)) return false;
            ItemValue decoded;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                Encoding previous = reader.Encoding;
                try { reader.Encoding = new UTF8Encoding(false, false); reader.SetBaseStream(stream); decoded = ItemValue.ReadOrNull(reader); if (stream.Position != stream.Length) return false; }
                finally { reader.Encoding = previous; }
            }
            if (!RegistryCurrent(cursor, plan) || !Hydrate(plan, decoded)) return false;
            byte[] roundtrip = EncodeDetached(decoded);
            if (!RegistryCurrent(cursor, plan) || Convert.ToBase64String(roundtrip) != encoded) return false;
            value = decoded; return true;
        }
        catch (Exception) { value = null; return false; }
    }
    // v1 Contents framing: version byte, capacity byte, then native UInt16-count stack rows.
    // One cursor shares all structural/string budgets across rows before native allocation.
    internal static bool TryDecodeStackArrayV1(string encoded, int capacity, int maximumBytes, int maximumText, out ItemStack[] values)
    {
        values = null;
        if (capacity <= 0 || capacity > byte.MaxValue || maximumBytes <= 0 || maximumText <= 0 ||
            string.IsNullOrEmpty(encoded) || encoded.Length > maximumText || encoded.Length > MaximumEncodedCharacters) return false;
        try
        {
            byte[] bytes = Convert.FromBase64String(encoded);
            if (bytes.Length > maximumBytes || Convert.ToBase64String(bytes) != encoded) return false;
            var cursor = new Cursor(bytes);
            if (cursor.Byte() != 1 || cursor.Byte() != capacity) return false;
            var plans = new Node[capacity]; var counts = new ushort[capacity];
            for (int i = 0; i < capacity; i++)
            {
                counts[i] = cursor.U16();
                if (counts[i] > 0) plans[i] = Preflight(cursor, false);
            }
            if (cursor.Position != bytes.Length) return false;
            for (int i = 0; i < capacity; i++) if (plans[i] != null && !RegistryCurrent(cursor, plans[i])) return false;
            ItemStack[] decoded;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                Encoding previous = reader.Encoding;
                try
                {
                    reader.Encoding = new UTF8Encoding(false, false); reader.SetBaseStream(stream);
                    if (reader.ReadByte() != 1 || reader.ReadByte() != capacity) return false;
                    decoded = ItemStack.CreateArray(capacity);
                    for (int i = 0; i < capacity; i++) decoded[i].Read(reader);
                    if (stream.Position != bytes.Length) return false;
                }
                finally { reader.Encoding = previous; }
            }
            for (int i = 0; i < capacity; i++)
            {
                if (decoded[i] == null || decoded[i].count != counts[i]) return false;
                if (plans[i] != null && (!RegistryCurrent(cursor, plans[i]) || !Hydrate(plans[i], decoded[i].itemValue))) return false;
                if (plans[i] == null && !decoded[i].IsEmpty()) return false;
            }
            string roundtrip;
            if (!TryEncodeStackArrayImage(decoded, maximumBytes, maximumText, out roundtrip) || roundtrip != encoded) return false;
            for (int i = 0; i < capacity; i++) if (plans[i] != null && !RegistryCurrent(cursor, plans[i])) return false;
            values = decoded; return true;
        }
        catch (Exception) { values = null; return false; }
    }
    private static bool TryEncodeStackArrayImage(ItemStack[] values, int maximumBytes, int maximumText, out string encoded)
    {
        encoded = null;
        // Bound native writer recursion before caller-owned trees, including cyclic graphs.
        var topology = new Stack<KeyValuePair<ItemValue, int>>();
        foreach (ItemStack row in values)
            if (row != null && row.count > 0 && row.itemValue != null) topology.Push(new KeyValuePair<ItemValue, int>(row.itemValue, 1));
        int nodes = 0, slots = 0, stringBytes = 0, metadataEntries = 0;
        while (topology.Count != 0)
        {
            var entry = topology.Pop(); ItemValue item = entry.Key;
            if (entry.Value > MaximumDepth || ++nodes > MaximumNodes || item.IsEmpty() || item.ItemClass == null) return false;
            if (item.Metadata != null && item.Metadata.Count > byte.MaxValue || item.Stats != null && item.Stats.Length > byte.MaxValue) return false;
            if (item.Metadata != null)
                foreach (var entryMetadata in item.Metadata)
                {
                    if (++metadataEntries > maximumBytes / 5 || entryMetadata.Key == null || entryMetadata.Value == null) return false;
                    int keyBytes = StrictUtf8.GetByteCount(entryMetadata.Key);
                    if (keyBytes > MaximumStringBytes || keyBytes > MaximumAggregateStringBytes - stringBytes) return false;
                    stringBytes += keyBytes;
                    object data = entryMetadata.Value.GetValue();
                    switch (entryMetadata.Value.typeTag)
                    {
                        case TypedMetadataValue.TypeTag.Float: if (!(data is float)) return false; break;
                        case TypedMetadataValue.TypeTag.Integer: if (!(data is int)) return false; break;
                        case TypedMetadataValue.TypeTag.String:
                            string text = data as string; if (text == null) return false;
                            int textBytes = StrictUtf8.GetByteCount(text);
                            if (textBytes > MaximumStringBytes || textBytes > MaximumAggregateStringBytes - stringBytes) return false;
                            stringBytes += textBytes; break;
                        default: return false;
                    }
                    // Each entry has at least one key-length byte and four tag bytes.
                    if ((long)metadataEntries * 5 + stringBytes > maximumBytes) return false;
                }
            if (item.ItemClass is ItemClassModifier) continue;
            int mods = item.ModificationCount, cosmetics = item.CosmeticModCount;
            if (mods > byte.MaxValue || cosmetics > byte.MaxValue || mods + cosmetics > MaximumChildSlots - slots) return false;
            slots += mods + cosmetics;
            for (int group = 0; group < 2; group++)
                for (int i = 0; i < (group == 0 ? mods : cosmetics); i++)
                {
                    ItemValue child = group == 0 ? item.GetModification(i) : item.GetCosmeticMod(i);
                    if (child != null && !child.IsEmpty()) topology.Push(new KeyValuePair<ItemValue, int>(child, entry.Value + 1));
                }
        }
        using (var stream = new MemoryStream())
        using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous = writer.Encoding;
            try
            {
                writer.Encoding = new UTF8Encoding(false, false); writer.SetBaseStream(stream);
                writer.Write((byte)1); writer.Write((byte)values.Length);
                foreach (ItemStack stack in values)
                {
                    if (stack == null || stack.count < 0 || stack.count > ushort.MaxValue ||
                        stack.count > 0 && (stack.itemValue == null || stack.itemValue.IsEmpty())) return false;
                    stack.Write(writer);
                    if (stream.Length > maximumBytes) return false;
                }
                writer.Flush();
                string text = Convert.ToBase64String(stream.ToArray());
                if (text.Length > maximumText || text.Length > MaximumEncodedCharacters) return false;
                encoded = text; return true;
            }
            finally { writer.Encoding = previous; }
        }
    }
    internal static bool TryEncodeStackArrayV1(ItemStack[] values, int maximumBytes, int maximumText, out string encoded)
    {
        encoded = null;
        if (values == null || values.Length <= 0 || values.Length > byte.MaxValue || maximumBytes <= 0 || maximumText <= 0) return false;
        try
        {
            string image; ItemStack[] detached;
            if (!TryEncodeStackArrayImage(values, maximumBytes, maximumText, out image) ||
                !TryDecodeStackArrayV1(image, values.Length, maximumBytes, maximumText, out detached)) return false;
            encoded = image; return true;
        }
        catch (Exception) { encoded = null; return false; }
    }
    // Native workstation completion-list framing, with one aggregate item/string preflight budget.
    internal static bool TryDecodeStationCompletions(byte[] bytes,out CraftCompleteData[] values)
    {
        values=null;
        if(bytes==null||bytes.Length<2||bytes.Length>256*1024)return false;
        try
        {
            var cursor=new Cursor(bytes);int count=unchecked((short)cursor.U16());
            if(count<1||count>short.MaxValue||count>(bytes.Length-2)/16)return false;
            var plans=new Node[count];
            for(int i=0;i<count;i++)
            {
                if(cursor.U16()!=1)return false;
                cursor.I32();int stackCount=cursor.U16();
                if(stackCount>0)plans[i]=Preflight(cursor,false);
                cursor.String();cursor.I32();cursor.U16();cursor.String();
            }
            if(cursor.Position!=bytes.Length)return false;
            for(int i=0;i<count;i++)if(plans[i]!=null&&!RegistryCurrent(cursor,plans[i]))return false;
            var decoded=new CraftCompleteData[count];
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                Encoding previous=reader.Encoding;
                try
                {
                    reader.Encoding=new UTF8Encoding(false,false);reader.SetBaseStream(stream);
                    if(reader.ReadInt16()!=count)return false;
                    for(int i=0;i<count;i++){decoded[i]=new CraftCompleteData();decoded[i].Read(reader);}
                    if(stream.Position!=stream.Length)return false;
                }
                finally{reader.Encoding=previous;}
            }
            for(int i=0;i<count;i++)
            {
                var stack=decoded[i].CraftedItemStack;
                if(stack==null)return false;
                if(plans[i]!=null)
                {if(!RegistryCurrent(cursor,plans[i])||!Hydrate(plans[i],stack.itemValue))return false;}
                else if(!stack.IsEmpty())return false;
            }
            using(var stream=new MemoryStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                Encoding previous=writer.Encoding;
                try
                {
                    writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);
                    writer.Write((short)count);foreach(var data in decoded)data.Write(writer);writer.Flush();
                    if(stream.Length!=bytes.Length)return false;
                    var image=stream.ToArray();for(int i=0;i<bytes.Length;i++)if(bytes[i]!=image[i])return false;
                }
                finally{writer.Encoding=previous;}
            }
            for(int i=0;i<count;i++)if(plans[i]!=null&&!RegistryCurrent(cursor,plans[i]))return false;
            values=decoded;return true;
        }
        catch{values=null;return false;}
    }
    private static bool QueueFinite(Cursor cursor,bool nonnegative=false)
    {
        float value=BitConverter.ToSingle(BitConverter.GetBytes(cursor.I32()),0);
        return !float.IsNaN(value)&&!float.IsInfinity(value)&&(!nonnegative||value>=0f);
    }
    // Native v2 queue/Recipe-v1 wire only. No omitted live authoring fields are reconstructed.
    internal static bool TryDecodeStationQueue(byte[] bytes,out RecipeQueueItem[] values)
    {
        values=null;
        if(bytes==null||bytes.Length<1||bytes.Length>256*1024)return false;
        try
        {
            var cursor=new Cursor(bytes);int count=cursor.Byte();if(count<1)return false;
            var repairs=new Node[count];var ingredients=new Node[count][];var outputs=new ItemClass[count];
            for(int i=0;i<count;i++)
            {
                if(cursor.U16()!=2||unchecked((short)cursor.U16())<0)return false;
                cursor.Boolean();if(!QueueFinite(cursor))return false;
                if(cursor.Boolean())
                {
                    cursor.Need(1);if(cursor.Bytes[cursor.Position]==0)cursor.Byte();else repairs[i]=Preflight(cursor,false);
                    cursor.U16();
                }
                cursor.Byte();cursor.I32();if(!QueueFinite(cursor))return false;
                if(!cursor.Boolean())continue;
                if(cursor.U16()!=1)return false;
                int outputType=cursor.I32();outputs[i]=ItemClass.GetForId(outputType);if(outputType<=0||outputs[i]==null)return false;
                if(cursor.I32()<1)return false;cursor.Boolean();if(!QueueFinite(cursor,true))return false;
                if(cursor.I32()<0)return false;cursor.String();int rows=cursor.I32();
                if(rows<0||rows>MaximumChildSlots-cursor.Slots)return false;cursor.Slots+=rows;
                ingredients[i]=new Node[rows];
                for(int j=0;j<rows;j++)if(cursor.U16()>0)ingredients[i][j]=Preflight(cursor,false);
            }
            if(cursor.Position!=bytes.Length)return false;
            for(int i=0;i<count;i++)
            {
                if(repairs[i]!=null&&!RegistryCurrent(cursor,repairs[i]))return false;
                if(ingredients[i]!=null)foreach(var plan in ingredients[i])if(plan!=null&&!RegistryCurrent(cursor,plan))return false;
            }
            var decoded=new RecipeQueueItem[count];
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                Encoding previous=reader.Encoding;
                try
                {
                    reader.Encoding=new UTF8Encoding(false,false);reader.SetBaseStream(stream);
                    if(reader.ReadByte()!=count)return false;
                    for(int i=0;i<count;i++){decoded[i]=new RecipeQueueItem();decoded[i].Read(reader);}
                    if(stream.Position!=stream.Length)return false;
                }
                finally{reader.Encoding=previous;}
            }
            for(int i=0;i<count;i++)
            {
                if(repairs[i]!=null&&(!RegistryCurrent(cursor,repairs[i])||!Hydrate(repairs[i],decoded[i].RepairItem)))return false;
                if(ingredients[i]==null){if(decoded[i].Recipe!=null)return false;continue;}
                var recipe=decoded[i].Recipe;
                if(recipe==null||!ReferenceEquals(ItemClass.GetForId(recipe.itemValueType),outputs[i])||recipe.ingredients==null||recipe.ingredients.Count!=ingredients[i].Length)return false;
                for(int j=0;j<ingredients[i].Length;j++)
                {
                    var stack=recipe.ingredients[j];if(stack==null)return false;
                    if(ingredients[i][j]!=null)
                    {if(!RegistryCurrent(cursor,ingredients[i][j])||!Hydrate(ingredients[i][j],stack.itemValue))return false;}
                    else if(!stack.IsEmpty())return false;
                }
            }
            using(var stream=new MemoryStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                Encoding previous=writer.Encoding;
                try
                {
                    writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);
                    writer.Write((byte)count);foreach(var row in decoded)row.Write(writer);writer.Flush();
                    if(stream.Length!=bytes.Length)return false;
                    var image=stream.ToArray();for(int i=0;i<bytes.Length;i++)if(bytes[i]!=image[i])return false;
                }
                finally{writer.Encoding=previous;}
            }
            if(!ReferenceEquals(cursor.Registry,ItemClass.list)||cursor.ItemStart!=Block.ItemsStartHere)return false;
            for(int i=0;i<count;i++)
            {
                if(repairs[i]!=null&&!RegistryCurrent(cursor,repairs[i]))return false;
                if(ingredients[i]!=null)
                {
                    if(!ReferenceEquals(ItemClass.GetForId(decoded[i].Recipe.itemValueType),outputs[i]))return false;
                    foreach(var plan in ingredients[i])if(plan!=null&&!RegistryCurrent(cursor,plan))return false;
                }
            }
            values=decoded;return true;
        }
        catch{values=null;return false;}
    }
}
