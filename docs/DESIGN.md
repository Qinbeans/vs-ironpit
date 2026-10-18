# Iron Pit: Design Spec (v0.1 draft)

Smelt iron in a bellows-driven fire pit using a crucible, without tearing down a bloomery each run.

Values marked **[VERIFY]** came from the design conversation or from third-party pages, not from the game files. Check them against your install before relying on them.

## 1. Core idea

- Put a crucible with iron ore/nuggets in a fire pit.
- Burn charcoal, coke, or coal. Attach bellows to push the pit hotter and burn fuel faster.
- Outcome depends on temperature and how long it is held:
  - Base heat gives **pig iron** (high carbon, brittle, melts lower).
  - Sustained high heat gives **iron** (refined).

## 2. Fuel ladder (max temperature without bellows)

Verified in 1.22.7 `combustibleProps.burnTemperature`: lignite (brown coal) 1100, bituminous (black) coal 1200, anthracite 1200 (burns 196 s vs 84 s), charcoal 1300, coke 1340.

| Fuel       | Max temp | Result         |
|------------|----------|----------------|
| Brown coal | ~1,100 C | Pilot heating only; cannot reach iron even with bellows |
| Black coal | ~1,200 C | Pig iron       |
| Charcoal   | ~1,300 C | Pig iron       |
| Coke       | ~1,340 C | Pig iron       |

Brown coal's role: cheap preheat of the pit and crucible before adding a better fuel. Whether the pit allows swapping or layering fuel mid-burn is unknown. [VERIFY]

## 3. Bellows

- Attach to the fire pit. Push temperature past the fuel's normal cap, up to a ceiling.
- Raise fuel burn rate while blowing.
- Proposed: flat bonus with a ceiling, so brown coal can never reach the iron threshold.
- v1: hand-operated. Mechanical power (windmill/water) is a later feature.

### Implemented (v0.1, untested in game)

Place a bellows facing the fire pit, as for a forge. Values are in `ModConfig/ironpit.json`:

| Setting | Default | Effect |
|---------|---------|--------|
| `BellowsTempBonus` | 250 | Degrees added at full air, scaled by air level |
| `BellowsTempCeiling` | 1600 | Bellows can never push past this |
| `AirDecayPerSecond` | 0.1 | Each pump adds 0.2 air, so one pump per 2 s holds steady; 5 quick pumps = full |
| `BellowsBurnRateMultiplier` | 2 | Fuel burns 2× as fast at full air |

Resulting peaks at full air: brown coal 1350 (below both refine thresholds, as intended), black coal 1450 (refines only with borax), charcoal 1550, coke 1590.

## 4. Refining thresholds

| Setup                          | Threshold to refine |
|--------------------------------|---------------------|
| Bellows, no borax              | 1,500 C             |
| Bellows + borax (at start)     | 1,400 C             |

