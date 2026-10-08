using UnityEngine.Scripting;
[Preserve] public sealed class XUiC_RebirthPlayersChrome : XUiC_RebirthScreenChrome {
 protected override RebirthCraftingNavigationService.Destination Destination => RebirthCraftingNavigationService.Destination.Players;
}
[Preserve] public sealed class XUiC_RebirthChallengesChrome : XUiC_RebirthScreenChrome {
 protected override RebirthCraftingNavigationService.Destination Destination => RebirthCraftingNavigationService.Destination.Challenges;
 private bool selectedInitialCategory;
 public override void Update(float dt) {
  base.Update(dt);
  if (selectedInitialCategory || windowGroup?.isShowing != true || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
  // Wait for the native category buttons to exist. Only choose the initial category;
  // subsequent visits preserve the player's selection and vanilla categories remain accessible.
  var categories = windowGroup.Controller.GetChildByType<XUiC_CategoryList>();
  if (categories != null && categories.TrySetCategory("RebirthLearning")) selectedInitialCategory = true;
 }
}
