using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronPit
{
    // BlockSmeltingContainer.GetOutputText is not virtual, so our crucible subclass
    // cannot override it. This prefix hands the call to BlockIronPitCrucible when the
    // crucible in the fire pit is ours and it has something to say; otherwise vanilla runs.
    [HarmonyPatch(typeof(BlockSmeltingContainer), nameof(BlockSmeltingContainer.GetOutputText))]
    public static class CrucibleOutputTextPatch
    {
        public static bool Prefix(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ref string? __result)
        {
            if (inputSlot?.Itemstack?.Collectible is BlockIronPitCrucible crucible
                && crucible.TryGetOutputText(world, cookingSlotsProvider, out string? text))
            {
                __result = text;
                return false; // skip vanilla
            }
            return true;
        }
    }
}
