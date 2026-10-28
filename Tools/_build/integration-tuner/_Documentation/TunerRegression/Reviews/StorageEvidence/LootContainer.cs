using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class LootContainer
{
	public enum DestroyOnClose
	{
		False,
		True,
		Empty
	}

	public class LootItem
	{
		public ItemValue itemValue;
	}

	public class LootGroup
	{
		public enum AbundanceLootModTypes
		{
			None,
			Food,
			Drinks,
			Ammo,
			Meds,
			Resources,
			Armor,
			Melee,
			Ranged,
			Dukes,
			Magazines,
			Books
		}

		public string name;

		public string lootQualityTemplate;

		public int minCount;

		public int maxCount;

		public int minQuality = -1;

		public int maxQuality = -1;

		public float minLevel;

		public float maxLevel;

		public string[] modsToInstall;

		public float modChance = 1f;

		public readonly List<LootEntry> items = new List<LootEntry>();

		public AbundanceLootModTypes abundanceType;
	}

	public class LootEntry
	{
		public string lootProbTemplate;

		public int minCount;

		public int maxCount;

		public int minQuality;

		public int maxQuality;

		public float minLevel;

		public float maxLevel;

		public float prob;

		public bool forceProb;

		public string[] modsToInstall;

		public float modChance = 1f;

		public float lootstageCountMod;

		public LootItem item;

		public LootGroup group;

		public LootGroup parentGroup;

		public FastTags<TagGroup.Global> tags;

		public string[] buffsToAdd;

		public bool randomDurability = true;

		public List<BaseLootEntryRequirement> Requirements;

		public bool HasRequirements(EntityPlayer player)
		{
			if (Requirements != null)
			{
				for (int i = 0; i < Requirements.Count; i++)
				{
					if (!Requirements[i].CheckRequirement(player))
					{
						return false;
					}
				}
			}
			return true;
		}
	}

	public class LootProbabilityTemplate
	{
		public string name;

		public readonly List<LootEntry> templates = new List<LootEntry>();
	}

	public class LootQualityTemplate
	{
		public string name;

		public readonly List<LootGroup> templates = new List<LootGroup>();
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static readonly Dictionary<string, LootContainer> lootContainers = new CaseInsensitiveStringDictionary<LootContainer>();

	public static readonly Dictionary<string, LootGroup> lootGroups = new Dictionary<string, LootGroup>();

	public static readonly Dictionary<string, LootQualityTemplate> lootQualityTemplates = new Dictionary<string, LootQualityTemplate>();

	public static readonly Dictionary<string, LootProbabilityTemplate> lootProbTemplates = new Dictionary<string, LootProbabilityTemplate>();

	public string Name;

	public string soundOpen;

	public string soundClose;

	public Vector2i size;

	public float openTime;

	public int minCount;

	public int maxCount;

	public DestroyOnClose destroyOnClose;

	public string lootQualityTemplate;

	public List<string> BuffActions;

	public string OnOpenEvent = "";

	public bool ignoreLootAbundance;

	public bool useUnmodifiedLootstage;

	public bool UniqueItems;

	public bool IgnoreLootProb;

	public readonly List<LootEntry> itemsToSpawn = new List<LootEntry>();

	public static Dictionary<EntityPlayer, string[]> OverrideItems = new Dictionary<EntityPlayer, string[]>();

	public static float GlobalCountModifier = 1f;

	public static float FoodCountModifier = 1f;

	public static float DrinkCountModifier = 1f;

	public static float AmmoCountModifier = 1f;

	public static float MedicalCountModifier = 1f;

	public static float ResourceCountModifier = 1f;

	public static float ArmorCountModifier = 1f;

	public static float MeleeCountModifier = 1f;

	public static float RangedCountModifier = 1f;

	public static float DukesCountModifier = 1f;

	public static float MagazinesCountModifier = 1f;

	public static float BookCountModifier = 1f;

	public static float TreasureMapChance = 1f;

	public static int LootMaxTier = 6;

	public static float LootTimerModifier = 1f;

	public static float LootBagChance = 1f;

	public static bool NoLoot = false;

	public static FastTags<TagGroup.Global> foodTags = FastTags<TagGroup.Global>.Parse("food");

	public static FastTags<TagGroup.Global> drinksTags = FastTags<TagGroup.Global>.Parse("drinks");

	public static FastTags<TagGroup.Global> ammoTags = FastTags<TagGroup.Global>.Parse("ammo");

	public static FastTags<TagGroup.Global> medicalTags = FastTags<TagGroup.Global>.Parse("medical");

	public static FastTags<TagGroup.Global> junkTags = FastTags<TagGroup.Global>.Parse("junk");

	public static FastTags<TagGroup.Global> armorTags = FastTags<TagGroup.Global>.Parse("armor,armorParts");

	public static FastTags<TagGroup.Global> meleeTags = FastTags<TagGroup.Global>.Parse("melee,meleeParts");

	public static FastTags<TagGroup.Global> rangedTags = FastTags<TagGroup.Global>.Parse("ranged,rangedParts");

	public static FastTags<TagGroup.Global> dukesTags = FastTags<TagGroup.Global>.Parse("dukes");

	public static FastTags<TagGroup.Global> magazineTags = FastTags<TagGroup.Global>.Parse("csm");

	public static FastTags<TagGroup.Global> booksTags = FastTags<TagGroup.Global>.Parse("books");

	public static FastTags<TagGroup.Global> treasureTags = FastTags<TagGroup.Global>.Parse("treasureMap");

	public static void InitStatic()
	{
		Cleanup();
	}

	public void Init()
	{
		lootContainers[Name] = this;
	}

	public static void Cleanup()
	{
		lootContainers.Clear();
		lootGroups.Clear();
		lootQualityTemplates.Clear();
		lootProbTemplates.Clear();
	}

	public static bool IsLoaded()
	{
		return lootContainers.Count > 0;
	}

	public static LootContainer GetLootContainer(string _name, bool _errorOnMiss = true)
	{
		if (string.IsNullOrEmpty(_name))
		{
			return null;
		}
		if (lootContainers.TryGetValue(_name, out var value))
		{
			return value;
		}
		if (_errorOnMiss)
		{
			Log.Error("LootContainer '" + _name + "' unknown");
		}
		return null;
	}

	public static ItemStack GetRewardItem(string lootGroup, float questDifficulty)
	{
		if (!lootGroups.ContainsKey(lootGroup))
		{
			return ItemStack.Empty;
		}
		List<ItemStack> list = new List<ItemStack>();
		int slotsLeft = 1;
		SpawnItemsFromGroup(GameManager.Instance.lootManager.Random, lootGroups[lootGroup], 1, 1f, list, ref slotsLeft, questDifficulty, 0f, lootGroups[lootGroup].lootQualityTemplate, null, FastTags<TagGroup.Global>.none, uniqueItems: true, ignoreLootProb: true, _forceStacking: false, _ignoreLootAbundance: true, null, _sandboxModifierUsed: true);
		if (list.Count == 0)
		{
			return ItemStack.Empty;
		}
		return list[0];
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static bool SpawnItem(GameRandom random, LootEntry template, ItemValue lootItemValue, int countToSpawn, List<ItemStack> spawnedItems, ref int slotsLeft, float gameStage, string lootQualityTemplate, EntityPlayer player, FastTags<TagGroup.Global> containerTags, bool _forceStacking)
	{
		if (lootItemValue.ItemClass == null)
		{
			return false;
		}
		if ((bool)player)
		{
			countToSpawn = Math.Min((int)EffectManager.GetValue(PassiveEffects.LootQuantity, player.inventory.holdingItemItemValue, countToSpawn, player, null, lootItemValue.ItemClass.ItemTags | containerTags), lootItemValue.ItemClass.MaxCount);
		}
		if (countToSpawn < 1 || ((bool)player && GlobalCountModifier == 0f))
		{
			return false;
		}
		if (lootItemValue.HasQuality && LootMaxTier == 0)
		{
			return false;
		}
		ItemClass itemClass = ItemClass.HandleSandboxTechType(lootItemValue.ItemClass);
		if (itemClass != lootItemValue.ItemClass)
		{
			if (itemClass == null)
			{
				return false;
			}
			lootItemValue = ItemClass.GetItem(itemClass.Name);
		}
		if (lootItemValue.ItemClass.CanStack())
		{
			int num = lootItemValue.ItemClass.MaxCount;
			for (int i = 0; i < spawnedItems.Count; i++)
			{
				ItemStack itemStack = spawnedItems[i];
				if (itemStack.itemValue.type == lootItemValue.type)
				{
					if (itemStack.CanStack(countToSpawn) || _forceStacking)
					{
						itemStack.count += countToSpawn;
						return true;
					}
					int num2 = num - itemStack.count;
					itemStack.count = num;
					countToSpawn -= num2;
				}
			}
		}
		if (slotsLeft < 1)
		{
			return false;
		}
		int num3 = template.minQuality;
		int num4 = template.maxQuality;
		string text = lootQualityTemplate;
		if (string.IsNullOrEmpty(text) || template.parentGroup?.lootQualityTemplate != null)
		{
			text = template.parentGroup?.lootQualityTemplate;
		}
		if (!string.IsNullOrEmpty(text))
		{
			bool flag = false;
			for (int j = 0; j < lootQualityTemplates[text].templates.Count; j++)
			{
				_ = random.RandomFloat;
				LootGroup lootGroup = lootQualityTemplates[text].templates[j];
				num3 = lootGroup.minQuality;
				num4 = lootGroup.maxQuality;
				if (!(lootGroup.minLevel <= gameStage) || !(lootGroup.maxLevel >= gameStage))
				{
					continue;
				}
				for (int k = 0; k < lootGroup.items.Count; k++)
				{
					LootEntry lootEntry = lootGroup.items[k];
					if (random.RandomFloat <= lootEntry.prob)
					{
						num3 = lootEntry.minQuality;
						num4 = lootEntry.maxQuality;
						flag = true;
						break;
					}
				}
				if (flag)
				{
					break;
				}
			}
		}
		string[] modsToInstall = template.modsToInstall;
		float modChance = template.modChance;
		if (template.parentGroup != null && template.parentGroup.modsToInstall.Length != 0)
		{
			modsToInstall = template.parentGroup.modsToInstall;
			modChance = template.parentGroup.modChance;
		}
		ItemValue itemValue;
		if (lootItemValue.HasQuality)
		{
			if (num3 <= -1)
			{
				num3 = 1;
				num4 = 6;
			}
			if (LootMaxTier != -1 && num4 > LootMaxTier)
			{
				num4 = LootMaxTier;
			}
			if (num3 > num4)
			{
				num3 = num4;
			}
			itemValue = new ItemValue(lootItemValue.type, num3, num4, _bCreateDefaultModItems: true, modsToInstall, modChance);
		}
		else
		{
			itemValue = new ItemValue(lootItemValue.type, 1, 6, _bCreateDefaultModItems: true, modsToInstall, modChance);
		}
		ItemClass itemClass2 = itemValue.ItemClass;
		if (itemClass2 != null)
		{
			itemClass2.AddGSStats(itemValue, (int)gameStage, random);
			if (itemClass2.Actions != null && itemClass2.Actions.Length != 0 && itemClass2.Actions[0] != null)
			{
				itemValue.Meta = 0;
			}
			if (itemValue.MaxUseTimes > 0)
			{
				if (template.randomDurability)
				{
					itemValue.UseTimes = (int)((float)itemValue.MaxUseTimes * random.RandomRange(0.2f, 0.8f));
				}
				else
				{
					itemValue.UseTimes = 0f;
				}
			}
		}
		ItemStack item;
		if ((bool)player)
		{
			if (!OverrideItems.ContainsKey(player))
			{
				item = new ItemStack(itemValue, countToSpawn);
			}
			else
			{
				string[] array = OverrideItems[player];
				item = new ItemStack(ItemClass.GetItem(array[random.RandomRange(array.Length)]), 1);
			}
		}
		else
		{
			item = new ItemStack(itemValue, countToSpawn);
		}
		spawnedItems.Add(item);
		slotsLeft--;
		return true;
	}

	public static int RandomSpawnCount(GameRandom random, int min, int max, float abundance)
	{
		if (min < 0)
		{
			return -1;
		}
		float num = random.RandomRange((float)min - 0.49f, (float)max + 0.49f);
		if (num < (float)min)
		{
			num = min;
		}
		if (num > (float)max)
		{
			num = max;
		}
		num *= abundance;
		int num2 = (int)num;
		float num3 = num - (float)num2;
		if (random.RandomFloat < num3)
		{
			num2++;
		}
		return num2;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static bool SpawnAllItemsFromList(GameRandom random, List<LootEntry> itemSet, float abundance, List<ItemStack> spawnedItems, ref int slotsLeft, float playerLevelPercentage, float rareLootChance, string lootQualityTemplate, EntityPlayer player, FastTags<TagGroup.Global> containerTags, bool uniqueItems, bool ignoreLootProb, bool _forceStacking, bool _ignoreLootAbundance, List<string> _buffsToAdd)
	{
		bool flag = false;
		for (int i = 0; i < itemSet.Count; i++)
		{
			LootEntry lootEntry = itemSet[i];
			if ((bool)player && !lootEntry.HasRequirements(player))
			{
				continue;
			}
			bool flag2 = false;
			if (!lootEntry.forceProb || random.RandomFloat <= getProbability(player, lootEntry, playerLevelPercentage, ignoreLootProb))
			{
				float abundance2 = ((lootEntry.group == null) ? abundance : 1f);
				var (num, sandboxModifierUsed) = RandomCountFromSandbox(random, lootEntry.group, lootEntry.minCount, lootEntry.maxCount, abundance2);
				if (lootEntry.group != null)
				{
					if (lootEntry.group.minLevel <= playerLevelPercentage && lootEntry.group.maxLevel >= playerLevelPercentage)
					{
						flag2 = SpawnItemsFromGroup(random, lootEntry.group, num, abundance, spawnedItems, ref slotsLeft, playerLevelPercentage, rareLootChance, lootQualityTemplate, player, containerTags, uniqueItems, ignoreLootProb, _forceStacking, _ignoreLootAbundance, _buffsToAdd, sandboxModifierUsed);
					}
				}
				else
				{
					if (!_ignoreLootAbundance)
					{
						num = RandomCountFromSandboxTags(random, lootEntry.item.itemValue, num);
					}
					flag2 = SpawnItem(random, lootEntry, lootEntry.item.itemValue, num, spawnedItems, ref slotsLeft, playerLevelPercentage, lootQualityTemplate, player, containerTags, _forceStacking);
				}
			}
			flag = flag || flag2;
			if (flag2 && lootEntry.buffsToAdd != null)
			{
				_buffsToAdd.AddRange(lootEntry.buffsToAdd);
			}
		}
		return flag;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static bool SpawnItemsFromGroup(GameRandom random, LootGroup group, int numToSpawn, float abundance, List<ItemStack> spawnedItems, ref int slotsLeft, float gameStage, float rareLootChance, string lootQualityTemplate, EntityPlayer player, FastTags<TagGroup.Global> containerTags, bool uniqueItems, bool ignoreLootProb, bool _forceStacking, bool _ignoreLootAbundance, List<string> _buffsToAdd, bool _sandboxModifierUsed)
	{
		bool flag = false;
		for (int i = 0; i < numToSpawn; i++)
		{
			if (slotsLeft <= 0)
			{
				break;
			}
			if (_sandboxModifierUsed)
			{
				flag |= SpawnLootItemsFromList(random, group.items, RandomSpawnCount(random, group.minCount, group.maxCount, 1f), abundance, spawnedItems, ref slotsLeft, gameStage, rareLootChance, lootQualityTemplate, player, containerTags, uniqueItems, ignoreLootProb, _forceStacking, _ignoreLootAbundance, _buffsToAdd, _sandboxModifierUsed);
				continue;
			}
			(int, bool) tuple = RandomCountFromSandbox(random, group, group.minCount, group.maxCount, 1f);
			int item = tuple.Item1;
			bool item2 = tuple.Item2;
			flag |= SpawnLootItemsFromList(random, group.items, item, abundance, spawnedItems, ref slotsLeft, gameStage, rareLootChance, lootQualityTemplate, player, containerTags, uniqueItems, ignoreLootProb, _forceStacking, _ignoreLootAbundance, _buffsToAdd, item2);
		}
		return flag;
	}

	public static bool SpawnLootItemsFromList(GameRandom random, List<LootEntry> itemSet, int numToSpawn, float abundance, List<ItemStack> spawnedItems, ref int slotsLeft, float lootStage, float rareLootChance, string lootQualityTemplate, EntityPlayer player, FastTags<TagGroup.Global> containerTags, bool uniqueItems, bool ignoreLootProb, bool _forceStacking, bool _ignoreLootAbundance, List<string> _buffsToAdd, bool _sandboxModifierUsed)
	{
		if (numToSpawn < 1)
		{
			if (numToSpawn == -1)
			{
				return SpawnAllItemsFromList(random, itemSet, abundance, spawnedItems, ref slotsLeft, lootStage, rareLootChance, lootQualityTemplate, player, containerTags, uniqueItems, ignoreLootProb, _forceStacking, _ignoreLootAbundance, _buffsToAdd);
			}
			return false;
		}
		float num = 0f;
		for (int i = 0; i < itemSet.Count; i++)
		{
			LootEntry lootEntry = itemSet[i];
			if (!lootEntry.forceProb)
			{
				num += getProbability(player, lootEntry, lootStage, ignoreLootProb);
			}
		}
		if (num == 0f)
		{
			return false;
		}
		List<int> list = new List<int>();
		bool flag = false;
		for (int j = 0; j < numToSpawn; j++)
		{
			float num2 = 0f;
			float randomFloat = random.RandomFloat;
			for (int k = 0; k < itemSet.Count; k++)
			{
				bool flag2 = false;
				LootEntry lootEntry2 = itemSet[k];
				if (list.Contains(k) && (lootEntry2.forceProb || uniqueItems))
				{
					continue;
				}
				float probability = getProbability(player, lootEntry2, lootStage, ignoreLootProb);
				bool flag3;
				if (lootEntry2.forceProb)
				{
					flag3 = random.RandomFloat <= probability;
				}
				else
				{
					num2 += probability / num;
					flag3 = randomFloat <= num2 + rareLootChance;
				}
				if (!flag3)
				{
					continue;
				}
				list.Add(k);
				if (uniqueItems)
				{
					num -= getProbability(player, lootEntry2, lootStage, ignoreLootProb);
				}
				bool sandboxModifierUsed = _sandboxModifierUsed;
				float abundance2 = ((lootEntry2.group == null) ? abundance : 1f);
				int num3;
				if (!_sandboxModifierUsed)
				{
					(num3, sandboxModifierUsed) = RandomCountFromSandbox(random, lootEntry2.group, lootEntry2.minCount, lootEntry2.maxCount, abundance2);
				}
				else
				{
					num3 = RandomSpawnCount(random, lootEntry2.minCount, lootEntry2.maxCount, abundance2);
				}
				num3 += Mathf.RoundToInt((float)num3 * (lootEntry2.lootstageCountMod * lootStage));
				if (lootEntry2.group != null)
				{
					if (lootEntry2.group.minLevel <= lootStage && lootEntry2.group.maxLevel >= lootStage)
					{
						flag2 = SpawnItemsFromGroup(random, lootEntry2.group, num3, abundance, spawnedItems, ref slotsLeft, lootStage, rareLootChance, lootQualityTemplate, player, containerTags, uniqueItems, ignoreLootProb, _forceStacking, _ignoreLootAbundance, _buffsToAdd, sandboxModifierUsed);
					}
				}
				else
				{
					if (!_ignoreLootAbundance && !_sandboxModifierUsed)
					{
						num3 = RandomCountFromSandboxTags(random, lootEntry2.item.itemValue, num3);
					}
					flag2 = SpawnItem(random, lootEntry2, lootEntry2.item.itemValue, num3, spawnedItems, ref slotsLeft, lootStage, lootQualityTemplate, player, containerTags, _forceStacking);
				}
				flag = flag || flag2;
				if (flag2 && lootEntry2.buffsToAdd != null)
				{
					_buffsToAdd.AddRange(lootEntry2.buffsToAdd);
				}
				break;
			}
		}
		return flag;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static int RandomCountFromSandboxTags(GameRandom rand, ItemValue itemValue, int itemSpawnCount)
	{
		float num = 1f;
		ItemClass itemClass = itemValue.ItemClass;
		if (itemClass.HasAnyTags(foodTags))
		{
			num = FoodCountModifier;
		}
		else if (itemClass.HasAnyTags(drinksTags))
		{
			num = DrinkCountModifier;
		}
		else if (itemClass.HasAnyTags(ammoTags))
		{
			num = AmmoCountModifier;
		}
		else if (itemClass.HasAnyTags(medicalTags))
		{
			num = MedicalCountModifier;
		}
		else if (itemClass.HasAnyTags(junkTags))
		{
			num = ResourceCountModifier;
		}
		else if (itemClass.HasAnyTags(armorTags))
		{
			num = ArmorCountModifier;
		}
		else if (itemClass.HasAnyTags(meleeTags))
		{
			num = MeleeCountModifier;
		}
		else if (itemClass.HasAnyTags(rangedTags))
		{
			num = RangedCountModifier;
		}
		else if (itemClass.HasAnyTags(dukesTags))
		{
			num = DukesCountModifier;
		}
		else if (itemClass.HasAnyTags(magazineTags))
		{
			num = MagazinesCountModifier;
		}
		else if (itemClass.HasAnyTags(booksTags))
		{
			num = BookCountModifier;
		}
		if (num == 0f)
		{
			return 0;
		}
		if (num == 1f)
		{
			return itemSpawnCount;
		}
		return RandomSpawnCount(rand, itemSpawnCount, itemSpawnCount, num);
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static (int, bool) RandomCountFromSandbox(GameRandom _random, LootGroup _group, int _minCount, int _maxCount, float _abundance)
	{
		float countMultiplierFromSandbox = GetCountMultiplierFromSandbox(_group?.abundanceType ?? LootGroup.AbundanceLootModTypes.None);
		if (countMultiplierFromSandbox >= 0f)
		{
			_abundance *= countMultiplierFromSandbox;
		}
		return (RandomSpawnCount(_random, _minCount, _maxCount, _abundance), countMultiplierFromSandbox >= 0f);
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static float GetCountMultiplierFromSandbox(LootGroup.AbundanceLootModTypes abundanceType)
	{
		return abundanceType switch
		{
			LootGroup.AbundanceLootModTypes.Food => FoodCountModifier, 
			LootGroup.AbundanceLootModTypes.Drinks => DrinkCountModifier, 
			LootGroup.AbundanceLootModTypes.Ammo => AmmoCountModifier, 
			LootGroup.AbundanceLootModTypes.Meds => MedicalCountModifier, 
			LootGroup.AbundanceLootModTypes.Resources => ResourceCountModifier, 
			LootGroup.AbundanceLootModTypes.Armor => ArmorCountModifier, 
			LootGroup.AbundanceLootModTypes.Melee => MeleeCountModifier, 
			LootGroup.AbundanceLootModTypes.Ranged => RangedCountModifier, 
			LootGroup.AbundanceLootModTypes.Dukes => DukesCountModifier, 
			LootGroup.AbundanceLootModTypes.Magazines => MagazinesCountModifier, 
			LootGroup.AbundanceLootModTypes.Books => BookCountModifier, 
			_ => -1f, 
		};
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static float GetSandboxProb(ItemClass _item)
	{
		if (_item.ItemTags.Test_AnySet(treasureTags))
		{
			return TreasureMapChance;
		}
		return 1f;
	}

	[PublicizedFrom(EAccessModifier.Private)]
	public static float getProbability(EntityPlayer _player, LootEntry _item, float _lootstage, bool _ignoreLootProb)
	{
		if (_player != null && !_item.HasRequirements(_player))
		{
			return 0f;
		}
		if (_item.lootProbTemplate != string.Empty && lootProbTemplates.ContainsKey(_item.lootProbTemplate))
		{
			LootProbabilityTemplate lootProbabilityTemplate = lootProbTemplates[_item.lootProbTemplate];
			for (int i = 0; i < lootProbabilityTemplate.templates.Count; i++)
			{
				LootEntry lootEntry = lootProbabilityTemplate.templates[i];
				if (!(lootEntry.minLevel <= _lootstage) || !(lootEntry.maxLevel >= _lootstage))
				{
					continue;
				}
				if (_item.item != null && !_item.item.itemValue.ItemClass.ItemTags.IsEmpty)
				{
					float sandboxProb = GetSandboxProb(_item.item.itemValue.ItemClass);
					if (_ignoreLootProb)
					{
						return lootEntry.prob * sandboxProb;
					}
					return EffectManager.GetValue(PassiveEffects.LootProb, null, lootEntry.prob, _player, null, _item.item.itemValue.ItemClass.ItemTags) * sandboxProb;
				}
				if (_item.tags.IsEmpty)
				{
					return lootEntry.prob;
				}
				return EffectManager.GetValue(PassiveEffects.LootProb, null, lootEntry.prob, _player, null, _item.tags);
			}
		}
		if (_item.item != null && !_item.item.itemValue.ItemClass.ItemTags.IsEmpty)
		{
			float sandboxProb2 = GetSandboxProb(_item.item.itemValue.ItemClass);
			if (_ignoreLootProb)
			{
				return _item.prob * sandboxProb2;
			}
			return EffectManager.GetValue(PassiveEffects.LootProb, null, _item.prob, _player, null, _item.item.itemValue.ItemClass.ItemTags) * sandboxProb2;
		}
		if (_item.tags.IsEmpty)
		{
			return _item.prob;
		}
		return EffectManager.GetValue(PassiveEffects.LootProb, null, _item.prob, _player, null, _item.tags);
	}

	public void ExecuteBuffActions(int instigatorId, EntityAlive target)
	{
		if (BuffActions != null)
		{
			for (int i = 0; i < BuffActions.Count; i++)
			{
				target.Buffs.AddBuff(BuffActions[i]);
			}
		}
	}

	public List<ItemStack> Spawn(GameRandom random, int _maxItems, float playerLevelPercentage, float rareLootChance, EntityPlayer player, FastTags<TagGroup.Global> containerTags, bool uniqueItems, bool ignoreLootProb, bool ignoreLootAbundance)
	{
		float abundance = 1f;
		float abundance2 = 1f;
		if (!ignoreLootAbundance)
		{
			if (GlobalCountModifier <= 1f)
			{
				abundance = GlobalCountModifier;
			}
			else
			{
				abundance2 = GlobalCountModifier;
			}
		}
		int v = RandomSpawnCount(random, minCount, maxCount, abundance2);
		v = Utils.FastMin(v, _maxItems);
		List<ItemStack> list = new List<ItemStack>();
		List<string> list2 = new List<string>();
		if (SpawnLootItemsFromList(random, itemsToSpawn, v, abundance, list, ref _maxItems, playerLevelPercentage, rareLootChance, lootQualityTemplate, player, containerTags, uniqueItems, ignoreLootProb, _forceStacking: false, ignoreLootAbundance, list2, ignoreLootAbundance))
		{
			foreach (string item in list2)
			{
				player.Buffs.AddBuff(item);
			}
			return list;
		}
		return list;
	}

	[Conditional("DEBUG_LOOTLOG")]
	[PublicizedFrom(EAccessModifier.Private)]
	public static void LootLog(string _name, string _format = "", params object[] _args)
	{
		_format = $"{GameManager.frameCount} LootContainer {_name} {_format}";
		Log.Warning(_format, _args);
	}
}
