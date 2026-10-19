using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronPit
{
    // Every 4 s the server walks each online player's inventories to update spoilage and other
    // transitions (ServerSystemInventory.UpdateTransitionStates). The player backpack inventory
    // can report more slots than its bag list holds, e.g. while a mod that adds bag slots
    // (XSkills) is still setting a joining player up. Reading the extra slot throws, and the
    // throw ends the whole pass, so no player's items get updated that tick, and the log fills.
    // This prefix runs the same pass but stops reading an inventory at a missing slot.
    [HarmonyPatch]
    public static class TransitionStatesSafetyPatch
    {
        // Set from IronPitMod.StartServerSide.
        public static ICoreServerAPI? Api;

        static readonly Type? CreativeInventoryType = AccessTools.TypeByName("Vintagestory.Common.InventoryPlayerCreative");
        static readonly HashSet<string> warned = new HashSet<string>();

        static MethodBase? TargetMethod()
        {
            Type? system = AccessTools.TypeByName("Vintagestory.Server.ServerSystemInventory");
            return system == null ? null : AccessTools.Method(system, "UpdateTransitionStates");
        }

        // Skip the patch (instead of failing to load) if a game update renames the method.
        static bool Prepare() => TargetMethod() != null;

        public static bool Prefix(ref bool ___ItemFilterEnabled)
        {
            if (Api == null) return true;

            ___ItemFilterEnabled = Api.World.Config.GetBool("itemFilterEnabled");

            foreach (IServerPlayer player in Api.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) continue;

                foreach (IInventory inventory in player.InventoryManager.Inventories.Values)
                {
                    if (inventory is not InventoryBasePlayer || CreativeInventoryType?.IsInstanceOfType(inventory) == true) continue;
                    UpdateInventory(player, inventory);
                }
            }
            return false; // replaces vanilla
        }

        static void UpdateInventory(IServerPlayer player, IInventory inventory)
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                ItemSlot? slot;
                try
                {
                    slot = inventory[i];
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Every later slot is out of range too.
                    if (warned.Add(player.PlayerUID + "/" + inventory.InventoryID))
                    {
                        Api!.Logger.Warning("[IronPit] {0}'s inventory {1} reports {2} slots but slot {3} does not exist; skipping the rest until it recovers. Probably another mod's extra bag slots.",
                            player.PlayerName, inventory.InventoryID, inventory.Count, i);
                    }
                    return;
                }
                slot?.Itemstack?.Collectible?.UpdateAndGetTransitionStates(Api!.World, slot);
            }
        }
    }
}
