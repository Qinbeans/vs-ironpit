using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace IronPit
{
    // Replaces the fired crucible's block class (patches/crucible-class.json).
    //
    // The fire pit drives smelting through four virtual methods on the crucible:
    // GetMeltingPoint (heat target), GetMeltingDuration (cook time), CanSmelt and DoSmelt.
    // For an "iron charge" (iron ore nuggets, optionally with borax) we answer those
    // ourselves: melt at the refine threshold and produce molten iron ingots with the
    // borax recovery applied. Everything else falls through to vanilla, except
    // remelting finished ferrous metal that is not specifically allowed (see
    // IronPitConfig.BlockFerrousRemelt).
    //
    // GetOutputText (the "Will create N units" line) is not virtual; CrucibleOutputTextPatch handles it.
    public class BlockIronPitCrucible : BlockSmeltingContainer
    {
        static IronPitConfig Config => IronPitMod.Config;

        // Contents of a crucible that will refine into iron.
        public class IronCharge
        {
            public int Nuggets;
            public int Borax;
            public double BaseUnits;     // units vanilla would give (5 per nugget)
            public float Recovery;       // 0..1
            public int Units;            // units after recovery (floor of base units × whole-percent recovery)
            public int Threshold;        // melting point for this charge
        }

        // Returns null unless every ingredient is iron ore or borax and there is at least one ore.
        public IronCharge? GetIronCharge(IWorldAccessor world, ISlotProvider cookingSlotsProvider)
        {
            var charge = new IronCharge();

            foreach (ItemStack? stack in GetIngredients(world, cookingSlotsProvider))
            {
                if (stack == null) continue;

                if (IsFlux(stack))
                {
                    charge.Borax += stack.StackSize;
                    continue;
                }

                CombustibleProperties? props = stack.Collectible.GetCombustibleProperties(world, stack, null);
                ItemStack? smeltsTo = props?.SmeltedStack?.ResolvedItemstack;
                if (props == null || smeltsTo == null || !IsIronOreOutput(smeltsTo)) return null;

                charge.Nuggets += stack.StackSize;
                // Same unit math as vanilla's GetSingleSmeltableStack: 20 nuggets -> 1 bloom -> 100 units.
                charge.BaseUnits += stack.StackSize * (double)props.SmeltedStack.StackSize / props.SmeltedRatio * 100.0;
            }

            if (charge.Nuggets == 0) return null;

            float boraxPerNugget = (float)charge.Borax / charge.Nuggets;
            charge.Recovery = GetRecovery(boraxPerNugget);
            // Integer math: in floats 100 * 0.9f floors to 89, and 23 nuggets come to 114.999... units.
            int recoveryPercent = (int)Math.Round(charge.Recovery * 100f);
            charge.Units = (int)Math.Round(charge.BaseUnits) * recoveryPercent / 100;
            charge.Threshold = boraxPerNugget >= Config.BoraxDiscountMinRatio
                ? Config.RefineThresholdWithBorax
                : Config.RefineThreshold;
            return charge;
        }

        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            IronCharge? charge = GetIronCharge(world, cookingSlotsProvider);
            if (charge != null) return charge.Threshold;
            return base.GetMeltingPoint(world, cookingSlotsProvider, inputSlot);
        }

        // Duration needs no override: vanilla sums each ingredient's melting time and
        // skips items without combustible props, which includes borax.

        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            IronCharge? charge = GetIronCharge(world, cookingSlotsProvider);
            if (charge != null) return charge.Units > 0 && ResolveIronOutput(world) != null;

            if (IsBlockedFerrousRemelt(world, cookingSlotsProvider)) return false;

            return base.CanSmelt(world, cookingSlotsProvider, inputStack, outputStack);
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            IronCharge? charge = GetIronCharge(world, cookingSlotsProvider);
            ItemStack? iron = charge != null ? ResolveIronOutput(world) : null;
            var smeltedBlock = world.GetBlock(CodeWithVariant("type", "smelted")) as BlockSmeltedContainer;
            if (charge == null || iron == null || smeltedBlock == null)
            {
                base.DoSmelt(world, cookingSlotsProvider, inputSlot, outputSlot);
                return;
            }

            ItemStack[] ingredients = GetIngredients(world, cookingSlotsProvider);
            float temperature = GetIngredientsTemperature(world, ingredients);

            // Cooking progress only grows while the charge is at its threshold, so a fair smelt
            // always finishes hot enough. It can finish too cold only if the contents changed
            // mid-smelt so the cook time shrank below the progress already made, e.g. pulling
            // the borax (threshold 1400 -> 1500) together with some nuggets. Refuse that smelt;
            // the fire pit then resets cooking progress to zero and nothing is consumed.
            // (Not checked in CanSmelt: there, false would also stop the fire pit heating the crucible.)
            if (temperature + 1f < charge.Threshold) return;

            // Mirrors vanilla DoSmelt: swap the fired crucible for a smelted one holding the metal.
            var smeltedStack = new ItemStack(smeltedBlock, 1);
            smeltedBlock.SetContents(smeltedStack, iron, charge.Units);
            smeltedStack.Collectible.SetTemperature(world, smeltedStack, temperature, true);

            outputSlot.Itemstack = smeltedStack;
            inputSlot.Itemstack = null;
            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                slot.Itemstack = null; // borax is used up along with the ore
            }
        }

        // Replacement for the fire pit window's output line. Returns false to keep vanilla's text.
        public bool TryGetOutputText(IWorldAccessor world, ISlotProvider cookingSlotsProvider, out string? text)
        {
            IronCharge? charge = GetIronCharge(world, cookingSlotsProvider);
            if (charge != null)
            {
                text = Lang.Get("ironpit:crucible-iron-output",
                    charge.Units, (int)Math.Round(charge.Recovery * 100), charge.Threshold);
                return true;
            }

            if (IsBlockedFerrousRemelt(world, cookingSlotsProvider))
            {
                text = Lang.Get("ironpit:crucible-ferrous-blocked");
                return true;
            }

            text = null;
            return false;
        }

        float GetRecovery(float boraxPerNugget)
        {
            float recovery = 0;
            foreach (RecoveryTier tier in Config.RecoveryTiers ?? Array.Empty<RecoveryTier>())
            {
                if (boraxPerNugget >= tier.MinBoraxPerNugget) recovery = Math.Max(recovery, tier.Recovery);
            }
            return Math.Clamp(recovery, 0f, 1f);
        }

        // True if vanilla would smelt these contents into a ferrous metal we do not allow remelting.
        bool IsBlockedFerrousRemelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider)
        {
            if (!Config.BlockFerrousRemelt) return false;

            // Empty cooking slots come back as null entries.
            ItemStack[] ingredients = GetIngredients(world, cookingSlotsProvider);
            ItemStack[] present = ingredients.Where(stack => stack != null).ToArray();
            if (present.Length > 0 && present.All(IsAllowedFerrousRemeltIngredient)) return false;

            ItemStack? output = GetMatchingAlloy(world, ingredients)?.Output?.ResolvedItemstack
                ?? GetSingleSmeltableStack(ingredients)?.output;
            if (output == null) return false;

            string? metal = output.Collectible.Variant?["metal"];
            return metal != null && Config.FerrousMetals.Contains(metal);
        }

        static bool IsAllowedFerrousRemeltIngredient(ItemStack stack)
        {
            return Config.AllowFerrousRemeltItemCodes.Any(code => stack.Collectible.Code.Equals(new AssetLocation(code)));
        }

        static bool IsFlux(ItemStack stack)
        {
            return Config.FluxItemCodes.Any(code => stack.Collectible.Code.Equals(new AssetLocation(code)));
        }

        static bool IsIronOreOutput(ItemStack smeltsTo)
        {
            return Config.IronOreSmeltsTo.Any(code => smeltsTo.Collectible.Code.Equals(new AssetLocation(code)));
        }

        static ItemStack? ResolveIronOutput(IWorldAccessor world)
        {
            Item? item = world.GetItem(new AssetLocation(Config.IronOutputCode));
            return item == null ? null : new ItemStack(item, 1);
        }
    }
}
