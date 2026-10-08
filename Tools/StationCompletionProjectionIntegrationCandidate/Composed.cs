using System.Xml.Linq;using Noemax.GZip;using System;using System.Text;using System.Linq;using System.IO;using System.Collections.Generic;using System.Runtime.CompilerServices;
public enum EAccessModifier{Private}public class PublicizedFromAttribute:Attribute{public PublicizedFromAttribute(EAccessModifier a){}}
public sealed class PooledBinaryWriter:IDisposable{
 public byte[] buffer=new byte[128];public int maxBytesPerChar;public Encoder encoder;public Encoding encoding;public Stream OutStream;
 public PooledBinaryWriter(){Encoding=new UTF8Encoding(false,false);}
 public Encoding Encoding
	{
		get
		{
			return encoding;
		}
		set
		{
			encoding = value ?? throw new ArgumentNullException("value");
			encoder = null;
			maxBytesPerChar = encoding.GetMaxByteCount(1);
		}
	}
public void SetBaseStream(Stream _stream)
	{
		if (_stream != null && !_stream.CanWrite)
		{
			throw new ArgumentException("Stream does not support writing or already closed.");
		}
		StreamSets++;OutStream = _stream;
		encoder = null;
	}
public void Write(byte _value)
	{
		OutStream.WriteByte(_value);
	}
public unsafe void Write(string _value)
	{
		if (_value == null)
		{
			throw new ArgumentNullException("_value");
		}
		if (encoder == null)
		{
			encoder = encoding.GetEncoder();
		}
		int byteCount;
		fixed (char* chars = _value)
		{
			byteCount = encoder.GetByteCount(chars, _value.Length, flush: true);
		}
		Write7BitEncodedInt(byteCount);
		int num = 128 / maxBytesPerChar;
		int num2 = 0;
		int num3 = _value.Length;
		while (num3 > 0)
		{
			int num4 = ((num3 <= num) ? num3 : num);
			int bytes2;
			fixed (char* ptr = _value)
			{
				fixed (byte* bytes = buffer)
				{
					bytes2 = encoder.GetBytes(ptr + num2, num4, bytes, 128, num4 == num3);
				}
			}
			OutStream.Write(buffer, 0, bytes2);
			num2 += num4;
			num3 -= num4;
		}
	}
public void Write7BitEncodedInt(int _value)
	{
		do
		{
			int num = (_value >> 7) & 0xFFFFFF;
			byte b = (byte)((uint)_value & 0x7Fu);
			if (num != 0)
			{
				b = (byte)(b | 0x80u);
			}
			Write(b);
			_value = num;
		}
		while (_value != 0);
	}

public static int Disposals;public void Dispose(){Disposals++;}public static int StreamSets;public static bool FlushFault;public void Flush(){if(FlushFault)throw new IOException("fixture flush fault");OutStream.Flush();}public void Write(ushort n){var b=BitConverter.GetBytes(n);OutStream.Write(b,0,b.Length);}public void Write(short n){var b=BitConverter.GetBytes(n);OutStream.Write(b,0,b.Length);}public void Write(int n){var b=BitConverter.GetBytes(n);OutStream.Write(b,0,b.Length);}public void Write(long n){var b=BitConverter.GetBytes(n);OutStream.Write(b,0,b.Length);}public void Write(float n){var b=BitConverter.GetBytes(n);OutStream.Write(b,0,b.Length);}public void Write(bool _value){Write((byte)(_value?1:0));}}
public sealed class PooledBinaryReader:IDisposable{
 public byte[] buffer=new byte[128];public char[] charBuffer=new char[128];public Decoder decoder;public StringBuilder stringBuilder=new StringBuilder(128);public Stream baseStream;public Encoding encoding;public int Limit=512,CapacityCalls,Declared;
 public PooledBinaryReader(){Encoding=new UTF8Encoding(false,false);}
 
 public Encoding Encoding
	{
		get
		{
			return encoding;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			encoding = value;
			decoder = null;
		}
	}
public void SetBaseStream(Stream _stream)
	{
		if (_stream != null && !_stream.CanRead)
		{
			throw new ArgumentException("The stream doesn't support reading.");
		}
		baseStream = _stream;
		decoder = null;
	}
public byte ReadByte()
	{
		if (baseStream == null)
		{
			throw new IOException("Stream is invalid");
		}
		int num = baseStream.ReadByte();
		if (num == -1)
		{
			throw new EndOfStreamException();
		}
		return (byte)num;
	}
public string ReadString()
	{
		int num = Read7BitEncodedInt();
		if (num < 0)
		{
			throw new IOException("Invalid binary file (string len < 0)");
		}
		if (num == 0)
		{
			return string.Empty;
		}
		stringBuilder.Length = 0;
		stringBuilder.EnsureCapacity(num);
		do
		{
			int num2 = ((num <= 128) ? num : 128);
			FillBuffer(num2);
			if (decoder == null)
			{
				decoder = encoding.GetDecoder();
			}
			int chars = decoder.GetChars(buffer, 0, num2, charBuffer, 0);
			stringBuilder.Append(charBuffer, 0, chars);
			num -= num2;
		}
		while (num > 0);
		return stringBuilder.ToString();
	}
public int Read7BitEncodedInt()
	{
		int num = 0;
		int num2 = 0;
		while (num2 < 35)
		{
			byte b = ReadByte();
			num |= (b & 0x7F) << num2;
			num2 += 7;
			if ((b & 0x80) == 0)
			{
				return num;
			}
		}
		throw new FormatException("Illegal encoding for 7 bit encoded int");
	}
public void FillBuffer(int _numBytes)
	{
		if (baseStream == null)
		{
			throw new IOException("Stream is invalid");
		}
		int num;
		for (int i = 0; i < _numBytes; i += num)
		{
			num = baseStream.Read(buffer, i, _numBytes - i);
			if (num == 0)
			{
				throw new EndOfStreamException();
			}
		}
	}

public static int Disposals;public void Dispose(){Disposals++;}public ushort ReadUInt16(){FillBuffer(2);return BitConverter.ToUInt16(buffer,0);}public short ReadInt16(){FillBuffer(2);return BitConverter.ToInt16(buffer,0);}public int ReadInt32(){FillBuffer(4);return BitConverter.ToInt32(buffer,0);}public long ReadInt64(){FillBuffer(8);return BitConverter.ToInt64(buffer,0);}public float ReadSingle(){FillBuffer(4);return BitConverter.ToSingle(buffer,0);}public bool ReadBoolean(){return ReadByte()!=0;}}
public class Pool<T>where T:new(){public T Value=new T();public T AllocSync(bool b){return Value;}}public static class MemoryPools{public static Pool<PooledBinaryWriter> poolBinaryWriter=new Pool<PooledBinaryWriter>();public static Pool<PooledBinaryReader> poolBinaryReader=new Pool<PooledBinaryReader>();}
public enum PassiveEffects:byte{One=1,Two=2}public class Mapping{public void AddMapping(int t,string n){}}public static class Block{public const int ItemsStartHere=65536;public static Mapping nameIdMapping=new Mapping();}
public class ItemClass{public int Id;public float ScrapTimeOverride,CraftComponentTime=1;public bool HasAnyTags(string tag){return false;}public static ItemClass GetForId(int i){return i>=0&&i<list.Length?list[i]:null;}public static ItemClass[] list=new ItemClass[100000];public static Mapping nameIdMapping=new Mapping();public int MaxCount=100;public string GetItemName(){return Name;}public int QualityMin;public string Name="fixture";public bool IsBlock(){return false;}}public class ItemClassModifier:ItemClass{}public class ItemClassQuest:ItemClass{public static ItemClass GetItemQuestById(int i){return list[1];}}
public static class Utils{public static int FastMax(int a,int b){return Math.Max(a,b);}}public static class Log{public static void Error(string s){}public static void Warning(string s){}}public static class StackTraceUtility{public static string ExtractStackTrace(){return "boundary";}}
public class ItemStack{public ItemStack(){}public ItemStack(ItemValue v,int c){itemValue=v;count=c;}public static Action<ItemStack> AfterNativeRead;public ItemValue itemValue=new ItemValue();public int countField;public int count{get{return countField;}set{countField=value;}}public static int Reads;public void OnValueChanged(){}public bool IsEmpty(){return count==0||itemValue==null||itemValue.IsEmpty();}public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new ItemStack();return a;}public ItemStack Read(PooledBinaryReader _br)
	{
		Reads++;countField = _br.ReadUInt16();
		if (countField > 0)
		{
			itemValue.Read(_br);
		}
		else
		{
			itemValue = ItemValue.None;
		}
		if(AfterNativeRead!=null)AfterNativeRead(this);return this;
	}public void Write(PooledBinaryWriter _bw)
	{
		int num = count;
		if (num > 65535)
		{
			num = 65535;
		}
		_bw.Write((ushort)num);
		if (count != 0)
		{
			itemValue.Write(_bw);
		}
	}}
public static class NativeArrayBoundary{public static ItemValue[] CloneItemValueArray(this ItemValue[] v){if(v==null)return null;var a=new ItemValue[v.Length];for(int i=0;i<a.Length;i++)a[i]=v[i]==null?null:v[i].Clone();return a;}}
public class ItemValue{public bool TryGetMetadata(string key, out string value)
	{
		if (!TryGetMetadata(key, out var value2, TypedMetadataValue.TypeTag.String))
		{
			value = null;
			return false;
		}
		value = (string)value2;
		return true;
	}public bool TryGetMetadata(string key, out object value, TypedMetadataValue.TypeTag typeTag = TypedMetadataValue.TypeTag.None)
	{
		if (Metadata == null)
		{
			value = null;
			return false;
		}
		if (!Metadata.TryGetValue(key, out var value2))
		{
			value = null;
			return false;
		}
		if (typeTag != 0 && value2.GetTypeTag() != typeTag)
		{
			value = null;
			return false;
		}
		value = value2.GetValue();
		return true;
	}public void SetMetadata(string key, string value)
	{
		SetMetadata(key, value, TypedMetadataValue.TypeTag.String);
	}public void SetMetadata(string key, object value, TypedMetadataValue.TypeTag typeTag)
	{
		if (Metadata == null)
		{
			Metadata = new Dictionary<string, TypedMetadataValue>();
		}
		TypedMetadataValue result;
		if (Metadata.TryGetValue(key, out var value2))
		{
			if (!value2.SetValue(value))
			{
				Log.Warning($"Can not update Metadata value '{key}' on ItemValue, type of value '{value}' ({value.GetType().Name}) does not match existing TypeTag ({value2.GetTypeTag()}). From: {StackTraceUtility.ExtractStackTrace()}");
			}
		}
		else if (TypedMetadataValue.TryCreate(value, typeTag, out result))
		{
			Metadata.Add(key, result);
		}
		else
		{
			Log.Warning($"Can not set Metadata key '{key}' on ItemValue, type of value '{value}' ({value.GetType().Name}) does not match TypeTag ({typeTag}). From: {StackTraceUtility.ExtractStackTrace()}");
		}
	}public static int NativeReads;
public struct Stat
	{
		public PassiveEffects type;

		public bool isBoosted;

		public short value;

