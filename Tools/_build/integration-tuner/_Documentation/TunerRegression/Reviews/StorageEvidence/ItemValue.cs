using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

public class ItemValue : IEquatable<ItemValue>
{
	[StructLayout(LayoutKind.Auto)]
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

	[PublicizedFrom(EAccessModifier.Private)]
	public const int EmptySaveVersion = 0;

	[PublicizedFrom(EAccessModifier.Private)]
	public const int CurrentSaveVersion = 9;

	[PublicizedFrom(EAccessModifier.Private)]
	public const byte MaxModifications = byte.MaxValue;

	[PublicizedFrom(EAccessModifier.Private)]
	public static FastTags<TagGroup.Global> noPreinstallCosmeticItemTags = FastTags<TagGroup.Global>.Parse("weapon,tool,armor");

	[PublicizedFrom(EAccessModifier.Private)]
	public const byte cFlagsActivated = 1;

	[PublicizedFrom(EAccessModifier.Private)]
	public const byte cFlagsWasCombined = 2;

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemStack owner;

	[PublicizedFrom(EAccessModifier.Private)]
	public int typeField;

	[PublicizedFrom(EAccessModifier.Private)]
	public byte flags;

	[PublicizedFrom(EAccessModifier.Private)]
	public byte selectedAmmoTypeIndex;

	[PublicizedFrom(EAccessModifier.Private)]
	public float useTimes;

	[PublicizedFrom(EAccessModifier.Private)]
	public int meta;

	[PublicizedFrom(EAccessModifier.Private)]
	public ushort quality;

	[PublicizedFrom(EAccessModifier.Private)]
	public Dictionary<string, TypedMetadataValue> Metadata;

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemValue[] modifications;

	[PublicizedFrom(EAccessModifier.Private)]
	public ItemValue[] cosmeticMods;

	[PublicizedFrom(EAccessModifier.Private)]
	public ushort seed;

	[PublicizedFrom(EAccessModifier.Private)]
	public TextureFullArray textureFullArray;

	[PublicizedFrom(EAccessModifier.Private)]
	public const string cDurabilityMetaName = "DurabilityModifier";

	[PublicizedFrom(EAccessModifier.Private)]
	public static DateTime baseDate = new DateTime(2013, 10, 1);

	[PublicizedFrom(EAccessModifier.Private)]
	public const byte cStreamFlagItem = 1;

	[PublicizedFrom(EAccessModifier.Private)]
	public const byte cStreamFlagStats = 2;

	public static ItemValue None => new ItemValue(0);

	public int type
	{
		get
		{
			return typeField;
		}
		set
		{
			if (typeField != value)
			{
				typeField = value;
				NotifyChanged();
			}
		}
	}

	public byte Flags
	{
		get
		{
			return flags;
		}
		set
		{
			if (flags != value)
			{
				flags = value;
				NotifyChanged();
			}
		}
	}

	public byte SelectedAmmoTypeIndex
	{
		get
		{
			return selectedAmmoTypeIndex;
		}
		set
		{
			if (selectedAmmoTypeIndex != value)
			{
				selectedAmmoTypeIndex = value;
				NotifyChanged();
			}
		}
	}

	public float UseTimes
	{
		get
		{
			return useTimes;
		}
		set
		{
			if (useTimes != value)
			{
				useTimes = value;
				NotifyChanged();
			}
		}
	}

	public int Meta
	{
		get
		{
			return meta;
		}
		set
		{
			if (meta != value)
			{
				meta = value;
				NotifyChanged();
			}
		}
	}

	public ushort Quality
	{
		get
		{
			return quality;
		}
		set
		{
			if (quality != value)
			{
				quality = value;
				NotifyChanged();
			}
		}
	}

	[field: PublicizedFrom(EAccessModifier.Private)]
	public Stat[] Stats
	{
		get; [PublicizedFrom(EAccessModifier.Private)]
		set;
	}

	public int ModificationCount
	{
		get
		{
			if (modifications == null)
			{
				return 0;
			}
			return modifications.Length;
		}
	}

	public int CosmeticModCount
	{
		get
		{
			if (cosmeticMods == null)
			{
				return 0;
			}
			return cosmeticMods.Length;
		}
	}

	public ushort Seed
	{
		get
		{
			return seed;
		}
		set
		{
			if (seed != value)
			{
				seed = value;
				NotifyChanged();
			}
		}
	}

	public TextureFullArray TextureFullArray
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