Real pure iron melts at about 1,538 C, so these are game abstractions. [VERIFY the game's own iron melt value]

## 5. The 2/3 rule

- Track `timeAboveThreshold` and `totalSmeltTime` on the crucible.
- At the end of the smelt:
  - `timeAboveThreshold / totalSmeltTime >= 2/3` gives **iron**.
  - Otherwise, if the pig iron temperature was reached, gives **pig iron**.
- Counters must be saved with the crucible so a chunk reload does not wipe progress.
- Open: what counts as "heating time" (full fixed duration vs. from first ignition). Show the ratio to the player somehow (tooltip or glow).

## 6. Borax (flux)

- Added at the start of the smelt, not mid-run.
- Lowers the refine threshold by 100 C (1,500 to 1,400), so fewer bellows pumps are needed to stay in range.
- Improves **recovery** of iron that would otherwise be lost to slag (borax cannot create metal).
- Vanilla uses of `powder-borax` (verified 1.22.7): diluted borax for hide soaking and taxidermy, white dye, the stone coffin lid recipe, and **anvil welding flux** for iron, meteoric iron and steel. So borax is already a smithing flux; this mod adds a smelting use.

### Ratio and recovery (nuggets : borax) [tunable config]

5 units per nugget and 100 units per ingot (verified: nugget smeltedRatio 20, ingot mold requires 100).

| Ratio     | Borax per 20 nuggets | Recovery |
|-----------|----------------------|----------|
| No borax  | 0                    | 80%      |
| 10:1      | 2                    | 90%      |
| 5:1       | 4                    | 100%     |

Cap at 100% so the crucible never beats the bloomery's 1 bloom per 20 nuggets. Whether pig iron also uses recovery is left out of v1.

### Implemented (v0.1, untested in game)

- `patches/borax-in-crucible.json` gives `powder-borax` the Metallurgy storage flag and a nugget-sized size, so it fits crucible slots.
- `BlockIronPitCrucible` replaces the fired crucible's class (`patches/crucible-class.json`). A crucible holding only iron ore nuggets (anything vanilla would smelt to `ironbloom`), plus optional borax, is an **iron charge**:
  - melts at `RefineThreshold` (1500), or `RefineThresholdWithBorax` (1400) when borax per nugget ≥ `BoraxDiscountMinRatio` (0.1, the 10:1 tier, so a pinch of borax doesn't unlock it)
  - becomes molten `ingot-iron`, units = floor(5 × nuggets × recovery); recovery tiers are in config
  - borax is consumed with the ore
- The fire pit window shows "Will create N units of Iron (R% recovery). Melts at T°C" (Harmony patch, because vanilla's method is not virtual).
- Held heat matters already: vanilla drains cooking progress quickly whenever the charge dips below its melting point, so letting the bellows lapse costs progress. The real 2/3 rule is still milestone 4.
- **Borax "at the start" is not enforced.** Slots stay editable mid-burn. Adding cold borax drops the whole charge to the borax's temperature (vanilla takes the minimum across stacks), which is a natural penalty.
- **Per-batch trade-off:** borax takes a crucible slot, so a full no-borax crucible (512 nuggets → 2048 units) gives more metal per batch than 5:1 (320 nuggets + 64 borax → 1600 units). Borax wins per nugget, which is the scarce input.
- **Pouring:** molten iron solidifies below 0.9 × 1482 = 1334°C (vanilla rule). A crucible smelted at 1400-1500°C gives roughly 70-85 s to pour. The output slot does not keep it hot; reheat it in the input slot if it sets.

### Side effect of bellows: ferrous remelting

At 1550°C+ a vanilla crucible could remelt finished iron (ingots, bits, plates…), steel (1502) and meteoric iron (1476), then cast them in tool molds, which vanilla never allows. `BlockFerrousRemelt` (default true) refuses those charges, except `ingot-meteoriciron`, `metalbit-meteoriciron`, and `metalbit-iron`. This allows meteoric iron melting and reclamation of iron bits while keeping finished iron ingots, plates and steel blocked. Pig iron remains unimplemented. **Open question:** should refined iron from ore be castable in tool molds, or only ingot molds?

## 7. Outputs

- **Pig iron**: new item/ingot. Hard but brittle. Can be refined later (e.g. re-smelt, or a decarburizing step). Details open.
- **Iron**: standard iron ingots, so it plugs into existing recipes.
- **Slag**: optional byproduct. Open.

## 8. What is data vs. code

- **Data (JSON assets/patches)**: new items (pig iron, slag), recipes, lang strings, fuel temperature tweaks.
- **Code (C#)**: letting a fire pit accept bellows, raising its temperature cap, faster burn rate, per-crucible time tracking, the 2/3 outcome logic.

## 9. Answers from the game files (verified against 1.22.7)

Sources: `assets/survival/...` in the game install, and the game DLLs decompiled with `ilspycmd`.

1. **Bellows on a fire pit: not in vanilla, but the hook exists.** Bellows (`BlockEntityBellows`) call `IBellowsAirReceiver.BlowAirInto(world, pos, 0.2, facing)` on the block they face, ~0.73 s after each pump (at most one pump per ~1.33 s). Only `BlockEntityForge` implements it. `Block.GetInterface<T>` also checks block entity behaviors, so a behavior on the fire pit can receive air. **Built:** `BEBehaviorFirepitBellows`, added by `patches/firepit-bellows.json`.
2. **Fire pit temperature:** `BlockEntityFirepit.furnaceTemperature` moves toward the public `maxTemperature` field, which is set to `fuel burnTemperature × HeatModifier (1.0)` each time a new fuel item ignites. No hard cap beyond that. `fuelBurnTime` is also public, so burn rate can be changed from outside.
3. **Crucible:** `BlockSmeltingContainer`. It smelts when the input temperature reaches the highest `meltingPoint` among its contents, for `meltingDuration × stack / smeltedRatio` seconds of cooking. Output is a `crucible-*-smelted` holding `smeltedStack` × units. The crucible JSON has `maxHeatableTemp: 1200`, but **no game code reads it**, so the real limit is the fire pit.
4. **Iron:** metal `iron` has `meltPoint: 1538` (`worldproperties/block/metal.json`). Iron-bearing nuggets (hematite, limonite, magnetite) have `meltingPoint: 1482`, `meltingDuration: 30`, `smeltedRatio: 20`, `smeltedStack: ironbloom`. So 20 nuggets = 1 bloom, matching the 5 units/nugget assumption.
   - Consequence: a vanilla crucible of iron nuggets that reaches 1482 C would fill with molten *ironbloom*. The mod must replace that outcome (pig iron / iron ingot) rather than let vanilla smelt it.
5. **Borax:** `powder-borax`, 2 per `ore-borax` ground in a quern, stacks to 64. Tagged `flux`; used for anvil welding, diluted borax, white dye and the stone coffin lid. Vanilla cannot put it in a crucible: its storage flags (General only) and default size (0.5) both fail the crucible slot checks.
6. **Fuel layering:** the fire pit holds one fuel stack and ignites the next item when the current one burns out, so swapping fuel types mid-run works by changing the stack in the fuel slot. Each new item resets `maxTemperature` to that fuel's value.

## 10. Milestones

1. Toolchain: build, load in game, see the log line. **Builds against 1.22.7.**
2. Pig iron item and a crucible recipe at base heat. (Moved after borax, by decision on 2026-10-13: pig iron needs more design work.)
3. Bellows attach to fire pit; temperature rises. **Code written, needs in-game test.** (Done before 2, because 2 depends on overriding the vanilla crucible outcome; see section 9.4.)
4. Time-above-threshold tracking and the 2/3 outcome.
5. Borax discount and recovery table, plus a basic refine step (iron nuggets → molten iron at the threshold). **Code written, needs in-game test.** See section 6.
6. Config file, balance pass, in-game feedback. (Config covers bellows, thresholds, recovery tiers; the server sends its config to clients so the fire pit window shows server values.)