		public Stat(PassiveEffects _type, int _base, int _added)
		{
			type = _type;
			isBoosted = _added != 0;
			value = (short)(_base + _added);
		}
	}
public int typeField,meta;public byte flags,selectedAmmoTypeIndex;public float useTimes;public ushort quality,seed;public object MetadataDummy;public ItemStack owner;public Stat[] Stats;public ItemValue[] modifications,cosmeticMods;public Dictionary<string,TypedMetadataValue> Metadata;public TextureFullArray textureFullArray;
public ItemValue(int t=0){typeField=t;}public static ItemValue None{get{return new ItemValue();}}public int type{get{return typeField;}set{typeField=value;}}public int Meta{get{return meta;}set{meta=value;}}public float UseTimes{get{return useTimes;}set{useTimes=value;}}public ushort Quality{get{return quality;}set{quality=value;}}public byte Flags{get{return flags;}set{flags=value;}}public byte SelectedAmmoTypeIndex{get{return selectedAmmoTypeIndex;}set{selectedAmmoTypeIndex=value;}}public ushort Seed{get{return seed;}set{seed=value;}}public TextureFullArray TextureFullArray
	{
		get
		{
			return textureFullArray;
		}
		set
		{
			if (!(textureFullArray == value))
			{
				textureFullArray = value;
				NotifyChanged();
			}
		}
	}
public bool HasQuality{get{return Quality>0;}}public int ModificationCount{get{return modifications==null?0:modifications.Length;}}public int CosmeticModCount{get{return cosmeticMods==null?0:cosmeticMods.Length;}}public ItemValue GetModification(int i){return modifications[i];}public ItemValue GetCosmeticMod(int i){return cosmeticMods[i];}
public ItemClass ItemClass
	{
		get
		{
			if (type >= 0 && ItemClass.list != null && type < ItemClass.list.Length)
			{
				ItemClass itemClass = ItemClass.list[type];
				if (itemClass is ItemClassQuest)
				{
					return ItemClassQuest.GetItemQuestById(Seed);
				}
				return itemClass;
			}
			return null;
		}
	}
public static ItemValue ReadOrNull(PooledBinaryReader _br)
	{
		NativeReads++;byte b = _br.ReadByte();
		if (b == 0)
		{
			return null;
		}
		ItemValue itemValue = new ItemValue();
		itemValue.ReadData(_br, b);
		return itemValue;
	}
public void Read(PooledBinaryReader _br)
	{
		NativeReads++;byte b = _br.ReadByte();
		if (b == 0)
		{
			type = 0;
		}
		else
		{
			ReadData(_br, b);
		}
	}
public void ReadData(PooledBinaryReader _br, int version)
	{
		int num = 0;
		if (version >= 8)
		{
			num = _br.ReadByte();
		}
		type = _br.ReadUInt16();
		if ((num & 1) > 0)
		{
			type += Block.ItemsStartHere;
		}
		if (version < 8 && type >= 32768)
		{
			type += 32768;
		}
		ItemClass itemClass = ItemClass;
		if (version > 5)
		{
			UseTimes = _br.ReadSingle();
		}
		else
		{
			UseTimes = (int)_br.ReadUInt16();
		}
		Quality = _br.ReadUInt16();
		if (itemClass != null && itemClass.QualityMin > 0)
		{
			Quality = (byte)Utils.FastMax(Quality, itemClass.QualityMin);
		}
		Meta = _br.ReadUInt16();
		if (Meta >= 65535)
		{
			Meta = -1;
		}
		if (version > 6)
		{
			int num2 = _br.ReadByte();
			for (int i = 0; i < num2; i++)
			{
				string key = _br.ReadString();
				TypedMetadataValue tmv = TypedMetadataValue.Read(_br);
				SetMetadata(key, tmv);
			}
		}
		if ((num & 2) > 0)
		{
			int num3 = _br.ReadByte();
			Stats = new Stat[num3];
			for (int j = 0; j < num3; j++)
			{
				PassiveEffects passiveEffects = (PassiveEffects)_br.ReadByte();
				int @base = _br.ReadInt16();
				int added = _br.ReadInt16();
				Stats[j] = new Stat(passiveEffects, @base, added);
			}
			RemoveUnusedStats();
		}
		if ((version > 4 || HasQuality) && !(itemClass is ItemClassModifier))
		{
			NativeReads++;byte b = _br.ReadByte();
			modifications = ((b > 0) ? new ItemValue[b] : null);
			if (b != 0)
			{
				for (int k = 0; k < b; k++)
				{
					if (_br.ReadBoolean())
					{
						modifications[k] = new ItemValue();
						modifications[k].Read(_br);
					}
					else
					{
						modifications[k] = None;
					}
				}
			}
			b = _br.ReadByte();
			cosmeticMods = ((b > 0) ? new ItemValue[b] : null);
			if (b != 0)
			{
				for (int l = 0; l < b; l++)
				{
					if (_br.ReadBoolean())
					{
						cosmeticMods[l] = new ItemValue();
						cosmeticMods[l].Read(_br);
					}
					else
					{
						cosmeticMods[l] = None;
					}
				}
			}
		}
		Flags = _br.ReadByte();
		if (version > 2)
		{
			SelectedAmmoTypeIndex = _br.ReadByte();
		}
		if (version > 3)
		{
			Seed = _br.ReadUInt16();
			if (type == 0)
			{
				Seed = 0;
			}
		}
		if (version > 8)
		{
			if (_br.ReadBoolean())
			{
				TextureFullArray.Read(_br);
			}
			else
			{
				TextureFullArray.Fill(0L);
			}
		}
	}
public static void Write(ItemValue _iv, PooledBinaryWriter _bw)
	{
		if (_iv == null)
		{
			_bw.Write((byte)0);
		}
		else
		{
			_iv.Write(_bw);
		}
	}
public void Write(PooledBinaryWriter _bw)
	{
		if (IsEmpty())
		{
			_bw.Write((byte)0);
			return;
		}
		_bw.Write((byte)9);
		int num = type;
		byte b = 0;
		if (type >= Block.ItemsStartHere)
		{
			b = 1;
			num -= Block.ItemsStartHere;
		}
		if (Stats != null)
		{
			b = (byte)(b | 2u);
		}
		_bw.Write(b);
		_bw.Write((ushort)num);
		_bw.Write(UseTimes);
		_bw.Write(Quality);
		_bw.Write((ushort)Meta);
		int num2 = ((Metadata != null) ? Metadata.Count : 0);
		_bw.Write((byte)num2);
		if (Metadata != null)
		{
			foreach (string key in Metadata.Keys)
			{
				if (Metadata[key]?.GetValue() != null)
				{
					_bw.Write(key);
					TypedMetadataValue.Write(Metadata[key], _bw);
				}
			}
		}
		if (Stats != null)
		{
			int num3 = Stats.Length;
			_bw.Write((byte)num3);
			for (int i = 0; i < num3; i++)
			{
				Stat stat = Stats[i];
				_bw.Write((byte)stat.type);
				short value = (short)((!stat.isBoosted) ? stat.value : 0);
				short value2 = (short)(stat.isBoosted ? stat.value : 0);
				_bw.Write(value);
				_bw.Write(value2);
			}
		}
		if (!(ItemClass is ItemClassModifier))
		{
			_bw.Write((byte)ModificationCount);
			for (int j = 0; j < ModificationCount; j++)
			{
				ItemValue modification = GetModification(j);
				bool flag = modification != null && !modification.IsEmpty();
				_bw.Write(flag);
				if (flag)
				{
					modification.Write(_bw);
				}
			}
			_bw.Write((byte)CosmeticModCount);
			for (int k = 0; k < CosmeticModCount; k++)
			{
				ItemValue cosmeticMod = GetCosmeticMod(k);
				bool flag2 = cosmeticMod != null && !cosmeticMod.IsEmpty();
				_bw.Write(flag2);
				if (flag2)
				{
					cosmeticMod.Write(_bw);
				}
			}
		}
		_bw.Write(Flags);
		_bw.Write(SelectedAmmoTypeIndex);
		if (type == 0)
		{
			Seed = 0;
		}
		_bw.Write(Seed);
		if (TextureFullArray.IsDefault)
		{
			_bw.Write(_value: false);
		}
		else
		{
			_bw.Write(_value: true);
			TextureFullArray.Write(_bw);
		}
		ItemClass itemClass = ItemClass.list[type];
		if (itemClass == null)
		{
			if (type != 0)
			{
				Log.Error("No ItemClass entry for type " + type);
			}
		}
		else
		{
			((!itemClass.IsBlock()) ? ItemClass.nameIdMapping : Block.nameIdMapping)?.AddMapping(type, itemClass.Name);
		}
	}
public ItemValue Clone()
	{
		ItemValue itemValue = new ItemValue(type);
		itemValue.Meta = Meta;
		itemValue.UseTimes = UseTimes;
		itemValue.Quality = Quality;
		itemValue.SelectedAmmoTypeIndex = SelectedAmmoTypeIndex;
		if (Stats != null)
		{
			itemValue.Stats = new Stat[Stats.Length];
			Array.Copy(Stats, itemValue.Stats, Stats.Length);
		}
		itemValue.modifications = modifications.CloneItemValueArray();
		if (Metadata != null)
		{
			itemValue.Metadata = new Dictionary<string, TypedMetadataValue>();
			foreach (KeyValuePair<string, TypedMetadataValue> metadatum in Metadata)
			{
				itemValue.Metadata.Add(metadatum.Key, metadatum.Value?.Clone());
			}
		}
		itemValue.cosmeticMods = cosmeticMods.CloneItemValueArray();
		itemValue.Flags = Flags;
		if (itemValue.type == 0)
		{
			Seed = 0;
		}
		itemValue.Seed = Seed;
		itemValue.TextureFullArray = TextureFullArray;
		return itemValue;
	}
public void Clear()
	{
		typeField = 0;
		useTimes = 0f;
		quality = 0;
		meta = 0;
		seed = 0;
		selectedAmmoTypeIndex = 0;
		Stats = null;
		modifications = null;
		cosmeticMods = null;
		Metadata = null;
		NotifyChanged();
	}
public void NotifyChanged()
	{
		owner?.OnValueChanged();
	}
public bool IsEmpty()
	{
		return type == 0;
	}
public void SetMetadata(string key, TypedMetadataValue tmv)
	{
		if (Metadata == null)
		{
			Metadata = new Dictionary<string, TypedMetadataValue>();
		}
		if (Metadata.TryGetValue(key, out var value))
		{
			object value2 = tmv.GetValue();
			if (!value.SetValue(value2))
			{
				Log.Warning($"Can not update Metadata value '{key}' on ItemValue, type of value '{value2}' ({value2.GetType().Name}) does not match existing TypeTag ({value.GetTypeTag()}). From: {StackTraceUtility.ExtractStackTrace()}");
			}
		}
		else
		{
			Metadata.Add(key, tmv);
		}
	}
public void RemoveUnusedStats()
	{
		if (Stats == null)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < Stats.Length; i++)
		{
			if (Stats[i].value != 0)
			{
				num++;
			}
		}
		if (num == 0)
		{
			ClearStats();
		}
		else
		{
			if (num >= Stats.Length)
			{
				return;
			}
			Stat[] stats = Stats;
			Stats = new Stat[num];
			int num2 = 0;
			for (int j = 0; j < stats.Length; j++)
			{
				if (stats[j].value != 0)
				{
					Stats[num2++] = stats[j];
				}
			}
		}
	}
public void ClearStats()
	{
		Stats = null;
	}

}





public class TypedMetadataValue
{
	public enum TypeTag
	{
		None,
		Float,
		Integer,
		String
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public readonly TypeTag typeTag;

