using System;
using HarmonyLib;
using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronPit
{
    // Entry point. The game finds this class automatically (it derives from ModSystem).
    public class IronPitMod : ModSystem
    {
        const string ConfigFile = "ironpit.json";
        const string HarmonyId = "ironpit";
        const string ChannelName = "ironpit";

        // The server's values. On a remote client this holds local defaults until the
        // server's copy arrives (it is only used for display there).
        public static IronPitConfig Config { get; private set; } = new IronPitConfig();

        Harmony? harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            // Names used by the JSON patches in assets/ironpit/patches/.
            api.RegisterBlockEntityBehaviorClass("IronPitFirepitBellows", typeof(BEBehaviorFirepitBellows));
            api.RegisterBlockClass("IronPitCrucible", typeof(BlockIronPitCrucible));

            api.Network.RegisterChannel(ChannelName).RegisterMessageType<ConfigPacket>();

            // In single player, Start runs once per side in the same process; patch only once.
            if (!Harmony.HasAnyPatches(HarmonyId))
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(typeof(IronPitMod).Assembly);
            }

            api.Logger.Notification("[IronPit] loaded. Side: {0}", api.Side);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            LoadConfig(sapi);

            // The fire pit window and block info are drawn on the client, so send it the server's numbers.
            IServerNetworkChannel channel = sapi.Network.GetChannel(ChannelName);
            sapi.Event.PlayerJoin += player =>
                channel.SendPacket(new ConfigPacket { Json = JsonConvert.SerializeObject(Config) }, player);
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            capi.Network.GetChannel(ChannelName).SetMessageHandler<ConfigPacket>(packet =>
            {
                IronPitConfig? received = packet.Json == null ? null : JsonConvert.DeserializeObject<IronPitConfig>(packet.Json);
                if (received != null) Config = received;
            });
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            base.Dispose();
        }

        static void LoadConfig(ICoreAPI api)
        {
            try
            {
                Config = api.LoadModConfig<IronPitConfig>(ConfigFile) ?? new IronPitConfig();
                // Write it back so new fields show up in the player's file after an update.
                api.StoreModConfig(Config, ConfigFile);
            }
            catch (Exception e)
            {
                api.Logger.Error("[IronPit] could not read ModConfig/{0}, using defaults: {1}", ConfigFile, e.Message);
                Config = new IronPitConfig();
            }
        }
    }

    // Carries the server config as JSON, so new config fields need no packet changes.
    [ProtoContract]
    public class ConfigPacket
    {
        [ProtoMember(1)]
        public string? Json;
    }
}
