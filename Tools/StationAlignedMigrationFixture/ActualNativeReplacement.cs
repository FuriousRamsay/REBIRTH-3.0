using System.Collections.Generic;using TileEntity=NativeLifecycle.TileBase;namespace NativeLifecycle {public partial class TileBase {
public virtual void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)
	{
	}
public void ReplacedByNativeBase(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)
	{
	}
}public partial class TileEntityWorkstation {
public override void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)
	{
		base.ReplacedBy(_bvOld, _bvNew, _teNew);
		if (_teNew.TryGetSelfOrFeature<TileEntityWorkstation>(out var _))
		{
			return;
		}
		List<ItemStack> list = new List<ItemStack>();
		if (fuel != null)
		{
			list.AddRange(fuel);
		}
		if (input != null)
		{
			for (int i = 0; i < 3; i++)
			{
				if (!input[i].IsEmpty())
				{
					list.Add(input[i]);
				}
			}
			List<Recipe> allRecipes = CraftingManager.GetAllRecipes();
			for (int j = 0; j < materialNames.Length; j++)
			{
				int num = j + 3;
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
}public partial class CandidateTileEntityWorkstation {public bool CurrentLayoutWitness;private bool OriginalOwnerHasAuthenticatedCurrentNinePlainLayout(TileEntityWorkstation tile)=>CurrentLayoutWitness&&object.ReferenceEquals(tile,this);
public override void ReplacedBy(BlockValue _bvOld, BlockValue _bvNew, TileEntity _teNew)
	{
		ReplacedByNativeBase(_bvOld, _bvNew, _teNew);
		if (_teNew.TryGetSelfOrFeature<TileEntityWorkstation>(out var _))
		{
			return;
		}
		List<ItemStack> list = new List<ItemStack>();
		if (fuel != null)
		{
			list.AddRange(fuel);
		}
		if (input != null)
		{
			int physicalInputCount = RebirthPlainStationNativeMigrationCandidate.ResolvePhysicalSlots(
				OriginalOwnerHasAuthenticatedCurrentNinePlainLayout(this), 9, materialNames, input, lastInput, currentMeltTimesLeft);
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
				int num = j + 3;
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