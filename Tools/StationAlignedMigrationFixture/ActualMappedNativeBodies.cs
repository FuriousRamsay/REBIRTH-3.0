using System.Collections.Generic;using System.Runtime.CompilerServices;using TileEntity=NativeLifecycle.TileBase;namespace NativeLifecycle {public partial class TileBase {
public Vector3i localChunkPos
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			return chunkPos;
		}
		set
		{
			chunkPos = value;
			OnSetLocalChunkPosition();
		}
	}
}public partial class TileEntityWorkstation {
public void HandleMaterialInput(float timePassed)
	{
		if (XUiM_Recipes.DisableSmelter || !isModuleUsed[4] || (!isBurning && isModuleUsed[3]))
		{
			return;
		}
		for (int i = 0; i < input.Length - materialNames.Length; i++)
		{
			if (input[i].IsEmpty())
			{
				input[i].Clear();
				currentMeltTimesLeft[i] = -2.1474836E+09f;
				if (this.InputChanged != null)
				{
					this.InputChanged();
				}
				continue;
			}
			ItemClass forId = ItemClass.GetForId(input[i].itemValue.type);
			if (forId == null)
			{
				continue;
			}
			if (currentMeltTimesLeft[i] >= 0f && input[i].count > 0)
			{
				if (lastInput[i].itemValue.type != input[i].itemValue.type)
				{
					currentMeltTimesLeft[i] = -2.1474836E+09f;
				}
				else
				{
					currentMeltTimesLeft[i] -= timePassed;
				}
			}
			if (currentMeltTimesLeft[i] == -2.1474836E+09f && input[i].count > 0)
			{
				for (int j = 0; j < materialNames.Length; j++)
				{
					if (forId.MadeOfMaterial.ForgeCategory == null || !forId.MadeOfMaterial.ForgeCategory.EqualsCaseInsensitive(materialNames[j]))
					{
						continue;
					}
					ItemClass itemClass = ItemClass.GetItemClass("unit_" + materialNames[j]);
					if (itemClass == null || itemClass.MadeOfMaterial.ForgeCategory == null)
					{
						continue;
					}
					float _originalValue = (float)forId.GetWeight() * ((forId.MeltTimePerUnit > 0f) ? forId.MeltTimePerUnit : 1f);
					if (isModuleUsed[0])
					{
						for (int k = 0; k < tools.Length; k++)
						{
							float _perc_value = 1f;
							tools[k].itemValue.ModifyValue(null, null, PassiveEffects.CraftingSmeltTime, ref _originalValue, ref _perc_value, FastTags<TagGroup.Global>.Parse(forId.Name));
							_originalValue *= _perc_value;
						}
					}
					if (_originalValue > 0f && currentMeltTimesLeft[i] == -2.1474836E+09f)
					{
						currentMeltTimesLeft[i] = _originalValue;
					}
					else
					{
						currentMeltTimesLeft[i] += _originalValue;
					}
				}
				lastInput[i] = input[i].Clone();
			}
			if (currentMeltTimesLeft[i] == -2.1474836E+09f)
			{
				continue;
			}
			int num = 0;
			for (int l = 3; (l < input.Length) & (num < materialNames.Length); l++)
			{
				if (forId.MadeOfMaterial.ForgeCategory != null && forId.MadeOfMaterial.ForgeCategory.EqualsCaseInsensitive(materialNames[num]))
				{
					ItemClass itemClass2 = ItemClass.GetItemClass("unit_" + materialNames[num]);
					if (itemClass2 != null && itemClass2.MadeOfMaterial.ForgeCategory != null)
					{
						if (input[l].itemValue.type == 0)
						{
							input[l] = new ItemStack(new ItemValue(itemClass2.Id), input[l].count);
						}
						bool flag = false;
						while (currentMeltTimesLeft[i] < 0f && currentMeltTimesLeft[i] != -2.1474836E+09f)
						{
							if (input[i].count <= 0)
							{
								input[i].Clear();
								currentMeltTimesLeft[i] = 0f;
								flag = true;
								if (this.InputChanged != null)
								{
									this.InputChanged();
								}
								break;
							}
							if (input[l].count + forId.GetWeight() <= itemClass2.MaxCount)
							{
								input[l].count += forId.GetWeight();
								input[i].count--;
								float _originalValue2 = (float)forId.GetWeight() * ((forId.MeltTimePerUnit > 0f) ? forId.MeltTimePerUnit : 1f);
								if (isModuleUsed[0])
								{
									for (int m = 0; m < tools.Length; m++)
									{
										if (!tools[m].IsEmpty())
										{
											float _perc_value2 = 1f;
											tools[m].itemValue.ModifyValue(null, null, PassiveEffects.CraftingSmeltTime, ref _originalValue2, ref _perc_value2, FastTags<TagGroup.Global>.Parse(itemClass2.Name));
											_originalValue2 *= _perc_value2;
										}
									}
								}
								currentMeltTimesLeft[i] += _originalValue2;
								if (input[i].count <= 0)
								{
									input[i].Clear();
									currentMeltTimesLeft[i] = -2.1474836E+09f;
									flag = true;
									if (this.InputChanged != null)
									{
										this.InputChanged();
									}
									break;
								}
								if (this.InputChanged != null)
								{
									this.InputChanged();
								}
								flag = true;
								continue;
							}
							currentMeltTimesLeft[i] = -2.1474836E+09f;
							break;
						}
						if (flag && currentMeltTimesLeft[i] < 0f && currentMeltTimesLeft[i] != -2.1474836E+09f)
						{
							currentMeltTimesLeft[i] = -2.1474836E+09f;
						}
						break;
					}
				}
				num++;
			}
		}
	}
}public partial class MappedReviewTile {
public override void OnSetLocalChunkPosition()
	{
		if (base.localChunkPos == Vector3i.zero)
		{
			return;
		}
		Block block = chunk.GetBlock(World.toBlockXZ(base.localChunkPos.x), base.localChunkPos.y, World.toBlockXZ(base.localChunkPos.z)).Block;
		// UNIMPLEMENTED original-owner gate BEFORE materialNames/array mutation.
		// Owner may return3 only for admitted native legacy; nine requires exact
		// authored ordered mapping and already aligned input9+M/previous9/timers9.
		// Unknown/mismatched extended custody refuses rather than falling back3.
		if (!OriginalOwnerTryQualifySetupBeforeMutation(this, block, out int physicalInputCount)) return;
		if (block.Properties.Contains("Workstation", "InputMaterials"))
		{
			string @string = block.Properties.GetString("Workstation", "InputMaterials");
			if (@string.Contains(","))
			{
				materialNames = @string.Replace(" ", "").Split(',');
			}
			else
			{
				materialNames = new string[1] { @string };
			}
			if (input.Length != physicalInputCount + materialNames.Length)
			{
				ItemStack[] array = new ItemStack[physicalInputCount + materialNames.Length];
				for (int i = 0; i < input.Length; i++)
				{
					array[i] = input[i].Clone();
				}
				input = array;
				for (int j = 0; j < materialNames.Length; j++)
				{
					ItemClass itemClass = ItemClass.GetItemClass("unit_" + materialNames[j]);
					if (itemClass != null)
					{
						int num = j + physicalInputCount;
						input[num] = new ItemStack(new ItemValue(itemClass.Id), 0);
					}
				}
			}
		}
		if (block.Properties.Contains("Workstation", "Modules"))
		{
			string string2 = block.Properties.GetString("Workstation", "Modules");
			string[] array2 = ((!string2.Contains(",")) ? new string[1] { string2 } : string2.Replace(" ", "").Split(','));
			for (int k = 0; k < array2.Length; k++)
			{
				Module module = EnumUtils.Parse<Module>(array2[k], _ignoreCase: true);
				isModuleUsed[(int)module] = true;
			}
			if (isModuleUsed[4])
			{
				isModuleUsed[1] = true;
			}
		}
	}
public new void HandleMaterialInput(float timePassed)
	{
		if (XUiM_Recipes.DisableSmelter || !isModuleUsed[4] || (!isBurning && isModuleUsed[3]))
		{
			return;
		}
		// UNIMPLEMENTED owner gate: exact current material order/incarnation and
		// inputphysical+M/lastInputphysical/timersphysical; unknown extended refuses.
		if (!OriginalOwnerTryQualifyMaterialProcessing(this, out int physicalInputCount)) return;
		for (int i = 0; i < input.Length - materialNames.Length; i++)
		{
			if (input[i].IsEmpty())
			{
				input[i].Clear();
				currentMeltTimesLeft[i] = -2.1474836E+09f;
				if (this.InputChanged != null)
				{
					this.InputChanged();
				}
				continue;
			}
			ItemClass forId = ItemClass.GetForId(input[i].itemValue.type);
			if (forId == null)
			{
				continue;
			}
			if (currentMeltTimesLeft[i] >= 0f && input[i].count > 0)
			{
				if (lastInput[i].itemValue.type != input[i].itemValue.type)
				{
					currentMeltTimesLeft[i] = -2.1474836E+09f;
				}
				else
				{
					currentMeltTimesLeft[i] -= timePassed;
				}
			}
			if (currentMeltTimesLeft[i] == -2.1474836E+09f && input[i].count > 0)
			{
				for (int j = 0; j < materialNames.Length; j++)
				{
					if (forId.MadeOfMaterial.ForgeCategory == null || !forId.MadeOfMaterial.ForgeCategory.EqualsCaseInsensitive(materialNames[j]))
					{
						continue;
					}
					ItemClass itemClass = ItemClass.GetItemClass("unit_" + materialNames[j]);
					if (itemClass == null || itemClass.MadeOfMaterial.ForgeCategory == null)
					{
						continue;
					}
					float _originalValue = (float)forId.GetWeight() * ((forId.MeltTimePerUnit > 0f) ? forId.MeltTimePerUnit : 1f);
					if (isModuleUsed[0])
					{
						for (int k = 0; k < tools.Length; k++)
						{
							float _perc_value = 1f;
							tools[k].itemValue.ModifyValue(null, null, PassiveEffects.CraftingSmeltTime, ref _originalValue, ref _perc_value, FastTags<TagGroup.Global>.Parse(forId.Name));
							_originalValue *= _perc_value;
						}
					}
					if (_originalValue > 0f && currentMeltTimesLeft[i] == -2.1474836E+09f)
					{
						currentMeltTimesLeft[i] = _originalValue;
					}
					else
					{
						currentMeltTimesLeft[i] += _originalValue;
					}
				}
				lastInput[i] = input[i].Clone();
			}
			if (currentMeltTimesLeft[i] == -2.1474836E+09f)
			{
				continue;
			}
			int num = 0;
			for (int l = physicalInputCount; (l < input.Length) & (num < materialNames.Length); l++)
			{
				if (forId.MadeOfMaterial.ForgeCategory != null && forId.MadeOfMaterial.ForgeCategory.EqualsCaseInsensitive(materialNames[num]))
				{
					ItemClass itemClass2 = ItemClass.GetItemClass("unit_" + materialNames[num]);
					if (itemClass2 != null && itemClass2.MadeOfMaterial.ForgeCategory != null)
					{
						if (input[l].itemValue.type == 0)
						{
							input[l] = new ItemStack(new ItemValue(itemClass2.Id), input[l].count);
						}
						bool flag = false;
						while (currentMeltTimesLeft[i] < 0f && currentMeltTimesLeft[i] != -2.1474836E+09f)
						{
							if (input[i].count <= 0)
							{
								input[i].Clear();
								currentMeltTimesLeft[i] = 0f;
								flag = true;
								if (this.InputChanged != null)
								{
									this.InputChanged();
								}
								break;
							}
							if (input[l].count + forId.GetWeight() <= itemClass2.MaxCount)
							{
								input[l].count += forId.GetWeight();
								input[i].count--;
								float _originalValue2 = (float)forId.GetWeight() * ((forId.MeltTimePerUnit > 0f) ? forId.MeltTimePerUnit : 1f);
								if (isModuleUsed[0])
								{
									for (int m = 0; m < tools.Length; m++)
									{
										if (!tools[m].IsEmpty())
										{
											float _perc_value2 = 1f;
											tools[m].itemValue.ModifyValue(null, null, PassiveEffects.CraftingSmeltTime, ref _originalValue2, ref _perc_value2, FastTags<TagGroup.Global>.Parse(itemClass2.Name));
											_originalValue2 *= _perc_value2;
										}
									}
								}
								currentMeltTimesLeft[i] += _originalValue2;
								if (input[i].count <= 0)
								{
									input[i].Clear();
									currentMeltTimesLeft[i] = -2.1474836E+09f;
									flag = true;
									if (this.InputChanged != null)
									{
										this.InputChanged();
									}
									break;
								}
								if (this.InputChanged != null)
								{
									this.InputChanged();
								}
								flag = true;
								continue;
							}
							currentMeltTimesLeft[i] = -2.1474836E+09f;
							break;
						}
						if (flag && currentMeltTimesLeft[i] < 0f && currentMeltTimesLeft[i] != -2.1474836E+09f)
						{
							currentMeltTimesLeft[i] = -2.1474836E+09f;
						}
						break;
					}
				}
				num++;
			}
		}
	}
public override void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)
	{
		ReplacedByNativeBase(_bvOld, _bvNew, _teNew);
		if (_teNew.TryGetSelfOrFeature<TileEntityWorkstation>(out var _))
		{
			return;
		}
		// UNIMPLEMENTED original lifecycle admission. Unknown extended refuses;
		// caller must preserve/quarantine removed custody, not discard this return.
		if (!OriginalOwnerTryQualifyMappedDestruction(this, out int physicalInputCount)) return;
		List<ItemStack> list = new List<ItemStack>();
		if (fuel != null)
		{
			list.AddRange(fuel);
		}
		if (input != null)
		{
			for (int i = 0; i < physicalInputCount; i++)
			{
				if (!input[i].IsEmpty())
				{
					list.Add(input[i]);
				}
			}
			List<Recipe> allRecipes = CraftingManager.GetAllRecipes();
			for (int j = 0; j < materialNames.Length; j++)
			{
				int num = j + physicalInputCount;
				ItemClass itemClass = ItemClass.GetItemClass("unit_" + materialNames[j]);
				if (itemClass == null || itemClass.MadeOfMaterial.ForgeCategory == null)
				{
					continue;
				}
				ItemStack itemStack = input[num];
				if (itemStack.itemValue.type == 0)
				{
					input[num] = new ItemStack(new ItemValue(itemClass.Id), itemStack.count);
				}
				Recipe recipe = null;
				foreach (Recipe item in allRecipes)
				{
					if (item.ingredients.Count == 1 && item.ingredients[0].itemValue.type == itemClass.Id && (!item.UseIngredientModifier || recipe == null))
					{
						recipe = item;
					}
				}
				if (recipe == null)
				{
					Log.Warning("No craft out recipe found for workstation input " + itemClass.GetItemName());
					continue;
				}
				int num2 = itemStack.count / recipe.ingredients[0].count;
				ItemValue itemValue = new ItemValue(recipe.itemValueType);
				int maxCount = itemValue.ItemClass.MaxCount;
				while (num2 > 0)
				{
					int num3 = Mathf.Min(num2, maxCount);
					list.Add(new ItemStack(itemValue, num3));
					num2 -= num3;
				}
			}
		}
		if (tools != null)
		{
			list.AddRange(tools);
		}
		if (output != null)
		{
			list.AddRange(output);
		}
		Vector3 pos = ToWorldCenterPos();
		pos.y += 0.9f;
		GameManager.Instance.DropContentInLootContainerServer(-1, "DroppedLootContainer", pos, list.ToArray(), _skipIfEmpty: true);
	}
}}