	[PublicizedFrom(EAccessModifier.Private)]
	public object value;

	public static TypeTag StringToTag(string str)
	{
		return str switch
		{
			"float" => TypeTag.Float, 
			"int" => TypeTag.Integer, 
			"string" => TypeTag.String, 
			_ => TypeTag.None, 
		};
	}

	public TypeTag GetTypeTag()
	{
		return typeTag;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public TypedMetadataValue(object val, TypeTag tag)
	{
		typeTag = tag;
		value = val;
	}

	public static bool TryCreate(object val, TypeTag tag, out TypedMetadataValue result)
	{
		if (!ValueMatchesTag(val, tag))
		{
			result = null;
			return false;
		}
		result = new TypedMetadataValue(val, tag);
		return true;
	}

	public object GetValue()
	{
		return value;
	}

	public bool SetValue(object val)
	{
		if (ValueMatchesTag(val, typeTag))
		{
			value = val;
			return true;
		}
		return false;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static bool ValueMatchesTag(object val, TypeTag tag)
	{
		if (val == null)
		{
			return false;
		}
		return tag switch
		{
			TypeTag.Float => val is float, 
			TypeTag.Integer => val is int, 
			TypeTag.String => val is string, 
			_ => false, 
		};
	}

	public static void Write(TypedMetadataValue tmv, PooledBinaryWriter writer)
	{
		if (tmv != null)
		{
			writer.Write((int)tmv.typeTag);
			switch (tmv.typeTag)
			{
			case TypeTag.Float:
				writer.Write((float)tmv.value);
				break;
			case TypeTag.Integer:
				writer.Write((int)tmv.value);
				break;
			case TypeTag.String:
				writer.Write((string)tmv.value);
				break;
			}
		}
	}

	public static TypedMetadataValue Read(PooledBinaryReader reader)
	{
		object val = null;
		TypeTag typeTag = (TypeTag)reader.ReadInt32();
		switch (typeTag)
		{
		case TypeTag.Float:
			val = reader.ReadSingle();
			break;
		case TypeTag.Integer:
			val = reader.ReadInt32();
			break;
		case TypeTag.String:
			val = reader.ReadString();
			break;
		}
		return new TypedMetadataValue(val, typeTag);
	}

	public override bool Equals(object other)
	{
		TypedMetadataValue typedMetadataValue = other as TypedMetadataValue;
		if (typedMetadataValue != null && typeTag == typedMetadataValue.typeTag)
		{
			return value.Equals(typedMetadataValue.value);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}

	public TypedMetadataValue Clone()
	{
		_ = typeTag;
		return new TypedMetadataValue(value, typeTag);
	}
}



public struct TextureFullArray : IEquatable<TextureFullArray>
{
	[PublicizedFrom(EAccessModifier.Private)]
	public unsafe fixed long values[1];

	public static readonly TextureFullArray _default = new TextureFullArray(0L);

	public unsafe long this[int index]
	{
		get
		{
			if (index < 0 || index >= 1)
			{
				throw new IndexOutOfRangeException($"Index {index} is outside of the valid range of min: 0, max: {1}.");
			}
			return values[index];
		}
		set
		{
			if (index < 0 || index >= 1)
			{
				throw new IndexOutOfRangeException($"Index {index} is outside of the valid range of min: 0, max: {1}.");
			}
			values[index] = value;
		}
	}

	public static TextureFullArray Default => _default;

	public bool IsDefault => Equals(_default);

	public TextureFullArray(long _fillValue)
	{
		Fill(_fillValue);
	}

	public unsafe void Fill(long _fillValue)
	{
		for (int i = 0; i < 1; i++)
		{
			values[i] = _fillValue;
		}
	}

	public unsafe void Read(PooledBinaryReader _br, int count = 1)
	{
		int i;
		for (i = 0; i < count; i++)
		{
			long num = _br.ReadInt64();
			if (i < 1)
			{
				values[i] = num;
			}
		}
		for (; i < 1; i++)
		{
			values[i] = values[0];
		}
	}

	public unsafe void Write(PooledBinaryWriter _bw)
	{
		for (int i = 0; i < 1; i++)
		{
			_bw.Write(values[i]);
		}
	}

	public unsafe bool Equals(TextureFullArray other)
	{
		for (int i = 0; i < 1; i++)
		{
			if (values[i] != other.values[i])
			{
				return false;
			}
		}
		return true;
	}

	public override bool Equals(object obj)
	{
		if (obj is TextureFullArray)
		{
			TextureFullArray other = (TextureFullArray)obj;
			return Equals(other);
		}
		return false;
	}

	public unsafe override int GetHashCode()
	{
		int num = 17;
		for (int i = 0; i < 1; i++)
		{
			num = num * 31 + values[i].GetHashCode();
		}
		return num;
	}

	public static bool operator ==(TextureFullArray left, TextureFullArray right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(TextureFullArray left, TextureFullArray right)
	{
		return !left.Equals(right);
	}
}




// Shared native ItemValue serialization; callers enforce their own item/slot policy.

// Shared native ItemValue serialization; callers enforce their own item/slot policy.
public static class RebirthNativeItemCodec
{
    public static string Encode(ItemValue value)
    {
        using (var stream = new MemoryStream())
        using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous = writer.Encoding;
            try
            {
                writer.Encoding = new UTF8Encoding(false, false);
                writer.SetBaseStream(stream);
                ItemValue.Write(value, writer);
                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
            finally { writer.Encoding = previous; }
        }
    }

    public static bool TryDecode(string encoded, out ItemValue value)
    {
        // Canonical current-version images are preflighted before native allocation and
        // rebound to their complete texture data before exact reencoding and publication.
        return RebirthNativeItemConformanceReader.TryDecodeCanonicalV9(encoded, out value);
    }
}

// Library travels in the physical backpack ItemValue, including native drops/trades and gear custody.
public static class RebirthBackpackLibraryContents
{
    public const string MetadataKey="rebirth.backpack.library.v1";
    private const int MaxBytes=49152;
    private const int MaxText=65536;

    public static bool TryReadEquipped(EntityPlayer player,out ItemValue backpack,out ItemStack[] contents)
    {
        backpack=null;contents=null;
        ItemValue item;
        if(!RebirthSurvivorGearService.TryGetEquippedBackpackItem(player,out item)||!TryRead(item,out contents))return false;
        backpack=item;return true;
    }
    public static bool TryRead(ItemValue backpack,out ItemStack[] contents)
    {
        contents=null;
        if(backpack?.ItemClass==null||backpack.Metadata!=null&&backpack.Metadata.Count>byte.MaxValue)return false;
        int capacity=RebirthBackpackLibraryPolicy.CapacityForBackpack(backpack.ItemClass.GetItemName());
        if(capacity<=0)return false;
        if(backpack.Metadata==null||!backpack.Metadata.ContainsKey(MetadataKey))
        { contents=ItemStack.CreateArray(capacity);return true; }
        var stored=backpack.Metadata[MetadataKey];
        // A corrupt reserved key is not an absent old-save store; native getter can throw on null.
        if(stored==null||stored.GetTypeTag()!=TypedMetadataValue.TypeTag.String||!(stored.GetValue() is string))return false;
        string encoded;
        if(!backpack.TryGetMetadata(MetadataKey,out encoded))return false;
        if(string.IsNullOrEmpty(encoded)||encoded.Length>MaxText)return false;
        try
        {
            ItemStack[] candidate;
            if(!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(encoded,capacity,MaxBytes,MaxText,out candidate))return false;
            foreach(var stack in candidate)
                if(stack.count>0&&(stack.IsEmpty()||!RebirthBackpackLibraryPolicy.IsLearningMaterial(stack.itemValue)||stack.count>stack.itemValue.ItemClass.MaxCount))return false;
            contents=candidate;return true;
        }
        catch{return false;}
    }

    // Return a detached candidate. Inventory and persisted gear are unchanged until caller commits.
    public static bool TryWrite(ItemValue backpack,ItemStack[] contents,out ItemValue candidate)
    {
        candidate=null;
        ItemStack[] prior;
        if(!TryRead(backpack,out prior)||contents==null||contents.Length!=prior.Length)return false;
        try
        {
            foreach(var stack in contents)
            {
                if(stack==null||stack.count<0||stack.count>ushort.MaxValue)return false;
                if(stack.count>0&&(stack.IsEmpty()||!RebirthBackpackLibraryPolicy.IsLearningMaterial(stack.itemValue)||stack.count>stack.itemValue.ItemClass.MaxCount))return false;
                if(stack.itemValue?.Metadata!=null&&stack.itemValue.Metadata.Count>byte.MaxValue)return false;
            }
            string encoded;
            if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(contents,MaxBytes,MaxText,out encoded))return false;
            var next=backpack.Clone();next.SetMetadata(MetadataKey,encoded);
            if(next.Metadata!=null&&next.Metadata.Count>byte.MaxValue||!RebirthBackpackStoragePayload.Fits(next))return false;
            string storedImage; ItemStack[] verified;
            if(!next.TryGetMetadata(MetadataKey,out storedImage)||storedImage!=encoded||!TryRead(next,out verified))return false;
            candidate=next;return true;
        }
        catch{return false;}
    }
}
// Sell stash travels in the physical backpack ItemValue, including native drops/trades and gear custody.
public static class RebirthBackpackSellStashContents
{
    public const string MetadataKey="rebirth.backpack.sellstash.v1";
    private const int MaxBytes=98304;
    private const int MaxText=131072;

    public static bool TryReadEquipped(EntityPlayer player,out ItemValue backpack,out ItemStack[] contents)
    {
        backpack=null;contents=null;
        ItemValue item;
        if(!RebirthSurvivorGearService.TryGetEquippedBackpackItem(player,out item)||!TryRead(item,out contents))return false;
        backpack=item;return true;
    }
    public static bool TryRead(ItemValue backpack,out ItemStack[] contents)
    {
        contents=null;
        if(backpack?.ItemClass==null||backpack.Metadata!=null&&backpack.Metadata.Count>byte.MaxValue)return false;
        int capacity=RebirthBackpackSellStashPolicy.CapacityForBackpack(backpack.ItemClass.GetItemName());
        if(capacity<=0)return false;
        if(backpack.Metadata==null||!backpack.Metadata.ContainsKey(MetadataKey))
        { contents=ItemStack.CreateArray(capacity);return true; }
        var stored=backpack.Metadata[MetadataKey];
        // A corrupt reserved key is not an absent old-save store; native getter can throw on null.
        if(stored==null||stored.GetTypeTag()!=TypedMetadataValue.TypeTag.String||!(stored.GetValue() is string))return false;
        string encoded;
        if(!backpack.TryGetMetadata(MetadataKey,out encoded))return false;
        if(string.IsNullOrEmpty(encoded)||encoded.Length>MaxText)return false;
        try
        {
            ItemStack[] candidate;
            if(!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(encoded,capacity,MaxBytes,MaxText,out candidate))return false;
            foreach(var stack in candidate)
                if(stack.count>0&&(stack.IsEmpty()||!RebirthBackpackSellStashPolicy.IsStorableItem(stack.itemValue)||stack.count>stack.itemValue.ItemClass.MaxCount))return false;
            contents=candidate;return true;
        }
        catch{return false;}
    }

    // Return a detached candidate. Inventory and persisted gear are unchanged until caller commits.
    public static bool TryWrite(ItemValue backpack,ItemStack[] contents,out ItemValue candidate)
    {
        candidate=null;
        ItemStack[] prior;
        if(!TryRead(backpack,out prior)||contents==null||contents.Length!=prior.Length)return false;
        try
        {
            foreach(var stack in contents)
            {
                if(stack==null||stack.count<0||stack.count>ushort.MaxValue)return false;
                if(stack.count>0&&(stack.IsEmpty()||!RebirthBackpackSellStashPolicy.IsStorableItem(stack.itemValue)||stack.count>stack.itemValue.ItemClass.MaxCount))return false;
                if(stack.itemValue?.Metadata!=null&&stack.itemValue.Metadata.Count>byte.MaxValue)return false;
            }
            string encoded;
            if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(contents,MaxBytes,MaxText,out encoded))return false;
            var next=backpack.Clone();next.SetMetadata(MetadataKey,encoded);
            if(next.Metadata!=null&&next.Metadata.Count>byte.MaxValue||!RebirthBackpackStoragePayload.Fits(next))return false;
            string storedImage; ItemStack[] verified;
            if(!next.TryGetMetadata(MetadataKey,out storedImage)||storedImage!=encoded||!TryRead(next,out verified))return false;
            candidate=next;return true;
        }
        catch{return false;}
    }
}
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
public class EntityPlayer{}
public static class RebirthSurvivorGearService{public static bool TryGetEquippedBackpackItem(EntityPlayer p,out ItemValue v){v=null;return false;}}
public static class RebirthBackpackLibraryPolicy{public static int CapacityForBackpack(string n){return n=="backpack"?3:0;}public static bool IsLearningMaterial(ItemValue v){return v.ItemClass.Name=="book";}}
public static class RebirthBackpackSellStashPolicy{public static int CapacityForBackpack(string n){return n=="backpack"?3:0;}public static bool IsStorableItem(ItemValue v){return v!=null&&!v.IsEmpty()&&v.ItemClass!=null;}}
public static class RebirthBackpackStoragePayload{public static bool Allow=true;public static bool Fits(ItemValue v){return Allow;}}
public static class StackCheck{
static int n;static void A(bool b,string s){n++;if(!b)throw new Exception(s);}
static ItemValue V(int type=1){return new ItemValue(type){Meta=-1,Seed=17,TextureFullArray=new TextureFullArray(-23)};}
static void Bad(byte[] bytes){int reads=ItemStack.Reads;ItemStack[] rows;A(!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(Convert.ToBase64String(bytes),3,49152,65536,out rows),"bad image");A(rows==null,"atomic null");A(reads==ItemStack.Reads,"no native row reads");}
static byte[] Raw(ItemStack[] rows){using(var stream=new MemoryStream()){var w=MemoryPools.poolBinaryWriter.Value;var old=w.Encoding;try{w.Encoding=new UTF8Encoding(false,false);w.SetBaseStream(stream);w.Write((byte)1);w.Write((byte)rows.Length);foreach(var row in rows)row.Write(w);w.Flush();return stream.ToArray();}finally{w.Encoding=old;}}}
static void BadWide(byte[] bytes){int reads=ItemStack.Reads;ItemStack[] result;A(!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(Convert.ToBase64String(bytes),3,196608,262144,out result),"wide malformed");A(result==null&&reads==ItemStack.Reads,"wide preflight atomic");}
public static int Main(){ItemClass.list[1]=new ItemClass{Name="book"};ItemClass.list[2]=new ItemClassModifier{Name="mod"};ItemClass.list[3]=new ItemClass{Name="backpack"};
var b=V(3);b.SetMetadata("unrelated","🙂保持");string before=RebirthNativeItemCodec.Encode(b);var rows=ItemStack.CreateArray(3);rows[0].count=2;rows[0].itemValue=V();rows[0].itemValue.modifications=new[]{V(2)};rows[0].itemValue.cosmeticMods=new[]{V(2)};rows[2].count=1;rows[2].itemValue=V();rows[2].itemValue.SetMetadata("text","🙂é");
var pw=Encoding.Unicode;var pr=Encoding.BigEndianUnicode;MemoryPools.poolBinaryWriter.Value.Encoding=pw;MemoryPools.poolBinaryReader.Value.Encoding=pr;
ItemValue lib,sale;A(RebirthBackpackLibraryContents.TryWrite(b,rows,out lib),"library write");A(RebirthBackpackSellStashContents.TryWrite(b,rows,out sale),"sale write");A(RebirthNativeItemCodec.Encode(b)==before,"original backpack unchanged");
ItemStack[] lr,sr;A(RebirthBackpackLibraryContents.TryRead(lib,out lr),"library read");A(RebirthBackpackSellStashContents.TryRead(sale,out sr),"sale read");for(int i=0;i<3;i++){A(lr[i].count==rows[i].count&&sr[i].count==rows[i].count,"count");if(rows[i].count>0){A(RebirthNativeItemCodec.Encode(lr[i].itemValue)==RebirthNativeItemCodec.Encode(rows[i].itemValue),"library full item");A(RebirthNativeItemCodec.Encode(sr[i].itemValue)==RebirthNativeItemCodec.Encode(rows[i].itemValue),"sale full item");}}
A(object.ReferenceEquals(pw,MemoryPools.poolBinaryWriter.Value.Encoding)&&object.ReferenceEquals(pr,MemoryPools.poolBinaryReader.Value.Encoding),"pool encoding restored");string encoded;A(lib.TryGetMetadata(RebirthBackpackLibraryContents.MetadataKey,out encoded),"image");var bytes=Convert.FromBase64String(encoded);for(int i=0;i<bytes.Length;i++){var cut=new byte[i];Array.Copy(bytes,cut,i);Bad(cut);}var wrong=(byte[])bytes.Clone();wrong[1]=4;Bad(wrong);wrong=(byte[])bytes.Clone();wrong[4]=8;Bad(wrong);wrong=new byte[bytes.Length+1];Array.Copy(bytes,wrong,bytes.Length);Bad(wrong);
RebirthBackpackStoragePayload.Allow=false;A(!RebirthBackpackLibraryContents.TryWrite(b,rows,out lib)&&lib==null,"payload denial atomic");RebirthBackpackStoragePayload.Allow=true;
PooledBinaryWriter.FlushFault=true;A(!RebirthBackpackSellStashContents.TryWrite(b,rows,out sale)&&sale==null,"flush exception atomic");PooledBinaryWriter.FlushFault=false;A(object.ReferenceEquals(pw,MemoryPools.poolBinaryWriter.Value.Encoding),"flush failure encoding restored");
 ItemStack[] blank;A(RebirthBackpackLibraryContents.TryRead(b,out blank)&&blank.Length==3&&blank.All(x=>x.IsEmpty()),"missing metadata empty capacity");A(!RebirthBackpackLibraryContents.TryRead(V(),out blank)&&blank==null,"no backpack capacity");
 ItemValue output;A(!RebirthBackpackLibraryContents.TryWrite(b,ItemStack.CreateArray(2),out output)&&output==null,"wrong capacity");
 var denied=ItemStack.CreateArray(3);denied[0].itemValue=V(2);denied[0].count=1;A(!RebirthBackpackLibraryContents.TryWrite(b,denied,out output)&&output==null,"library policy denial");A(RebirthBackpackSellStashContents.TryWrite(b,denied,out output),"sale broad policy retained");denied[0].count=101;A(!RebirthBackpackSellStashContents.TryWrite(b,denied,out output)&&output==null,"max native count");denied[0].count=-1;A(!RebirthBackpackSellStashContents.TryWrite(b,denied,out output)&&output==null,"negative count");
 var cycle=V();cycle.modifications=new[]{cycle};var cyclic=ItemStack.CreateArray(3);cyclic[0].count=1;cyclic[0].itemValue=cycle;string rejected;A(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(cyclic,49152,65536,out rejected)&&rejected==null,"cycle bounded before native writer");
 ItemValue chain=V();for(int j=0;j<63;j++){var parent=V();parent.modifications=new[]{chain};chain=parent;}var deep=ItemStack.CreateArray(3);deep[0].count=1;deep[0].itemValue=chain;A(RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(deep,49152,65536,out rejected),"depth64 write");var tooDeep=V();tooDeep.modifications=new[]{chain};deep[0].itemValue=tooDeep;A(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(deep,49152,65536,out rejected)&&rejected==null,"depth65 write refusal");
 var huge=V();huge.SetMetadata("x",new string('a',65537));deep[0].itemValue=huge;A(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(deep,98304,131072,out rejected)&&rejected==null,"string limit");
 var aggregate=ItemStack.CreateArray(3);for(int j=0;j<3;j++){aggregate[j].count=1;aggregate[j].itemValue=V();aggregate[j].itemValue.SetMetadata("x",new string('a',45000));}A(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(aggregate,196608,262144,out rejected)&&rejected==null,"aggregate across rows");
 var unchanged=RebirthNativeItemCodec.Encode(b);A(RebirthBackpackLibraryContents.TryWrite(b,rows,out output),"repeat library write");string unrelated;A(output.TryGetMetadata("unrelated",out unrelated)&&unrelated=="🙂保持","unrelated outer metadata retained");A(RebirthNativeItemCodec.Encode(b)==unchanged,"outer source unchanged after repeat");
 var corrupt=b.Clone();corrupt.SetMetadata(RebirthBackpackLibraryContents.MetadataKey," "+encoded);A(!RebirthBackpackLibraryContents.TryRead(corrupt,out blank)&&blank==null,"canonical metadata base64 enforced");
 BadWide(Raw(aggregate));BadWide(Raw(deep));deep[0].itemValue=tooDeep;BadWide(Raw(deep));
 var invalidRows=ItemStack.CreateArray(3);invalidRows[0].count=1;invalidRows[0].itemValue=V();invalidRows[0].itemValue.SetMetadata("x","UNIQUEUTF8");var malformed=Raw(invalidRows);var marker=Encoding.UTF8.GetBytes("UNIQUEUTF8");int at=-1;for(int j=0;j<=malformed.Length-marker.Length;j++){if(marker.Select((v,k)=>malformed[j+k]==v).All(v=>v)){at=j;break;}}A(at>=0,"locate native UTF8 payload");malformed[at]=255;BadWide(malformed);
 var emptyImage=Raw(ItemStack.CreateArray(3));A(emptyImage.SequenceEqual(new byte[]{1,3,0,0,0,0,0,0}),"empty rows exact native framing");var countzero=(byte[])bytes.Clone();countzero[2]=0;countzero[3]=0;BadWide(countzero);
 int sets=PooledBinaryWriter.StreamSets;A(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(cyclic,49152,65536,out rejected)&&PooledBinaryWriter.StreamSets==sets,"cyclic refusal before writer stream");deep[0].itemValue=huge;A(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(deep,98304,131072,out rejected)&&PooledBinaryWriter.StreamSets==sets,"string refusal before writer stream");A(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(aggregate,196608,262144,out rejected)&&PooledBinaryWriter.StreamSets==sets,"aggregate refusal before writer stream");
 var alias=V(2);var shared=V();shared.modifications=new[]{alias,alias};shared.cosmeticMods=new[]{alias};var aliases=ItemStack.CreateArray(3);aliases[0].count=1;aliases[0].itemValue=shared;aliases[2].count=1;aliases[2].itemValue=shared;A(RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(aliases,49152,65536,out rejected),"shared acyclic aliases accepted per serialized occurrence");ItemStack[] copies;A(RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(rejected,3,49152,65536,out copies)&&RebirthNativeItemCodec.Encode(copies[0].itemValue)==RebirthNativeItemCodec.Encode(copies[2].itemValue),"alias rows detached equal payload");
 foreach(var key in new[]{RebirthBackpackLibraryContents.MetadataKey,RebirthBackpackSellStashContents.MetadataKey})foreach(var invalid in new TypedMetadataValue[]{new TypedMetadataValue(7,TypedMetadataValue.TypeTag.Integer),new TypedMetadataValue(1.5f,TypedMetadataValue.TypeTag.Float),null,new TypedMetadataValue(null,TypedMetadataValue.TypeTag.String),new TypedMetadataValue(7,TypedMetadataValue.TypeTag.String)}){var corruptStore=b.Clone();corruptStore.Metadata[key]=invalid;var snapshot=corruptStore.Metadata.Count;var held=corruptStore.Metadata[key];ItemStack[] invalidOut=rows;ItemValue invalidCandidate=b;bool isLibrary=key==RebirthBackpackLibraryContents.MetadataKey;A(!(isLibrary?RebirthBackpackLibraryContents.TryRead(corruptStore,out invalidOut):RebirthBackpackSellStashContents.TryRead(corruptStore,out invalidOut))&&invalidOut==null,"reserved corrupt read atomic");A(!(isLibrary?RebirthBackpackLibraryContents.TryWrite(corruptStore,rows,out invalidCandidate):RebirthBackpackSellStashContents.TryWrite(corruptStore,rows,out invalidCandidate))&&invalidCandidate==null,"reserved corrupt cannot overwrite");A(object.ReferenceEquals(held,corruptStore.Metadata[key])&&corruptStore.Metadata.Count==snapshot,"reserved corrupt source unchanged");}
Console.WriteLine("PASS "+n+" current Contents + canonical stack-array integration");return 0;}}public class CraftCompleteData{public int CrafterEntityID;public ItemStack CraftedItemStack;public string RecipeName="",ItemScrapped="";public int CraftExpGain;public ushort RecipeUsedCount;public CraftCompleteData(){}public CraftCompleteData(int crafterEntityID, ItemStack craftedItemStack, string recipeName, string itemScrapped, int craftExpGain, ushort recipeUsedCount)
	{
		CrafterEntityID = crafterEntityID;
		CraftedItemStack = craftedItemStack;
		RecipeName = recipeName;
		ItemScrapped = itemScrapped;
		RecipeUsedCount = recipeUsedCount;
		CraftExpGain = craftExpGain;
	}public void Read(PooledBinaryReader _br)
	{
		_br.ReadUInt16();
		CrafterEntityID = _br.ReadInt32();
		CraftedItemStack = new ItemStack().Read(_br);
		RecipeName = _br.ReadString();
		CraftExpGain = _br.ReadInt32();
		RecipeUsedCount = _br.ReadUInt16();
		ItemScrapped = _br.ReadString();
	}public void Write(PooledBinaryWriter _bw)
	{
		_bw.Write((ushort)1);
		_bw.Write(CrafterEntityID);
		CraftedItemStack.Write(_bw);
		_bw.Write(RecipeName);
		_bw.Write(CraftExpGain);
		_bw.Write(RecipeUsedCount);
		_bw.Write(ItemScrapped);
	}}
// Semantic filter for trusted native serializer spans. Not authentication or settlement authority.
internal static class RebirthStationTerminalContents
{
    internal static bool Matches(byte[] outputBytes,byte[] completionBytes,RebirthStationGridAdmission admission,
        Recipe savedRecipe,CraftCompleteData expected,IList<ItemStack> expectedOutput)
    {
        if(outputBytes==null||completionBytes==null||outputBytes.Length<1||completionBytes.Length<2||
            outputBytes.Length>256*1024||completionBytes.Length>256*1024||expectedOutput==null||
            expectedOutput.Count<1||expectedOutput.Count>255||
            !RebirthStationCompletionReceipt.MatchesAdmission(admission,savedRecipe,expected))return false;
        try
        {
            if(!TryReadOutput(outputBytes,expectedOutput.Count,out var output))return false;
            for(int i=0;i<output.Length;i++)
            {
                var item=output[i];
                if(item?.itemValue==null||item.count<0||expectedOutput[i]?.itemValue==null||expectedOutput[i].count<0||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(item,expectedOutput[i]))return false;
            }
            if(!RebirthNativeItemConformanceReader.TryDecodeStationCompletions(completionBytes,out var receipts))return false;
            int matches=0;
            foreach(var data in receipts)
            {
                if(data.CraftedItemStack?.itemValue==null||data.CraftedItemStack.count<0)return false;
                    bool marked=false;var metadata=data.CraftedItemStack.itemValue.Metadata;
                    if(metadata!=null)foreach(var key in metadata.Keys)if(key.StartsWith(RebirthStationCompletionReceipt.Prefix,StringComparison.Ordinal)){marked=true;break;}
                    if(!marked)continue;
                    if(!RebirthStationCompletionReceipt.TryReadIdentity(data,out var identity))return false;
                    if(identity.Job!=admission.JobId)continue;
                    if(++matches!=1||!RebirthStationCompletionReceipt.MatchesAdmission(admission,savedRecipe,data)||
                        data.CrafterEntityID!=expected.CrafterEntityID||data.RecipeName!=expected.RecipeName||
                        data.ItemScrapped!=expected.ItemScrapped||data.CraftExpGain!=expected.CraftExpGain||
                        data.RecipeUsedCount!=expected.RecipeUsedCount||
                        !RebirthStationGridIngredients.IsSameStackSnapshot(data.CraftedItemStack,expected.CraftedItemStack))return false;
                }
                return matches==1;
        }
        catch{return false;}
    }
    // Native output framing already has the row count. Add only the canonical codec version.
    internal static bool TryReadOutput(byte[] bytes,int expectedCount,out ItemStack[] output)
    {
        output=null;
        if(bytes==null||bytes.Length<1||bytes.Length>256*1024||expectedCount<1||expectedCount>255||
            bytes[0]!=expectedCount)return false;
        try
        {
            var framed=new byte[bytes.Length+1];framed[0]=1;
            Buffer.BlockCopy(bytes,0,framed,1,bytes.Length);
            int maximumBytes=256*1024+1;
            int maximumText=4*((maximumBytes+2)/3);
            return RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(Convert.ToBase64String(framed),
                expectedCount,maximumBytes,maximumText,out output);
        }
        catch{output=null;return false;}
    }
}// Outer admission/recipe identity adapters; production terminal filter and native receipt/item wire are actual sources.
public class Recipe{public ushort Version=1;public int itemValueType=1,count=1,craftExpGain;public bool IsScrap;public int craftingToolType,craftingTier;public string tags="";public float craftingTime;public string craftingArea="";public List<ItemStack> ingredients=new List<ItemStack>();public ItemClass GetOutputItemClass(){return ItemClass.GetForId(itemValueType);}public void Write(PooledBinaryWriter _bw)
	{
		_bw.Write(Version);
		_bw.Write(itemValueType);
		_bw.Write(count);
		_bw.Write(IsScrap);
		_bw.Write(craftingTime);
		_bw.Write(craftExpGain);
		_bw.Write(craftingArea ?? "");
		_bw.Write(ingredients.Count);
		for (int i = 0; i < ingredients.Count; i++)
		{
			ingredients[i].Write(_bw);
		}
	}public static Recipe Read(PooledBinaryReader _br)
	{
		_br.ReadUInt16();
		Recipe recipe = new Recipe();
		recipe.itemValueType = _br.ReadInt32();
		recipe.count = _br.ReadInt32();
		recipe.IsScrap = _br.ReadBoolean();
		recipe.craftingTime = _br.ReadSingle();
		recipe.craftExpGain = _br.ReadInt32();
		recipe.craftingArea = _br.ReadString();
		int num = _br.ReadInt32();
		recipe.ingredients = new List<ItemStack>(num);
		for (int i = 0; i < num; i++)
		{
			recipe.ingredients.Add(new ItemStack().Read(_br));
		}
		return recipe;
	}public void AddIngredients(List<ItemStack> _items)
	{
		ingredients.AddRange(_items);
	}}

public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b){return a.count==b.count&&RebirthNativeItemCodec.Encode(a.itemValue)==RebirthNativeItemCodec.Encode(b.itemValue);}}
public static class RebirthStationCompletionReceipt{
public const string Prefix="rebirth.station.receipt.";public class Identity{public string Job;}
public static bool HasReservedMarker(CraftCompleteData d){return d.CraftedItemStack.itemValue.Metadata.Keys.Any(k=>k.StartsWith(Prefix,StringComparison.Ordinal));} public static bool TryReadIdentity(CraftCompleteData d,out Identity id){id=null;string job;if(!d.CraftedItemStack.itemValue.TryGetMetadata(Prefix+"job",out job)||job=="")return false;id=new Identity{Job=job};return true;}
public static bool MatchesAdmission(RebirthStationGridAdmission a,Recipe r,CraftCompleteData d){Identity id;return a!=null&&r!=null&&d!=null&&TryReadIdentity(d,out id)&&id.Job==a.JobId;}}
public class RecipeQueueItem{public Recipe Recipe;public short Multiplier;public float CraftingTimeLeft,OneItemCraftTime=-1f;public bool IsCrafting;public ItemValue RepairItem;public ushort AmountToRepair;public byte Quality;public int StartingEntityId=-1;public void Write(PooledBinaryWriter _bw)
	{
		_bw.Write((ushort)2);
		_bw.Write(Multiplier);
		_bw.Write(IsCrafting);
		_bw.Write(CraftingTimeLeft);
		bool flag = RepairItem != null;
		_bw.Write(flag);
		if (flag)
		{
			RepairItem.Write(_bw);
			_bw.Write(AmountToRepair);
		}
		_bw.Write(Quality);
		_bw.Write(StartingEntityId);
		_bw.Write(OneItemCraftTime);
		bool flag2 = Recipe != null;
		_bw.Write(flag2);
		if (flag2)
		{
			Recipe.Write(_bw);
		}
		if (flag2)
		{
			ItemClass outputItemClass = Recipe.GetOutputItemClass();
			(outputItemClass.IsBlock() ? Block.nameIdMapping : ItemClass.nameIdMapping)?.AddMapping(outputItemClass.Id, outputItemClass.Name);
		}
	}public void Read(PooledBinaryReader _br)
	{
		ushort num = _br.ReadUInt16();
		int num2 = 0;
		if (num < 2)
		{
			num2 = _br.ReadInt32();
		}
		Multiplier = _br.ReadInt16();
		IsCrafting = _br.ReadBoolean();
		CraftingTimeLeft = _br.ReadSingle();
		if (_br.ReadBoolean())
		{
			RepairItem = ItemValue.ReadOrNull(_br);
			AmountToRepair = _br.ReadUInt16();
		}
		Quality = _br.ReadByte();
		StartingEntityId = _br.ReadInt32();
		OneItemCraftTime = _br.ReadSingle();
		if (num >= 2)
		{
			if (_br.ReadBoolean())
			{
				Recipe = Recipe.Read(_br);
			}
		}
		else if (_br.ReadBoolean())
		{
			Recipe = new Recipe();
			Recipe.itemValueType = _br.ReadInt32();
			Recipe.count = _br.ReadInt32();
			int num3 = _br.ReadInt32();
			Recipe.ingredients = new List<ItemStack>();
			for (int i = 0; i < num3; i++)
			{
				Recipe.ingredients.Add(new ItemStack().Read(_br));
			}
			Recipe.craftingTime = _br.ReadSingle();
			Recipe.craftExpGain = _br.ReadInt32();
			Recipe.IsScrap = _br.ReadBoolean();
		}
		else if (num2 != 0)
		{
			Log.Warning("[RecipeQueueItem] In-progress recipe has outdata data and has been discarded.");
		}
	}}// Original event/publication/paid catalogue boundaries are adapters; projection, item/receipt wire and region readers are production.
public class RebirthStationGridAdmission{public string JobId="original",CreationId="character",DefinitionId=new string('A',64);public XElement Write(){return new XElement("admission",new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("definition",DefinitionId));}public bool TryMaterialize(IList<Recipe> defs,out Recipe r,out int a,out int b){r=defs==null||defs.Count!=1?null:defs[0];a=b=0;return r!=null;}}
public class RebirthStationTerminalIntent{public XElement Write(){return new XElement("intent",new XAttribute("actor",42));}}
public class RebirthStationPublicationRecord{public XElement Write(){return new XElement("queued");}public static string Digest(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}}
public class RebirthStationCompletionPublication{
public RebirthStationCompletionPublication Clone()=>new(){Node=new XElement(Node)};public XElement Node;public XElement Write(){return new XElement(Node);}public static string Binding(XElement n){return RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(n.ToString(SaveOptions.DisableFormatting)));}public static bool ValidOriginals(RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q){return a!=null&&i!=null&&q!=null;}public static bool TryRead(XElement n,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,out RebirthStationCompletionPublication p){p=new RebirthStationCompletionPublication{Node=new XElement(n)};return ValidOriginals(a,i,q);}public static bool TryCreate(string root,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,RebirthStationCompletionExpectation e,RebirthStationSnapshotEvidence.Publication proof,out RebirthStationCompletionPublication p){p=proof.Record;return e!=null&&proof.Record!=null;}public bool Revalidate(string root,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,RebirthStationCompletionExpectation e){return e.Current;}}
public class RebirthStationCompletionExpectation{public bool Current=true;public bool IsBound(RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q){return Current;}public bool MatchesLive(object station){return Current;}public bool MatchesStation(byte[] a,byte[] b){return Current;}public bool MatchesTerminal(byte[] a,byte[] b){return Current;}}
public class RebirthStationCompletionOriginalScope{public static StringComparison? TestComparison;public static StringComparison PathComparison=>TestComparison??(Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);internal ProjectionOwner Owner;internal RebirthStablePlayerIdentity Identity=new();public bool Current=true;public RebirthStationGridAdmission Admission;public RebirthStationPublicationRecord Queued;public string SaveRoot;public object Station;public bool IsCurrent(){return Current;}}
public class RebirthStationCompletionCapture{public class SuccessfulOutput{public RebirthStationCompletionOriginalScope Original;public bool Current=true;public bool MatchesReceipt(){return Current;}}}
public class RebirthStationSnapshotEvidence{public static Publication Proof;public static bool TryGetPublished(object request,out Publication proof){proof=Proof;return proof!=null;}public const int MaximumSnapshotBytes=8*1024*1024;public class Publication{public RebirthStationCompletionPublication Record;}}
public class XUiM_Recipes{public static IList<Recipe> Defs;public static IList<Recipe> GetRecipes(){return Defs;}}
public class SdFile{public static Stream Open(string p,FileMode m,FileAccess a,FileShare s){return File.Open(p,m,a,s);}}
public static class RebirthStationGridQueue{public const string Prefix="rebirth.station.queue.";public static bool HasValidMultiplier(Recipe r,int m){return m==1;}public static bool IsMarked(Recipe r){return false;}public static bool TryGetJobId(Recipe r,out string id){id=null;return false;}public static bool TryGetDefinitionBinding(Recipe r,IList<Recipe> d,out object b){b=null;return false;}}
public static class ProjectionCheck{
static int checks;static void A(bool b,string s){checks++;if(!b)throw new Exception(s);}
static byte[] Bytes(Action<PooledBinaryWriter> action){using(var stream=new MemoryStream()){var writer=MemoryPools.poolBinaryWriter.Value;writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);action(writer);writer.Flush();return stream.ToArray();}}
static byte[] Pack(byte[] raw){using(var stream=new MemoryStream()){stream.Write(new byte[]{116,116,99,0,47,0,0,0},0,8);var zip=new DeflateOutputStream(stream,3,true);zip.Write(raw,0,raw.Length);zip.Restart();return stream.ToArray();}}
static byte[] Region(byte[] payload){int sectors=(payload.Length+16+4095)/4096;var file=new byte[(3+sectors)*4096];file[0]=55;file[1]=114;file[2]=103;file[3]=1;file[4096]=3;file[4099]=(byte)sectors;Buffer.BlockCopy(BitConverter.GetBytes(payload.Length),0,file,12288,4);Buffer.BlockCopy(payload,0,file,12304,payload.Length);return file;}
public static int Main(){ItemClass.list[1]=new ItemClass{Name="output"};var stack=new ItemStack{count=2,itemValue=new ItemValue(1){Meta=-1,Seed=17,TextureFullArray=new TextureFullArray(-23)}};stack.itemValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+"job","original");var row=new CraftCompleteData(42,stack,"recipe","",13,1);var parts=new[]{Bytes(w=>{w.Write((byte)1);stack.Write(w);}),Bytes(w=>{w.Write((byte)1);new RecipeQueueItem().Write(w);}),Bytes(w=>{w.Write((byte)1);stack.Write(w);}),Bytes(w=>{w.Write((short)1);row.Write(w);})};string root=Path.Combine(Path.GetTempPath(),"rb-projection-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Region"));string path=Path.Combine(root,"Region/r.0.0.7rg");var payload=Pack(parts.SelectMany(p=>p).ToArray());File.WriteAllBytes(path,Region(payload));var publication=new RebirthStationCompletionPublication{Node=new XElement("completed",new XAttribute("region","Region/r.0.0.7rg"),new XAttribute("chunkX",0),new XAttribute("chunkZ",0),new XAttribute("payload",RebirthStationPublicationRecord.Digest(payload)))};string[] names={"input","queue","output","completion"};int offset=8;for(int i=0;i<4;i++){publication.Node.Add(new XAttribute(names[i]+"Start",offset),new XAttribute(names[i]+"Length",parts[i].Length),new XAttribute(names[i]+"Digest",RebirthStationPublicationRecord.Digest(parts[i])));offset+=parts[i].Length;}
var admission=new RebirthStationGridAdmission();var intent=new RebirthStationTerminalIntent();var queued=new RebirthStationPublicationRecord();XUiM_Recipes.Defs=new[]{new Recipe()};var original=new RebirthStationCompletionOriginalScope{Admission=admission,Queued=queued,SaveRoot=root};var e=new RebirthStationCompletionCapture.SuccessfulOutput{Original=original};var expected=new RebirthStationCompletionExpectation();var proof=new RebirthStationSnapshotEvidence.Publication{Record=publication};RebirthStationCompletionExpectationProjection projection;
A(!RebirthStationCompletionExpectationProjection.TryCapture(null,expected,intent,publication,proof,out projection)&&projection==null,"no event refuses");A(RebirthStationCompletionExpectationProjection.TryCapture(e,expected,intent,publication,proof,out projection),"capture reads actual region");A(projection.CompareCold(root,admission,intent,queued,publication,XUiM_Recipes.Defs),"cold actual region comparison");var node=projection.Write();node.Element("output").Value="AAAA";A(projection.CompareCold(root,admission,intent,queued,publication,XUiM_Recipes.Defs),"Write detached");var clone=projection.Clone();A(clone.CompareCold(root,admission,intent,queued,publication,XUiM_Recipes.Defs),"clone detached");A(!projection.CompareCold(root,admission,intent,queued,publication,new Recipe[0]),"changed catalogue refuses");original.Current=false;A(!RebirthStationCompletionExpectationProjection.TryCapture(e,expected,intent,publication,proof,out var refused)&&refused==null,"stale original capture refuses");original.Current=true;
foreach(string key in new[]{"job","creation","admission","intent","queued","definition","completed"}){var bad=projection.Write();bad.SetAttributeValue(key,"bad");A(!RebirthStationCompletionExpectationProjection.TryRead(bad,admission,intent,queued,publication,XUiM_Recipes.Defs,out refused)&&refused==null,"original binding refused");}
foreach(string name in names){var bad=projection.Write();bad.Element(name).Value=" "+bad.Element(name).Value;A(!RebirthStationCompletionExpectationProjection.TryRead(bad,admission,intent,queued,publication,XUiM_Recipes.Defs,out refused),"noncanonical payload refuses");bad=projection.Write();bad.Add(new XElement(bad.Element(name)));A(!RebirthStationCompletionExpectationProjection.TryRead(bad,admission,intent,queued,publication,XUiM_Recipes.Defs,out refused),"duplicate span refuses");}
var aa=new Dictionary<string,RebirthStationGridAdmission>{{admission.JobId,admission}};var ii=new Dictionary<string,RebirthStationTerminalIntent>{{admission.JobId,intent}};var qq=new Dictionary<string,RebirthStationPublicationRecord>{{admission.JobId,queued}};var cc=new Dictionary<string,RebirthStationCompletionPublication>{{admission.JobId,publication}};var records=new Dictionary<string,RebirthStationCompletionExpectationProjection>{{admission.JobId,projection}};var section=RebirthStationCompletionExpectationProjection.WriteAll(records,aa,ii,qq,cc,XUiM_Recipes.Defs);Dictionary<string,RebirthStationCompletionExpectationProjection> loaded;A(RebirthStationCompletionExpectationProjection.ReadAll(new XElement("progression",section),aa,ii,qq,cc,XUiM_Recipes.Defs,out loaded)&&loaded.Count==1,"collection roundtrip");A(RebirthStationCompletionExpectationProjection.ReadAll(new XElement("progression"),aa,ii,qq,cc,XUiM_Recipes.Defs,out loaded)&&loaded.Count==0,"old absence");var duplicate=new XElement(section);duplicate.Add(projection.Write());A(!RebirthStationCompletionExpectationProjection.ReadAll(new XElement("progression",duplicate),aa,ii,qq,cc,XUiM_Recipes.Defs,out loaded)&&loaded==null,"duplicate job atomic");var many=new XElement("stationCompletionExpectationProjections",new XAttribute("version",1));for(int i=0;i<65;i++)many.Add(projection.Write());A(!RebirthStationCompletionExpectationProjection.ReadAll(new XElement("progression",many),aa,ii,qq,cc,XUiM_Recipes.Defs,out loaded)&&loaded==null,"65 entries preparse");var oversized=new XElement("stationCompletionExpectationProjections",new XAttribute("version",1));for(int i=0;i<8;i++){var p=projection.Write();foreach(var child in p.Elements())child.Value=new string('A',349528);oversized.Add(p);}A(!RebirthStationCompletionExpectationProjection.ReadAll(new XElement("progression",oversized),aa,ii,qq,cc,XUiM_Recipes.Defs,out loaded)&&loaded==null,"global8MiB before decode");
var originalFile=File.ReadAllBytes(path);File.WriteAllBytes(path,originalFile.Take(originalFile.Length-1).ToArray());A(!projection.CompareCold(root,admission,intent,queued,publication,XUiM_Recipes.Defs),"truncated native region refuses");File.WriteAllBytes(path,originalFile);var corrupt=(byte[])originalFile.Clone();corrupt[12304+8]^=1;File.WriteAllBytes(path,corrupt);A(!projection.CompareCold(root,admission,intent,queued,publication,XUiM_Recipes.Defs),"changed native compressed payload refuses");File.WriteAllBytes(path,originalFile);A(projection.CompareCold(root,admission,intent,queued,publication,XUiM_Recipes.Defs),"same original file restored");var invalidQueuePublication=new RebirthStationCompletionPublication{Node=new XElement(publication.Node)};invalidQueuePublication.Node.SetAttributeValue("queueLength",1);invalidQueuePublication.Node.SetAttributeValue("queueDigest",RebirthStationPublicationRecord.Digest(new byte[]{0}));var invalidQueueProjection=projection.Write();invalidQueueProjection.SetAttributeValue("completed",RebirthStationCompletionPublication.Binding(invalidQueuePublication.Write()));invalidQueueProjection.Element("queue").Value=Convert.ToBase64String(new byte[]{0});A(!RebirthStationCompletionExpectationProjection.TryRead(invalidQueueProjection,admission,intent,queued,invalidQueuePublication,XUiM_Recipes.Defs,out refused)&&refused==null,"fresh matching hashes do not admit malformed native queue");var noncanonicalQueue=(byte[])parts[1].Clone();noncanonicalQueue[5]=2;invalidQueuePublication.Node.SetAttributeValue("queueLength",noncanonicalQueue.Length);invalidQueuePublication.Node.SetAttributeValue("queueDigest",RebirthStationPublicationRecord.Digest(noncanonicalQueue));invalidQueueProjection.SetAttributeValue("completed",RebirthStationCompletionPublication.Binding(invalidQueuePublication.Write()));invalidQueueProjection.Element("queue").Value=Convert.ToBase64String(noncanonicalQueue);A(!RebirthStationCompletionExpectationProjection.TryRead(invalidQueueProjection,admission,intent,queued,invalidQueuePublication,XUiM_Recipes.Defs,out refused),"fresh matching hashes do not admit noncanonical boolean queue");var siblingCase=new XElement(publication.Node);string leaf=Path.GetFileName(root);siblingCase.SetAttributeValue("region","../"+leaf.ToUpperInvariant()+"/Region/r.0.0.7rg");var read=typeof(RebirthStationCompletionExpectationProjection).GetMethod("ReadNative",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);var args=new object[]{root,siblingCase,null};RebirthStationCompletionOriginalScope.TestComparison=StringComparison.Ordinal;A(!(bool)read.Invoke(null,args),"Unix ordinal sibling-case containment refuses before file read");RebirthStationCompletionOriginalScope.TestComparison=StringComparison.OrdinalIgnoreCase;if(Path.DirectorySeparatorChar=='\\')A((bool)read.Invoke(null,new object[]{root,siblingCase,null}),"Windows same-folder case alias uses ignorecase containment");RebirthStationCompletionOriginalScope.TestComparison=null;var owner=new ProjectionOwner{Progression=new(){StationPreparations=aa,StationTerminalIntents=ii,StationPublications=qq,StationCompletionPublications=cc,StationCompletionExpectationProjections=records}};
RebirthWorldCharacterRepository.Owner=owner;string ownerFile=Path.Combine(root,"owner.xml");var identity=new RebirthStablePlayerIdentity();
void SaveOwner(bool include=true){var xml=new XElement("progression",new XAttribute("owner","owner"),new XAttribute("creation","character"));if(include)xml.Add(RebirthStationCompletionExpectationProjection.WriteAll(records,aa,ii,qq,cc,XUiM_Recipes.Defs));xml.Save(ownerFile);RebirthWorldCharacterRepository.PathValue=ownerFile;RebirthWorldCharacterRepository.serverAuthority=true;RebirthWorldCharacterRepository.Migrated=false;RebirthWorldCharacterRepository.ThrowRead=false;RebirthWorldCharacterRepository.Callback=null;}
bool Paired()=>RebirthWorldCharacterRepository.HasSavedStationCompletionExpectationProjection(identity,admission,intent,queued,publication,projection);
SaveOwner();A(Paired(),"candidate exact saved pair witness");SaveOwner(false);A(!Paired(),"legacy completed without projection cannot prove new pair");
SaveOwner();RebirthWorldCharacterRepository.Migrated=true;A(!Paired(),"paired migrated refusal");SaveOwner();RebirthWorldCharacterRepository.ThrowRead=true;A(!Paired(),"paired unreadable refusal");
SaveOwner();RebirthWorldCharacterRepository.Callback=()=>RebirthWorldCharacterRepository.PathValue+="other";A(!Paired(),"paired path substitution");SaveOwner();RebirthWorldCharacterRepository.Callback=()=>RebirthWorldCharacterRepository.serverAuthority=false;A(!Paired(),"paired authority substitution");
SaveOwner();File.Copy(ownerFile,ownerFile+".bak",true);File.Delete(ownerFile);A(!Paired(),"paired backup only refuses");SaveOwner();File.WriteAllText(ownerFile,"<broken>");A(!Paired(),"paired malformed final refuses");
SaveOwner();var wrongOwner=XElement.Load(ownerFile);wrongOwner.SetAttributeValue("owner","foreign");wrongOwner.Save(ownerFile);A(!Paired(),"paired foreign owner refuses");SaveOwner();var wrongCreation=XElement.Load(ownerFile);wrongCreation.SetAttributeValue("creation","foreign");wrongCreation.Save(ownerFile);A(!Paired(),"paired foreign creation refuses");
SaveOwner();var malformed=XElement.Load(ownerFile);malformed.Element("stationCompletionExpectationProjections").Add(new XElement("bad"));malformed.Save(ownerFile);A(!Paired(),"second malformed record refuses atomic witness");
A(RebirthStationCompletionExpectationProjection.ReadAll(new XElement("progression"),aa,ii,qq,cc,null,out loaded)&&loaded.Count==0,"legacy absence unavailable catalogue loadable");
A(!RebirthStationCompletionExpectationProjection.ReadAll(new XElement("progression",section),aa,ii,qq,cc,null,out loaded)&&loaded==null,"nonempty unavailable catalogue refuses");
owner.Progression.StationCompletionPublications=new();owner.Progression.StationCompletionExpectationProjections=new();original.Owner=owner;original.Identity=identity;RebirthStationSnapshotEvidence.Proof=proof;var observation=new CandidateObservation(e,intent,expected);int saveCalls=0;
RebirthWorldCharacterRepository.SaveCallback=()=>{saveCalls++;return false;};A(!observation.TrySavePublication(out var observed)&&observed==null&&owner.Progression.StationCompletionPublications.Count==1&&owner.Progression.StationCompletionExpectationProjections.Count==1,"actual candidate uncertain save retains both");
var savedC=owner.Progression.StationCompletionPublications[admission.JobId];var savedP=owner.Progression.StationCompletionExpectationProjections[admission.JobId];
RebirthWorldCharacterRepository.SaveCallback=()=>{saveCalls++;var state=owner.Progression;new XElement("progression",new XAttribute("owner","owner"),new XAttribute("creation","character"),RebirthStationCompletionExpectationProjection.WriteAll(state.StationCompletionExpectationProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,XUiM_Recipes.Defs)).Save(ownerFile);RebirthWorldCharacterRepository.PathValue=ownerFile;RebirthWorldCharacterRepository.serverAuthority=true;RebirthWorldCharacterRepository.Callback=null;return true;};
A(observation.TrySavePublication(out observed)&&observed!=null&&saveCalls==2&&ReferenceEquals(savedC,owner.Progression.StationCompletionPublications[admission.JobId])&&ReferenceEquals(savedP,owner.Progression.StationCompletionExpectationProjections[admission.JobId]),"actual candidate retry SAME pair and final witness");
RebirthWorldCharacterRepository.SaveCallback=()=>throw new IOException("uncertain");A(!observation.TrySavePublication(out observed)&&owner.Progression.StationCompletionExpectationProjections.Count==1,"actual candidate save throw retains pair");
owner.Progression.StationCompletionExpectationProjections.Clear();saveCalls=0;RebirthWorldCharacterRepository.SaveCallback=()=>{saveCalls++;return true;};A(!observation.TrySavePublication(out observed)&&saveCalls==0,"actual candidate one sided legacy retains refuses promotion");
owner.Progression.StationCompletionExpectationProjections[admission.JobId]=savedP;RebirthWorldCharacterRepository.SaveCallback=()=>{RebirthWorldCharacterRepository.PathValue=ownerFile;RebirthWorldCharacterRepository.Callback=()=>owner.Progression.StationCompletionExpectationProjections[admission.JobId]=savedP.Clone();return true;};
A(!observation.TrySavePublication(out observed)&&observed==null,"actual candidate witness callback replaced projection refuses");
var emptyState=new ProjectionState();var catalogue=XUiM_Recipes.Defs;XUiM_Recipes.Defs=null;A(!CandidateHooks.Write(emptyState).HasElements,"exact writer hook canonical empty omitted");A(CandidateHooks.Read(new XElement("progression"),emptyState,out var hookError),"exact reader hook absent with unavailable catalogue");XUiM_Recipes.Defs=catalogue;
var hookState=new ProjectionState{StationPreparations=aa,StationTerminalIntents=ii,StationPublications=qq,StationCompletionPublications=cc,StationCompletionExpectationProjections=records};var hookXml=CandidateHooks.Write(hookState);A(hookXml.Elements().Count()==1,"exact writer hook nonempty");var readState=new ProjectionState{StationPreparations=aa,StationTerminalIntents=ii,StationPublications=qq,StationCompletionPublications=cc};A(CandidateHooks.Read(hookXml,readState,out hookError)&&readState.StationCompletionExpectationProjections.Count==1,"exact read hook composed valid");var badHook=new XElement(hookXml);badHook.Element("stationCompletionExpectationProjections").Add(new XElement("bad"));readState.StationCompletionExpectationProjections.Clear();A(!CandidateHooks.Read(badHook,readState,out hookError)&&readState.StationCompletionExpectationProjections.Count==0,"exact read hook no partial insertion");
var frozenNode=projection.Write();XUiM_Recipes.Defs=null;A(RebirthStationCompletionExpectationProjection.TryReadStored(frozenNode,admission,intent,queued,publication,out var rawStored),"data-only exact bound spans survive unavailable catalogue");A(!RebirthStationCompletionExpectationProjection.TryRead(frozenNode,admission,intent,queued,publication,null,out refused),"raw stored evidence never bypasses positive semantic gate");
A(RebirthStationCompletionExpectationProjection.ReadAllStored(new XElement("progression",section),aa,ii,qq,cc,out loaded)&&loaded.Count==1,"data-only nonempty collection before catalogue");A(RebirthStationCompletionExpectationProjection.WriteAllStored(loaded,aa,ii,qq,cc)!=null,"data-only dirtysave preserves existing immutable evidence");
foreach(var badSpan in names){var bad=frozenNode;bad=new XElement(bad);bad.Element(badSpan).Value="AAAA";A(!RebirthStationCompletionExpectationProjection.TryReadStored(bad,admission,intent,queued,publication,out rawStored)&&rawStored==null,"raw digest mismatch still refused "+badSpan);}
A(CandidateHooks.Read(new XElement("progression",section),new ProjectionState{StationPreparations=aa,StationTerminalIntents=ii,StationPublications=qq,StationCompletionPublications=cc},out hookError),"exact candidate repository stored read no registry access");XUiM_Recipes.Defs=catalogue;
var registry=ItemClass.list;ItemClass.list=new ItemClass[0];XUiM_Recipes.Defs=new Recipe[0];A(RebirthStationCompletionExpectationProjection.TryReadStored(frozenNode,admission,intent,queued,publication,out rawStored),"raw persistent read needs neither native item registry nor recipe catalogue");A(rawStored.Clone().Write().ToString()==rawStored.Write().ToString(),"raw immutable clone without registries");
A(RebirthStationCompletionExpectationProjection.ReadAllStored(new XElement("progression",section),aa,ii,qq,cc,out loaded)&&loaded.Count==1,"data-only collection empty native registries");A(RebirthStationCompletionExpectationProjection.WriteAllStored(loaded,aa,ii,qq,cc)!=null,"data-only existing save empty native registries");
A(!rawStored.CompareCold(root,admission,intent,queued,publication,XUiM_Recipes.Defs),"empty native registries refuse cold positive semantic evidence");A(!RebirthStationCompletionExpectationProjection.TryCapture(e,expected,intent,publication,proof,out refused),"empty native registries refuse new authentic semantic capture");A(!Paired(),"empty registries refuse positive saved witness despite storeddata support");
var malformedRaw=new XElement(frozenNode);malformedRaw.SetAttributeValue("job","foreign");A(!RebirthStationCompletionExpectationProjection.TryReadStored(malformedRaw,admission,intent,queued,publication,out rawStored)&&rawStored==null,"empty registry not malformed-data bypass");ItemClass.list=registry;XUiM_Recipes.Defs=catalogue;
A(RebirthStationCompletionExpectationProjection.TryReadStored(invalidQueueProjection,admission,intent,queued,invalidQueuePublication,out rawStored),"raw bounded bound hashes do not imply canonical native queue");A(!RebirthStationCompletionExpectationProjection.TryRead(invalidQueueProjection,admission,intent,queued,invalidQueuePublication,XUiM_Recipes.Defs,out refused),"malformed native wire still rejected by positive semantic gate");
Console.WriteLine("PASS "+checks+" production frozen projection/actual native regions (outer authority adapters explicit)");return 0;}}class RebirthWorldCharacterService{internal static int Dirty;internal static void MarkDirty(ProjectionOwner owner,string reason){Dirty++;}}
partial class RebirthWorldCharacterRepository{internal static Func<bool> SaveCallback;internal static bool SaveIfDirty(RebirthStablePlayerIdentity identity,string reason)=>SaveCallback();}
class CandidateObservation{
 RebirthStationCompletionCapture.SuccessfulOutput completed;RebirthStationCompletionOriginalScope original;RebirthStationTerminalIntent intent;RebirthStationCompletionExpectation expectation;object request=new();string saveRoot;
 internal CandidateObservation(RebirthStationCompletionCapture.SuccessfulOutput e,RebirthStationTerminalIntent i,RebirthStationCompletionExpectation x){completed=e;original=e.Original;intent=i;expectation=x;saveRoot=original.SaveRoot;}
 bool IsCurrent()=>original.IsCurrent()&&completed.MatchesReceipt()&&expectation.Current;
    internal bool TrySavePublication(out RebirthStationCompletionPublication publication)
    {
        publication=null;
        try
        {
            if(!IsCurrent())return false;
            var owner=original.Owner;var key=original.Admission.JobId;var state=owner.Progression;
            bool hasCompleted=state.StationCompletionPublications.TryGetValue(key,out var retained);
            bool hasProjection=state.StationCompletionExpectationProjections.TryGetValue(key,out var projection);
            if(hasCompleted!=hasProjection)return false;
            if(!hasCompleted)
            {
                if(state.StationCompletionPublications.Count>=64||state.StationCompletionExpectationProjections.Count>=64||
                    !RebirthStationSnapshotEvidence.TryGetPublished(request,out var proof)||
                    !RebirthStationCompletionPublication.TryCreate(saveRoot,original.Admission,intent,original.Queued,expectation,proof,out retained)||
                    !RebirthStationCompletionExpectationProjection.TryCapture(completed,expectation,intent,retained,proof,out projection)||!IsCurrent())return false;
                var nextCompleted=new System.Collections.Generic.Dictionary<string,RebirthStationCompletionPublication>(state.StationCompletionPublications,StringComparer.Ordinal);
                var nextProjections=new System.Collections.Generic.Dictionary<string,RebirthStationCompletionExpectationProjection>(state.StationCompletionExpectationProjections,StringComparer.Ordinal);
                nextCompleted.Add(key,retained);nextProjections.Add(key,projection);
                RebirthStationCompletionExpectationProjection.WriteAll(nextProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,nextCompleted,XUiM_Recipes.GetRecipes());
                if(!IsCurrent()||!ReferenceEquals(owner.Progression,state)||state.StationCompletionPublications.ContainsKey(key)||state.StationCompletionExpectationProjections.ContainsKey(key))return false;
                state.StationCompletionPublications.Add(key,retained);
                try{state.StationCompletionExpectationProjections.Add(key,projection);}catch{state.StationCompletionPublications.Remove(key);throw;}
                RebirthWorldCharacterService.MarkDirty(owner,"station-authentic-completion-pair-retained");
            }
            if(retained==null||projection==null||!retained.Revalidate(saveRoot,original.Admission,intent,original.Queued,expectation)||
                !projection.CompareCold(saveRoot,original.Admission,intent,original.Queued,retained,XUiM_Recipes.GetRecipes())||!IsCurrent()||
                !ReferenceEquals(owner.Progression,state)||!state.StationCompletionPublications.TryGetValue(key,out var preSaveCompleted)||!ReferenceEquals(preSaveCompleted,retained)||
                !state.StationCompletionExpectationProjections.TryGetValue(key,out var preSaveProjection)||!ReferenceEquals(preSaveProjection,projection))return false;
            RebirthWorldCharacterService.MarkDirty(owner,"station-authentic-completion-pair");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(original.Identity,"station-authentic-completion-pair")||!IsCurrent()||
                !ReferenceEquals(owner.Progression,state)||!state.StationCompletionPublications.TryGetValue(key,out var current)||!ReferenceEquals(current,retained)||
                !state.StationCompletionExpectationProjections.TryGetValue(key,out var currentProjection)||!ReferenceEquals(currentProjection,projection)||
                !RebirthWorldCharacterRepository.HasSavedStationCompletionExpectationProjection(original.Identity,original.Admission,intent,original.Queued,retained,projection)||
                !retained.Revalidate(saveRoot,original.Admission,intent,original.Queued,expectation)||
                !projection.CompareCold(saveRoot,original.Admission,intent,original.Queued,retained,XUiM_Recipes.GetRecipes())||!IsCurrent()||
                !ReferenceEquals(owner.Progression,state)||!state.StationCompletionPublications.TryGetValue(key,out var finalCompleted)||!ReferenceEquals(finalCompleted,retained)||
                !state.StationCompletionExpectationProjections.TryGetValue(key,out var finalProjection)||!ReferenceEquals(finalProjection,projection))return false;
            publication=retained.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(original.Owner,"station-authentic-completion-pair-uncertain");return false;}
    }
}static class CandidateHooks{internal static XElement Write(ProjectionState state){var node=new XElement("progression");        if(state.StationCompletionExpectationProjections.Count!=0)node.Add(RebirthStationCompletionExpectationProjection.WriteAllStored(state.StationCompletionExpectationProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications));return node;}internal static bool Read(XElement node,ProjectionState state,out string error){error=null;        if(!RebirthStationCompletionExpectationProjection.ReadAllStored(node,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,out var completionProjections)){error="Invalid station completion expectation projections";return false;}
        foreach(var pair in completionProjections)state.StationCompletionExpectationProjections.Add(pair.Key,pair.Value);return true;}}class RebirthStablePlayerIdentity{internal string StorageKey="owner";}
