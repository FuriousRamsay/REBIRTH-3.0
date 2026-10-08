using System;using System.Text;using System.Linq;using System.IO;using System.Collections.Generic;using System.Runtime.CompilerServices;
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
					bytes2 = encoder.GetBytes((char*)(void*)((UIntPtr)ptr + num2 * 2), num4, bytes, 128, num4 == num3);
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
public class ItemClass{public static ItemClass[] list=new ItemClass[100000];public static Mapping nameIdMapping=new Mapping();public int MaxCount=100;public string GetItemName(){return Name;}public int QualityMin;public string Name="fixture";public bool IsBlock(){return false;}}public class ItemClassModifier:ItemClass{}public class ItemClassQuest:ItemClass{public static ItemClass GetItemQuestById(int i){return list[1];}}
public static class Utils{public static int FastMax(int a,int b){return Math.Max(a,b);}}public static class Log{public static void Error(string s){}public static void Warning(string s){}}public static class StackTraceUtility{public static string ExtractStackTrace(){return "boundary";}}
public class ItemStack{public ItemValue itemValue=new ItemValue();public int countField;public int count{get{return countField;}set{countField=value;}}public static int Reads;public void OnValueChanged(){}public bool IsEmpty(){return count==0||itemValue==null||itemValue.IsEmpty();}public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new ItemStack();return a;}public ItemStack Read(PooledBinaryReader _br)
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
		return this;
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
	}public void SetMetadata(string k,string v){SetMetadata(k,new TypedMetadataValue(v,TypedMetadataValue.TypeTag.String));}public static int NativeReads;
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
        string encoded;
        if(!backpack.TryGetMetadata(MetadataKey,out encoded))
        { contents=ItemStack.CreateArray(capacity);return true; }
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
        string encoded;
        if(!backpack.TryGetMetadata(MetadataKey,out encoded))
        { contents=ItemStack.CreateArray(capacity);return true; }
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
Console.WriteLine("PASS "+n+" current Contents + canonical stack-array integration");return 0;}}public static class IndependentStackAudit {
static int n;static void A(bool b,string name){if(!b)throw new Exception(name);n++;Console.WriteLine(name);}
public static void Main(){
ItemClass.list[1]=new ItemClass{Name="book"};ItemClass.list[3]=new ItemClass{Name="backpack"};
foreach(bool library in new[]{true,false})foreach(bool integer in new[]{true,false}){
string key=library?RebirthBackpackLibraryContents.MetadataKey:RebirthBackpackSellStashContents.MetadataKey;
var pack=new ItemValue(3);pack.SetMetadata(key,new TypedMetadataValue(integer?(object)7:(object)7f,integer?TypedMetadataValue.TypeTag.Integer:TypedMetadataValue.TypeTag.Float));
ItemStack[] read;bool ok=library?RebirthBackpackLibraryContents.TryRead(pack,out read):RebirthBackpackSellStashContents.TryRead(pack,out read);
A(ok&&read.All(s=>s.count==0),"REPRO existing wrong-type "+key+" reported empty");
var rows=ItemStack.CreateArray(3);rows[0].count=1;rows[0].itemValue=new ItemValue(1);ItemValue candidate;
bool write=library?RebirthBackpackLibraryContents.TryWrite(pack,rows,out candidate):RebirthBackpackSellStashContents.TryWrite(pack,rows,out candidate);
A(write&&candidate!=null,"REPRO TryWrite success despite wrong-type reserved key");
ok=library?RebirthBackpackLibraryContents.TryRead(candidate,out read):RebirthBackpackSellStashContents.TryRead(candidate,out read);
A(ok&&read.All(s=>s.count==0),"REPRO candidate lacks requested positive-count contents");
A(pack.Metadata[key].GetTypeTag()==(integer?TypedMetadataValue.TypeTag.Integer:TypedMetadataValue.TypeTag.Float)&&rows[0].count==1,"source unchanged, ambiguity not actual transfer loss proof");
}
var zero=ItemStack.CreateArray(3);zero[0].itemValue=null;string encoded;
A(RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(zero,49152,65536,out encoded),"zero-count null sentinel safely native encoded");
ItemStack[] decoded;A(RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(encoded,3,49152,65536,out decoded)&&decoded.All(s=>s.IsEmpty()),"zero-count native rows canonical empty; custody count stays zero");
Console.WriteLine(n+" independent observations/checks; reserved-key bug reproduced, not fixed; engine/pool/policy adapters same as461 fixture");
}}