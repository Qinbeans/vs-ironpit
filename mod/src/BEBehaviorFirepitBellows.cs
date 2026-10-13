using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronPit
{
    // Added to the vanilla fire pit by assets/ironpit/patches/firepit-bellows.json.
    //
    // Vanilla bellows call IBellowsAirReceiver.BlowAirInto on the block they face
    // (only the forge implements it). Block.GetInterface also checks block entity
    // behaviors, so implementing it here lets bellows work on a fire pit with no
    // changes to game code.
    //
    // The fire pit heats toward its public maxTemperature field, which it sets from
    // the fuel's burn temperature on each ignition. We remember that base value and
    // raise the field while air is being blown in.
    public class BEBehaviorFirepitBellows : BlockEntityBehavior, IBellowsAirReceiver
    {
        // 0..MaxAir. Rises with each pump, decays over time.
        float air;

        // The fuel's own max temperature, before our bonus.
        int baseMaxTemp;

        // The value we last wrote to firepit.maxTemperature. If the field no longer
        // matches, the fire pit lit new fuel and we need to re-read the base.
        int lastWrittenMaxTemp = -1;

        // fuelBurnTime at the end of our last tick. Burn time only ever falls during a burn,
        // so a rise means a new fuel item ignited. This catches the case the temperature
        // check misses: new fuel whose burn temperature equals our last boosted value.
        float lastFuelBurnTime = -1;

        // Our extra burn never takes fuel below this. The fire pit's own tick must be the one
        // that crosses zero, because that is where it goes extinct (or lights the next fuel).
        const float MinFuelLeft = 0.001f;

        BlockEntityFirepit? Firepit => Blockentity as BlockEntityFirepit;

        static IronPitConfig Config => IronPitMod.Config;

        public BEBehaviorFirepitBellows(BlockEntity blockentity) : base(blockentity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            if (api.Side == EnumAppSide.Server)
            {
                Blockentity.RegisterGameTickListener(OnServerTick, 250);
            }
        }

        public void BlowAirInto(IWorldAccessor world, BlockPos pos, float amount, BlockFacing direction)
        {
            // Bellows fire this on both sides; only the server owns the temperature.
            if (Api.Side != EnumAppSide.Server) return;

            var firepit = Firepit;
            if (firepit == null || !firepit.IsBurning) return;

            air = Math.Min(Config.MaxAir, air + amount);
        }

        void OnServerTick(float dt)
        {
            var firepit = Firepit;
            if (firepit == null) return;

            if (!firepit.IsBurning)
            {
                air = 0;
                lastWrittenMaxTemp = -1;
                lastFuelBurnTime = -1;
                return;
            }

            // New fuel ignited (or first tick after lighting): take its temperature as the base.
            if (firepit.maxTemperature != lastWrittenMaxTemp || firepit.fuelBurnTime > lastFuelBurnTime + 0.001f)
            {
                baseMaxTemp = firepit.maxTemperature;
            }

            air = Math.Max(0, air - Config.AirDecayPerSecond * dt);

            float airFraction = Config.MaxAir > 0 ? air / Config.MaxAir : 0;

            int boosted = baseMaxTemp + (int)(airFraction * Config.BellowsTempBonus);
            // The ceiling only limits the bonus; never pull a hotter fuel below its own max.
            int target = Math.Max(baseMaxTemp, Math.Min(boosted, Config.BellowsTempCeiling));

            firepit.maxTemperature = target;
            lastWrittenMaxTemp = target;

            // Extra fuel use on top of the fire pit's own 1x burn.
            if (airFraction > 0)
            {
                float extraRate = (Config.BellowsBurnRateMultiplier - 1f) * airFraction;
                firepit.fuelBurnTime = Math.Max(MinFuelLeft, firepit.fuelBurnTime - extraRate * dt);
            }

            lastFuelBurnTime = firepit.fuelBurnTime;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("ironpitAir", air);
            tree.SetInt("ironpitBaseMaxTemp", baseMaxTemp);
            tree.SetInt("ironpitLastWrittenMaxTemp", lastWrittenMaxTemp);
            tree.SetFloat("ironpitLastFuelBurnTime", lastFuelBurnTime);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            air = tree.GetFloat("ironpitAir");
            baseMaxTemp = tree.GetInt("ironpitBaseMaxTemp");
            lastWrittenMaxTemp = tree.GetInt("ironpitLastWrittenMaxTemp", -1);
            // Saves from 0.1.0 lack this key; fall back to the fire pit's own saved burn time (same tree)
            // so a boosted maxTemperature is not mistaken for a fresh fuel's base.
            lastFuelBurnTime = tree.GetFloat("ironpitLastFuelBurnTime", tree.GetFloat("fuelBurnTime", -1));
        }

        // Shown when looking at the fire pit, so you can see the bellows working.
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (air > 0.01f)
            {
                dsc.AppendLine(Lang.Get("ironpit:bellows-air", (int)Math.Round(air / Config.MaxAir * 100)));
            }
        }
    }
}