	public bool Activated
	{
		get
		{
			return (Flags & 1) > 0;
		}
		set
		{
			Flags = (byte)(value ? (Flags | 1u) : (Flags & 0xFFFFFFFEu));
		}
	}

	public bool WasCombined => (Flags & 2) > 0;

	public int MaxUseTimes => ModMaxUseTimes(MaxUseTimesBase, this);

	public int MaxUseTimesUI => MaxUseTimesBase;

	public int MaxUseTimesBase
	{
		[PublicizedFrom(EAccessModifier.Private)]
		get
		{
			return (int)EffectManager.GetValue(PassiveEffects.DegradationMax, this, 0f, null, null, ItemClass?.ItemTags ?? FastTags<TagGroup.Global>.none);
		}
	}

	public float MaxDurabilityModifier
	{
		get
		{
			if (!TryGetMetadata("DurabilityModifier", out float value))
			{
				return 1f;
			}
			return value;
		}
		set
		{
			if (value == 1f)
			{
				RemoveMetaData("DurabilityModifier");
			}
			else
			{
				SetMetadata("DurabilityModifier", value);
			}
		}
	}

	public float PercentUsesLeft
	{
		get
		{
			int maxUseTimes = MaxUseTimes;
			if (maxUseTimes > 0)
			{
				return 1f - Utils.FastClamp01(UseTimes / (float)maxUseTimes);
			}
			return 1f;
		}
	}

	public bool HasQuality
	{
		get
		{
			ItemClass itemClass = ItemClass;
			if (itemClass != null)
			{
				if (!itemClass.HasQuality)
				{
					return itemClass is ItemClassModifier;
				}
				return true;
			}
			return false;
		}
	}

	public bool HasModSlots => ModificationCount > 0;

	public bool IsMod
	{
		get
		{
			ItemClass itemClass = ItemClass;
			if (itemClass != null)
			{
				return itemClass is ItemClassModifier;
			}
			return false;
		}
	}

	public bool IsShapeHelperBlock => (ItemClass as ItemClassBlock)?.GetBlock().SelectAlternates ?? false;

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

