namespace IronPit
{
    // Tunable values. The game writes this to ModConfig/ironpit.json on first run;
    // edit that file to rebalance without recompiling. See DESIGN.md for the reasoning.
    public class IronPitConfig
    {
        // Bumped when a default changes in a way existing config files should pick up;
        // IronPitMod.Migrate upgrades older files. Files from before this field existed
        // read as 0. Do not edit by hand.
        public const int CurrentVersion = 1;
        public int ConfigVersion = 0;

        // ---- Bellows (DESIGN.md section 3) ----

        // Degrees added to the fuel's normal max temperature at full air.
        // 250 keeps brown coal (1100 -> 1350) below the 1400 borax threshold
        // while charcoal (1300 -> 1550) and coke (1340 -> 1590) can pass 1500.
        public int BellowsTempBonus = 250;

        // Hard ceiling for a bellows-driven fire pit, whatever the fuel.
        public int BellowsTempCeiling = 1600;

        // Air level runs 0..1. Each bellows pump adds 0.2 (fixed by the game),
        // so 5 quick pumps reach full air.
        public float MaxAir = 1f;

        // Air lost per second. At 0.1, one pump every 2 seconds holds the level steady.
        public float AirDecayPerSecond = 0.1f;

        // Fuel burn speed at full air. 2 means fuel lasts half as long while fully blown.
        public float BellowsBurnRateMultiplier = 2f;

        // ---- Crucible refining (DESIGN.md sections 4 and 6) ----

        // Crucible temperature at which iron ore nuggets melt into iron.
        public int RefineThreshold = 1500;

        // Threshold when the charge has enough borax (see BoraxDiscountMinRatio).
        public int RefineThresholdWithBorax = 1400;

        // Borax items per nugget needed for the lower threshold. 0.1 = the 10:1 tier,
        // so a single pinch of borax in a full crucible does not unlock it.
        public float BoraxDiscountMinRatio = 0.1f;

        // (Lists below are arrays on purpose: the config loader appends to pre-filled Lists,
        // which would duplicate the defaults on every load.)

        // Recovery tiers, by borax items per nugget. The highest tier the charge meets wins.
        // Recovery is capped at 1.0 so the crucible never beats the bloomery (20 nuggets = 100 units).
        public RecoveryTier[] RecoveryTiers = new[]
        {
            new RecoveryTier { MinBoraxPerNugget = 0f, Recovery = 0.8f },
            new RecoveryTier { MinBoraxPerNugget = 0.1f, Recovery = 0.9f },
            new RecoveryTier { MinBoraxPerNugget = 0.2f, Recovery = 1.0f },
        };

        // Items that count as borax flux in the crucible.
        public string[] FluxItemCodes = new[] { "game:powder-borax" };

        // An ingredient is "iron ore" if vanilla would smelt it into one of these.
        public string[] IronOreSmeltsTo = new[] { "game:ironbloom" };

        // What iron ore turns into in the crucible.
        public string IronOutputCode = "game:ingot-iron";

        // Other iron sources that join an iron charge (same thresholds, borax and recovery)
        // but keep their own metal: an ingredient vanilla would smelt into one of these
        // produces that item. Meteorite stone smelts to meteoric iron ingots.
        public string[] IronChargeOwnMetalSmeltsTo = new[] { "game:ingot-meteoriciron" };

        // Bellows make the fire pit hot enough to remelt finished iron, steel and meteoric
        // iron in a crucible, which vanilla never allows (and molten iron casts in tool molds).
        // While true, crucibles refuse those except AllowFerrousRemeltItemCodes. Iron ore
        // nuggets still refine as above.
        public bool BlockFerrousRemelt = true;

        // Metals (the "metal" variant of the smelted output, e.g. ingot-steel) blocked by BlockFerrousRemelt.
        public string[] FerrousMetals = new[] { "iron", "meteoriciron", "steel" };

        // Ferrous items that may still melt in the crucible. Meteoritic iron is useful as a
        // cast metal (raw meteorite stone included), while iron bits are reclaimable smithing scrap.
        public string[] AllowFerrousRemeltItemCodes = new[] { "game:stone-meteorite-iron", "game:ingot-meteoriciron", "game:metalbit-meteoriciron", "game:metalbit-iron" };
    }

    public class RecoveryTier
    {
        public float MinBoraxPerNugget;
        public float Recovery;
    }
}