class Origin{internal string CreationId="character";}
class ProjectionState{internal Dictionary<string,RebirthStationGridAdmission> StationPreparations=new();internal Dictionary<string,RebirthStationTerminalIntent> StationTerminalIntents=new();internal Dictionary<string,RebirthStationPublicationRecord> StationPublications=new();internal Dictionary<string,RebirthStationCompletionPublication> StationCompletionPublications=new();internal Dictionary<string,RebirthStationCompletionExpectationProjection> StationCompletionExpectationProjections=new();}
class ProjectionOwner{internal ProjectionState Progression=new();internal Origin Origin=new();}
static class RebirthSurvivorRequestScope{internal static bool Matches(string a,string b)=>a==b;}
partial class RebirthWorldCharacterRepository{
 internal static bool serverAuthority=true,Migrated,ThrowRead;internal static string PathValue;internal static Action Callback;internal static ProjectionOwner Owner;static object Gate=new();
 static string GetPath(string owner)=>PathValue;static object GetWriteLock(string owner)=>Gate;
 static bool TryLoadValidatedRecord(string p,RebirthStablePlayerIdentity identity,out ProjectionOwner saved,out bool migrated,out string error,out bool reserved){saved=null;migrated=Migrated;error=null;reserved=false;if(ThrowRead)throw new IOException();if(!File.Exists(p))return false;var xml=XElement.Load(p);if((string)xml.Attribute("owner")!=identity.StorageKey)return false;var state=Owner.Progression;var result=new ProjectionOwner{Origin=new(){CreationId=(string)xml.Attribute("creation")},Progression=new(){StationPreparations=state.StationPreparations,StationTerminalIntents=state.StationTerminalIntents,StationPublications=state.StationPublications,StationCompletionPublications=state.StationCompletionPublications}};if(!RebirthStationCompletionExpectationProjection.ReadAllStored(xml,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,out var parsed))return false;result.Progression.StationCompletionExpectationProjections=parsed;saved=result;Callback?.Invoke();return true;}
}