	public ItemClass ItemClassOrMissing
	{
		get
		{
			ItemClass itemClass = ItemClass;
			if (itemClass != null)
			{
				return itemClass;
			}
			return ItemClass.MissingItem;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ItemValue GetModification(int _index)
	{
		return modifications[_index];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ItemValue GetCosmeticMod(int _index)
	{
		return cosmeticMods[_index];
	}

	public void SetModification(int _index, ItemValue _mod)
	{
		modifications[_index] = _mod;
		NotifyChanged();
	}

	public void SetCosmeticMod(int _index, ItemValue _mod)
	{
		cosmeticMods[_index] = _mod;
		NotifyChanged();
	}

	public void SetTextureFull(int _index, long _value)
	{
		if (textureFullArray[_index] != _value)
		{
			textureFullArray[_index] = _value;
			NotifyChanged();
		}
	}

	public void ResizeCosmeticMods(int _length)
	{
		if (_length == 0)
		{
			cosmeticMods = null;
			NotifyChanged();
		}
		else
		{
			Array.Resize(ref cosmeticMods, _length);
			NotifyChanged();
		}
	}

	public static int ModMaxUseTimes(int _value, ItemValue _iv)
	{
		if (_value <= 0)
		{
			return _value;
		}
		if (!_iv.TryGetMetadata("DurabilityModifier", out float value))
		{
			return _value;
		}
		_value = Mathf.RoundToInt((float)_value * value);
		_value = Mathf.Max(_value, 1);
		return _value;
	}

	public bool TryGetMetadata(string key, out int value)
	{
		if (!TryGetMetadata(key, out var value2, TypedMetadataValue.TypeTag.Integer))
		{
			value = 0;
			return false;
		}
		value = (int)value2;
		return true;
	}

	public bool TryGetMetadata(string key, out float value)
	{
		if (!TryGetMetadata(key, out var value2, TypedMetadataValue.TypeTag.Float))
		{
			value = 0f;
			return false;
		}
		value = (float)value2;
		return true;
	}

	public bool TryGetMetadata(string key, out string value)
	{
		if (!TryGetMetadata(key, out var value2, TypedMetadataValue.TypeTag.String))
		{
			value = null;
			return false;
		}
		value = (string)value2;
		return true;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public bool TryGetMetadata(string key, out object value, TypedMetadataValue.TypeTag typeTag = TypedMetadataValue.TypeTag.None)
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
	}

	public bool HasMetadata(string key, TypedMetadataValue.TypeTag typeTag = TypedMetadataValue.TypeTag.None)
	{
		if (Metadata == null)
		{
			return false;
		}
		if (typeTag == TypedMetadataValue.TypeTag.None)
		{
			return Metadata.ContainsKey(key);
		}
		if (!Metadata.TryGetValue(key, out var value))
		{
			return false;
		}
		return value.GetTypeTag() == typeTag;
	}

	public object GetMetadata(string key)
	{
		if (Metadata == null)
		{
			return false;
		}
		if (Metadata.TryGetValue(key, out var value))
		{
			return value.GetValue();
		}
		return null;
	}

	public void SetMetadata(string key, int value)
	{
		SetMetadata(key, value, TypedMetadataValue.TypeTag.Integer);
	}

	public void SetMetadata(string key, float value)
	{
		SetMetadata(key, value, TypedMetadataValue.TypeTag.Float);
	}

	public void SetMetadata(string key, string value)
	{
		SetMetadata(key, value, TypedMetadataValue.TypeTag.String);
	}

	public void SetMetadata(string key, object value, string typeTag)
	{
		SetMetadata(key, value, TypedMetadataValue.StringToTag(typeTag));
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void SetMetadata(string key, object value, TypedMetadataValue.TypeTag typeTag)
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
	}

	[PublicizedFrom(EAccessModifier.Private)]
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

	public bool RemoveMetaData(string name)
	{
		bool result = false;
		if (Metadata != null)
		{
			result = Metadata.Remove(name);
		}
		return result;
	}

	public ItemValue()
	{
	}

	public ItemValue(int _type, bool _bCreateDefaultParts = false)
		: this(_type, 1, 6, _bCreateDefaultParts)
	{
	}

	public ItemValue(int _type, int minQuality, int maxQuality, bool _bCreateDefaultModItems = false, string[] modsToInstall = null, float modInstallDescendingChance = 1f)
	{
		type = _type;
		if (type == 0)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		Seed = (ushort)((utcNow - baseDate).Seconds + utcNow.Millisecond + type);
		if (!ThreadManager.IsMainThread())
		{
			return;
		}
		ItemClass itemClass = ItemClass;
		if (itemClass == null)
		{
			return;
		}
		GameRandom gameRandom = null;
		if (itemClass.HasQuality)
		{
			if (minQuality == maxQuality)
			{
				Quality = (ushort)minQuality;
			}
			else
			{
				gameRandom = GameRandomManager.Instance.CreateGameRandom(Seed);
				Quality = (ushort)gameRandom.RandomRange(minQuality, maxQuality + 1);
			}
		}
		if (itemClass is ItemClassModifier)
		{
			GameRandomManager.Instance.FreeGameRandom(gameRandom);
			return;
		}
		if (itemClass.MaxCount > 1)
		{
			GameRandomManager.Instance.FreeGameRandom(gameRandom);
			return;
		}
		int num = CalcModSlotCount();
		modifications = ((num > 0) ? new ItemValue[num] : null);
		cosmeticMods = (itemClass.HasAnyTags(ItemClassModifier.CosmeticItemTags) ? new ItemValue[1] : null);
		if (_bCreateDefaultModItems)
		{
			if (gameRandom == null)
			{
				gameRandom = GameRandomManager.Instance.CreateGameRandom(Seed);
			}
			createDefaultModItems(itemClass, gameRandom, modsToInstall, modInstallDescendingChance);
		}
		GameRandomManager.Instance.FreeGameRandom(gameRandom);
	}

	public void BindItemStack(ItemStack _owner)
	{
		owner = _owner;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void NotifyChanged()
	{
		owner?.OnValueChanged();
	}

	public void Clear()
	{
		ResetFields();
		NotifyChanged();
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void ResetFields()
	{
		typeField = 0;
		flags = 0;
		useTimes = 0f;
		quality = 0;
		meta = 0;
		seed = 0;
		selectedAmmoTypeIndex = 0;
		textureFullArray = default(TextureFullArray);
		Stats = null;
		modifications = null;
		cosmeticMods = null;
		Metadata = null;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public int CalcModSlotCount()
	{
		int num = Utils.FastMax(0, Quality - 1);
		return Utils.FastMin(255, (int)EffectManager.GetValue(PassiveEffects.ModSlots, this, num, null, null, default(FastTags<TagGroup.Global>), calcEquipment: true, calcHoldingItem: true, calcProgression: true, calcBuffs: true, calcChallenges: true, 1, useMods: false));
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void createDefaultModItems(ItemClass ic, GameRandom random, string[] modsToInstall, float modInstallDescendingChance)
	{
		FastTags<TagGroup.Global> none = FastTags<TagGroup.Global>.none;
		bool flag = false;
		bool flag2 = false;
		if (modsToInstall != null && modsToInstall.Length != 0)
		{
			float num = modInstallDescendingChance;
			if (!ic.ItemTags.IsEmpty)
			{
				int num2 = 0;
				for (int i = 0; i < modsToInstall.Length; i++)
				{
					ItemClassModifier itemClassModifier = ItemClass.GetItemClass(modsToInstall[i], _caseInsensitive: true) as ItemClassModifier;
					if (itemClassModifier == null)
					{
						itemClassModifier = ItemClassModifier.GetDesiredItemModWithAnyTags(ic.ItemTags, none, FastTags<TagGroup.Global>.Parse(modsToInstall[i]), random);
					}
					if (itemClassModifier == null)
					{
						continue;
					}
					if (itemClassModifier.HasAnyTags(ItemClassModifier.CosmeticModTypes))
					{
						flag = true;
						if (!flag2 && !(random.RandomFloat > modInstallDescendingChance))
						{
							cosmeticMods[0] = new ItemValue(itemClassModifier.Id);
							none |= itemClassModifier.ItemTags;
							flag2 = true;
							Log.Warning("ItemValue createDefaultModItems cosmetic {0}", GetCosmeticMod(0));
						}
					}
					else if (num2 < ModificationCount && !(random.RandomFloat > num))
					{
						modifications[num2] = new ItemValue(itemClassModifier.Id);
						none |= itemClassModifier.ItemTags;
						num2++;
						num *= 0.5f;
					}
				}
				for (int j = num2; j < ModificationCount; j++)
				{
					modifications[j] = None;
				}
			}
		}
		if (flag || ic.HasAnyTags(noPreinstallCosmeticItemTags))
		{
			return;
		}
		for (int k = 0; k < CosmeticModCount; k++)
		{
			ItemClassModifier cosmeticItemMod = ItemClassModifier.GetCosmeticItemMod(ic.ItemTags, none, random);
			if (cosmeticItemMod != null)
			{
				cosmeticMods[k] = new ItemValue(cosmeticItemMod.Id);
				none |= cosmeticItemMod.ItemTags;
			}
			else
			{
				cosmeticMods[k] = None;
			}
		}
	}

	public void ModifyValue(EntityAlive _entity, ItemValue _originalItemValue, PassiveEffects _passiveEffect, ref float _originalValue, ref float _perc_value, FastTags<TagGroup.Global> _tags, bool _useMods = true, bool _useDurability = false)
	{
		if (_originalItemValue != null && _originalItemValue.Equals(this))
		{
			return;
		}
		int num = MinEventParams.CachedEventParam.Seed;
		if (_entity != null)
		{
			num = _entity.MinEventContext.Seed;
		}
		ItemClass itemClass = ItemClass;
		if (itemClass != null)
		{
			if (itemClass.Actions != null && itemClass.Actions.Length != 0 && itemClass.Actions[0] is ItemActionRanged)
			{
				string[] magazineItemNames = (itemClass.Actions[0] as ItemActionRanged).MagazineItemNames;
				if (magazineItemNames != null)
				{
					ItemClass itemClass2 = ItemClass.GetItemClass(magazineItemNames[SelectedAmmoTypeIndex]);
					if (itemClass2 != null && itemClass2.Effects != null)
					{
						itemClass2.Effects.ModifyValue(_entity, _passiveEffect, ref _originalValue, ref _perc_value, 0f, _tags);
					}
				}
			}
			if (itemClass.Effects != null)
			{
				ItemValue itemValue = MinEventParams.CachedEventParam.ItemValue;
				ItemValue itemValue2 = ((_entity != null) ? _entity.MinEventContext.ItemValue : null);
				MinEventParams.CachedEventParam.Seed = (int)Seed + (int)((Seed != 0) ? _passiveEffect : PassiveEffects.None);
				MinEventParams.CachedEventParam.ItemValue = this;
				if (_entity != null)
				{
					_entity.MinEventContext.Seed = MinEventParams.CachedEventParam.Seed;
					_entity.MinEventContext.ItemValue = this;
				}
				float num2 = _originalValue;
				itemClass.Effects.ModifyValue(_entity, _passiveEffect, ref _originalValue, ref _perc_value, (int)Quality, _tags);
				if (_useDurability)
				{
					switch (_passiveEffect)
					{
					case PassiveEffects.PhysicalDamageResist:
						if (PercentUsesLeft < 0.5f)
						{
							float num4 = _originalValue - num2;
							_originalValue = num2 + num4 * PercentUsesLeft * 2f;
						}
						break;
					case PassiveEffects.ElementalDamageResist:
						if (PercentUsesLeft < 0.5f)
						{
							float num5 = _originalValue - num2;
							_originalValue = num2 + num5 * PercentUsesLeft * 2f;
						}
						break;
					case PassiveEffects.BuffResistance:
						if (PercentUsesLeft < 0.5f)
						{
							float num3 = _originalValue - num2;
							_originalValue = num2 + num3 * PercentUsesLeft * 2f;
						}
						break;
					}
				}
				MinEventParams.CachedEventParam.ItemValue = itemValue;
				if (_entity != null)
				{
					_entity.MinEventContext.ItemValue = itemValue2;
				}
			}
		}
		if (Stats != null)
		{
			StatModifyValue(_passiveEffect, ref _originalValue);
		}
		if (_useMods)
		{
			for (int i = 0; i < CosmeticModCount; i++)
			{
				ItemValue cosmeticMod = GetCosmeticMod(i);
				if (cosmeticMod != null && cosmeticMod.ItemClass is ItemClassModifier)
				{
					cosmeticMod.ModifyValue(_entity, _originalItemValue, _passiveEffect, ref _originalValue, ref _perc_value, _tags);
				}
			}
			for (int j = 0; j < ModificationCount; j++)
			{
				ItemValue modification = GetModification(j);
				if (modification != null && modification.ItemClass is ItemClassModifier)
				{
					modification.ModifyValue(_entity, _originalItemValue, _passiveEffect, ref _originalValue, ref _perc_value, _tags);
				}
			}
		}
		MinEventParams.CachedEventParam.Seed = num;
		if (_entity != null)
		{
			_entity.MinEventContext.Seed = num;
		}
	}

	public void GetModifiedValueData(List<EffectManager.ModifierValuesAndSources> _modValueSources, EffectManager.ModifierValuesAndSources.ValueSourceType _sourceType, EntityAlive _entity, ItemValue _originalItemValue, PassiveEffects _passiveEffect, ref float _originalValue, ref float _perc_value, FastTags<TagGroup.Global> _tags)
	{
		if (_originalItemValue != null && _originalItemValue.Equals(this))
		{
			return;
		}
		ItemClass itemClass = ItemClass;
		if (itemClass != null)
		{
			if (itemClass.Actions != null && itemClass.Actions.Length != 0 && itemClass.Actions[0] is ItemActionRanged)
			{
				string[] magazineItemNames = (itemClass.Actions[0] as ItemActionRanged).MagazineItemNames;
				if (magazineItemNames != null)
				{
					ItemClass.GetItem(magazineItemNames[SelectedAmmoTypeIndex]).GetModifiedValueData(_modValueSources, EffectManager.ModifierValuesAndSources.ValueSourceType.Ammo, _entity, _originalItemValue, _passiveEffect, ref _originalValue, ref _perc_value, _tags);
				}
			}
			if (itemClass.Effects != null)
			{
				itemClass.Effects.GetModifiedValueData(_modValueSources, _sourceType, _entity, _passiveEffect, ref _originalValue, ref _perc_value, (int)Quality, _tags);
			}
		}
		for (int i = 0; i < CosmeticModCount; i++)
		{
			ItemValue cosmeticMod = GetCosmeticMod(i);
			if (cosmeticMod != null && cosmeticMod.ItemClass is ItemClassModifier)
			{
				cosmeticMod.GetModifiedValueData(_modValueSources, EffectManager.ModifierValuesAndSources.ValueSourceType.CosmeticMod, _entity, _originalItemValue, _passiveEffect, ref _originalValue, ref _perc_value, _tags);
			}
		}
		for (int j = 0; j < ModificationCount; j++)
		{
			ItemValue modification = GetModification(j);
			if (modification != null && modification.ItemClass is ItemClassModifier)
			{
				modification.GetModifiedValueData(_modValueSources, EffectManager.ModifierValuesAndSources.ValueSourceType.Mod, _entity, _originalItemValue, _passiveEffect, ref _originalValue, ref _perc_value, _tags);
			}
		}
	}

	public void FireEvent(MinEventTypes _eventType, MinEventParams _eventParms)
	{
		ItemClass itemClass = ItemClass;
		if (itemClass != null)
		{
			if (itemClass is ItemClassModifier && itemClass.Effects != null)
			{
				itemClass.Effects.FireEvent(_eventType, _eventParms);
				return;
			}
			if (itemClass.Actions != null && itemClass.Actions.Length != 0 && itemClass.Actions[0] is ItemActionRanged)
			{
				string[] magazineItemNames = (itemClass.Actions[0] as ItemActionRanged).MagazineItemNames;
				if (magazineItemNames != null)
				{
					ItemClass.GetItem(magazineItemNames[SelectedAmmoTypeIndex]).FireEvent(_eventType, _eventParms);
				}
			}
			itemClass.FireEvent(_eventType, _eventParms);
		}
		if (HasQuality)
		{
			for (int i = 0; i < ModificationCount; i++)
			{
				GetModification(i)?.FireEvent(_eventType, _eventParms);
			}
			for (int j = 0; j < CosmeticModCount; j++)
			{
				GetCosmeticMod(j)?.FireEvent(_eventType, _eventParms);
			}
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

	public bool IsEmpty()
	{
		return type == 0;
	}

	public BlockValue ToBlockValue(bool _allowAlternates = false)
	{
		if (type < Block.ItemsStartHere)
		{
			BlockValue result = default(BlockValue);
			result.type = type;
			if (_allowAlternates && result.Block.SelectAlternates)
			{
				return result.Block.GetAltBlockValue(Meta);
			}
			return result;
		}
		return BlockValue.Air;
	}

	public void MergeBest(ItemValue _iv)
	{
		Flags |= 2;
		int maxUseTimes = MaxUseTimes;
		int maxUseTimes2 = _iv.MaxUseTimes;
		float num = (float)maxUseTimes - UseTimes;
		float num2 = (float)maxUseTimes2 - _iv.UseTimes;
		if (ItemAction.RepairType == ItemAction.RepairTypes.Both || ItemAction.RepairType == ItemAction.RepairTypes.CombineOnly)
		{
			int num3 = Utils.FastMax(maxUseTimes, maxUseTimes2);
			float num4 = num + num2;
			UseTimes = Utils.FastMax(0f, (float)num3 - num4);
		}
		else
		{
			bool flag = false;
			if (_iv.Quality == Quality)
			{
				flag = num2 > num;
				if (flag)
				{
					MaxDurabilityModifier = _iv.MaxDurabilityModifier;
				}
			}
			else if (_iv.Quality > Quality)
			{
				flag = true;
			}
			if (flag)
			{
				UseTimes = Utils.FastMax(0f, (float)maxUseTimes2 - num2);
			}
		}
		if (_iv.Quality > Quality)
		{
			Quality = _iv.Quality;
			MaxDurabilityModifier = _iv.MaxDurabilityModifier;
		}
		MergeBestStats(_iv);
		if (_iv.HasMods())
		{
			modifications = _iv.modifications.CloneItemValueArray();
		}
		if (_iv.HasCosmetics())
		{
			cosmeticMods = _iv.cosmeticMods.CloneItemValueArray();
		}
		int num5 = CalcModSlotCount();
		if (num5 == 0)
		{
			modifications = null;
		}
		else
		{
			Array.Resize(ref modifications, num5);
		}
	}

	public void InitStats(int _count)
	{
		Stats = new Stat[_count];
	}

	public void ClearStats()
	{
		Stats = null;
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

	public bool HasStats()
	{
		return Stats != null;
	}

	public bool HasAnyBoostedStats()
	{
		if (Stats != null)
		{
			for (int i = 0; i < Stats.Length; i++)
			{
				if (Stats[i].isBoosted)
				{
					return true;
				}
			}
		}
		return false;
	}

	public float GetStatPercent(PassiveEffects _type, bool _onlyBoosted)
	{
		float _originalValue = 1f;
		if (Stats != null)
		{
			StatModifyValue(_type, ref _originalValue, _onlyBoosted);
		}
		return _originalValue;
	}

	public void SetStat(int _index, PassiveEffects _type, int _base, int _added)
	{
		Stats[_index] = new Stat(_type, _base, _added);
	}

	public void MergeBestStats(ItemValue _iv)
	{
		if (_iv.Stats == null)
		{
			return;
		}
		if (Stats == null)
		{
			Stats = new Stat[_iv.Stats.Length];
			Array.Copy(_iv.Stats, Stats, _iv.Stats.Length);
			return;
		}
		List<Stat> list = new List<Stat>(Stats);
		for (int i = 0; i < _iv.Stats.Length; i++)
		{
			Stat item = _iv.Stats[i];
			bool flag = false;
			for (int j = 0; j < list.Count; j++)
			{
				Stat value = list[j];
				if (item.type == value.type)
				{
					bool flag2 = IsStatLowerBetter(value.type);
					if (item.value != value.value && item.value > value.value != flag2)
					{
						value.value = item.value;
						value.isBoosted = item.isBoosted;
						list[j] = value;
					}
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(item);
			}
		}
		Stats = list.ToArray();
	}

	public static bool IsStatLowerBetter(PassiveEffects _type)
	{
		if (_type != PassiveEffects.StaminaLoss)
		{
			return _type == PassiveEffects.TargetArmor;
		}
		return true;
	}

	public void StatModifyValue(PassiveEffects _passiveEffect, ref float _originalValue, bool onlyBoosted = false)
	{
		for (int i = 0; i < Stats.Length; i++)
		{
			Stat stat = Stats[i];
			if (stat.type == _passiveEffect)
			{
				int num = ((!onlyBoosted || stat.isBoosted) ? stat.value : 0);
				float num2 = 1f + (float)num * 0.005f;
				_originalValue *= num2;
				break;
			}
		}
	}

	public void ReadOld(PooledBinaryReader _br)
	{
	}

	public static ItemValue ReadOrNull(PooledBinaryReader _br)
	{
		byte b = _br.ReadByte();
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
		ResetFields();
		byte b = _br.ReadByte();
		if (b != 0)
		{
			ReadData(_br, b);
		}
		NotifyChanged();
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public void ReadData(PooledBinaryReader _br, int version)
	{
		int num = 0;
		if (version >= 8)
		{
			num = _br.ReadByte();
		}
		typeField = _br.ReadUInt16();
		if ((num & 1) > 0)
		{
			typeField += Block.ItemsStartHere;
		}
		if (version < 8 && typeField >= 32768)
		{
			typeField += 32768;
		}
		ItemClass itemClass = ItemClass;
		if (version > 5)
		{
			useTimes = _br.ReadSingle();
		}
		else
		{
			useTimes = (int)_br.ReadUInt16();
		}
		quality = _br.ReadUInt16();
		if (itemClass != null && itemClass.QualityMin > 0)
		{
			quality = (byte)Utils.FastMax(quality, itemClass.QualityMin);
		}
		meta = _br.ReadUInt16();
		if (meta >= 65535)
		{
			meta = -1;
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
			byte b = _br.ReadByte();
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
		flags = _br.ReadByte();
		if (version > 2)
		{
			selectedAmmoTypeIndex = _br.ReadByte();
		}
		if (version > 3)
		{
			seed = _br.ReadUInt16();
			if (typeField == 0)
			{
				seed = 0;
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

	public void AdjustForSandboxOptions()
	{
		if (!EntityPlayerLocal.PermaDegrationOn)
		{
			RemoveMetaData("DurabilityModifier");
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

	public bool Equals(ItemValue _other)
	{
		if (_other == null)
		{
			return false;
		}
		if (_other.type == type && _other.UseTimes == UseTimes && _other.Meta == Meta && _other.Seed == Seed && _other.Quality == Quality && _other.SelectedAmmoTypeIndex == SelectedAmmoTypeIndex && _other.Activated == Activated && Equals(_other.Metadata, Metadata) && Equals(_other.Stats, Stats) && Equals(_other.cosmeticMods, cosmeticMods))
		{
			return Equals(_other.modifications, modifications);
		}
		return false;
	}

	public override bool Equals(object _other)
	{
		if (!(_other is ItemValue))
		{
			return false;
		}
		return Equals((ItemValue)_other);
	}

	public bool EqualsExceptUseTimesAndMeta(ItemValue _other)
	{
		if (_other == null)
		{
			return false;
		}
		if (_other.type == type && _other.Seed == Seed && _other.Quality == Quality && _other.SelectedAmmoTypeIndex == SelectedAmmoTypeIndex && _other.Activated == Activated && Equals(_other.Metadata, Metadata) && Equals(_other.Stats, Stats) && Equals(_other.cosmeticMods, cosmeticMods))
		{
			return Equals(_other.modifications, modifications);
		}
		return false;
	}

	public bool EqualsForMerging(ItemValue _other)
	{
		if (_other == null)
		{
			return false;
		}
		bool flag = ItemAction.RepairType == ItemAction.RepairTypes.Both || ItemAction.RepairType == ItemAction.RepairTypes.CombineOnly;
		if (_other.type == type && (!flag || UseTimes + _other.UseTimes == 0f || ((float)MaxUseTimes == UseTimes && (float)_other.MaxUseTimes == _other.UseTimes)))
		{
			return Equals(_other.Stats, Stats);
		}
		return false;
	}

	public static bool Equals(ItemValue[] _a, ItemValue[] _b)
	{
		if (_a == null && _b == null)
		{
			return true;
		}
		if (_a == null || _b == null)
		{
			return false;
		}
		if (_a.Length != _b.Length)
		{
			return false;
		}
		if (_a.Length == 0)
		{
			return true;
		}
		for (int i = 0; i < _a.Length; i++)
		{
			if (_a[i] != null || _b[i] != null)
			{
				if (_a[i] == null || _b[i] == null)
				{
					return false;
				}
				if (!_a[i].Equals(_b[i]))
				{
					return false;
				}
			}
		}
		return true;
	}

	public static bool Equals(Stat[] _a, Stat[] _b)
	{
		if (_a == null && _b == null)
		{
			return true;
		}
		if (_a == null || _b == null)
		{
			return false;
		}
		if (_a.Length != _b.Length)
		{
			return false;
		}
		if (_a.Length == 0)
		{
			return true;
		}
		return _a.SequenceEqual(_b);
	}

	public static bool Equals(object[] _a, object[] _b)
	{
		if (!(_a is ItemValue[]) || !(_b is ItemValue[]))
		{
			return false;
		}
		return Equals((ItemValue[])_a, (ItemValue[])_b);
	}

	public static bool Equals(Dictionary<string, TypedMetadataValue> _a, Dictionary<string, TypedMetadataValue> _b)
	{
		if (_a == null && _b == null)
		{
			return true;
		}
		if (_a == null || _b == null)
		{
			return false;
		}
		if (_a.Count != _b.Count)
		{
			return false;
		}
		if (_a.Count == 0)
		{
			return true;
		}
		foreach (string key in _a.Keys)
		{
			if (!_b.ContainsKey(key))
			{
				return false;
			}
			if (!_a[key].Equals(_b[key]))
			{
				return false;
			}
		}
		return true;
	}

	public override int GetHashCode()
	{
		return type;
	}

	public int GetItemId()
	{
		return type - Block.ItemsStartHere;
	}

	public int GetItemOrBlockId()
	{
		if (type < Block.ItemsStartHere)
		{
			return type;
		}
		return type - Block.ItemsStartHere;
	}

	public override string ToString()
	{
		return ((type >= Block.ItemsStartHere) ? ("item=" + (type - Block.ItemsStartHere)) : ("block=" + type)) + " m=" + Meta + " ut=" + UseTimes;
	}

	public string GetPropertyOverride(string _propertyName, string _originalValue)
	{
		if (ModificationCount == 0 && CosmeticModCount == 0)
		{
			return _originalValue;
		}
		string _value = "";
		string itemName = ItemClass.GetItemName();
		for (int i = 0; i < ModificationCount; i++)
		{
			ItemValue modification = GetModification(i);
			if (modification != null)
			{
				ItemClassModifier itemClassModifier = modification.ItemClass as ItemClassModifier;
				if (itemClassModifier != null && itemClassModifier.GetPropertyOverride(_propertyName, itemName, ref _value))
				{
					return _value;
				}
			}
		}
		_value = "";
		for (int j = 0; j < CosmeticModCount; j++)
		{
			ItemValue cosmeticMod = GetCosmeticMod(j);
			if (cosmeticMod != null)
			{
				ItemClassModifier itemClassModifier2 = cosmeticMod.ItemClass as ItemClassModifier;
				if (itemClassModifier2 != null && itemClassModifier2.GetPropertyOverride(_propertyName, itemName, ref _value))
				{
					return _value;
				}
			}
		}
		return _originalValue;
	}

	public bool HasCosmetics()
	{
		for (int i = 0; i < CosmeticModCount; i++)
		{
			ItemValue cosmeticMod = GetCosmeticMod(i);
			if (cosmeticMod != null && !cosmeticMod.IsEmpty())
			{
				return true;
			}
		}
		return false;
	}

	public bool HasMods()
	{
		for (int i = 0; i < ModificationCount; i++)
		{
			ItemValue modification = GetModification(i);
			if (modification != null && !modification.IsEmpty())
			{
				return true;
			}
		}
		return false;
	}
}