partial class RebirthWorldCharacterRepository{
    internal static bool HasSavedStationCompletionExpectationProjection(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed,RebirthStationCompletionExpectationProjection projection)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||queued==null||completed==null||projection==null||
            !RebirthStationCompletionPublication.TryRead(completed.Write(),admission,intent,queued,out _)||
            !RebirthStationCompletionExpectationProjection.TryRead(projection.Write(),admission,intent,queued,completed,XUiM_Recipes.GetRecipes(),out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var a)||a==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var i)||i==null||
                    !saved.Progression.StationPublications.TryGetValue(admission.JobId,out var q)||q==null||
                    !saved.Progression.StationCompletionPublications.TryGetValue(admission.JobId,out var c)||c==null||
                    !saved.Progression.StationCompletionExpectationProjections.TryGetValue(admission.JobId,out var p)||p==null||
                    !serverAuthority||path!=GetPath(identity.StorageKey))return false;
                return XNode.DeepEquals(a.Write(),admission.Write())&&XNode.DeepEquals(i.Write(),intent.Write())&&
                    XNode.DeepEquals(q.Write(),queued.Write())&&XNode.DeepEquals(c.Write(),completed.Write())&&XNode.DeepEquals(p.Write(),projection.Write())&&
                    serverAuthority&&path==GetPath(identity.StorageKey);
            }
            catch{return false;}
        }
    }
}