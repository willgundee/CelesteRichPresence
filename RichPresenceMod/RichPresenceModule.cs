using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Celeste.Mod.RichPresence
{
    public class DiscordRpc
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReadyCallback();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DisconnectedCallback(int errorCode, string message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ErrorCallback(int errorCode, string message);

        public struct EventHandlers
        {
            public ReadyCallback readyCallback;
            public DisconnectedCallback disconnectedCallback;
            public ErrorCallback errorCallback;
        }

        // Values explanation and example: https://discordapp.com/developers/docs/rich-presence/how-to#updating-presence-update-presence-payload-fields
        [System.Serializable]
        public struct RichPresence
        {
            public string state; /* max 128 bytes */
            public string details; /* max 128 bytes */
            public long startTimestamp;
            public long endTimestamp;
            public string largeImageKey; /* max 32 bytes */
            public string largeImageText; /* max 128 bytes */
            public string smallImageKey; /* max 32 bytes */
            public string smallImageText; /* max 128 bytes */
            public string partyId; /* max 128 bytes */
            public int partySize;
            public int partyMax;
            public string matchSecret; /* max 128 bytes */
            public string joinSecret; /* max 128 bytes */
            public string spectateSecret; /* max 128 bytes */
            public bool instance;
        }

        [DllImport("discord-rpc-w32.dll", EntryPoint = "Discord_Initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Initialize(string applicationId, ref EventHandlers handlers, bool autoRegister, string optionalSteamId);

        [DllImport("discord-rpc-w32.dll", EntryPoint = "Discord_UpdatePresence", CallingConvention = CallingConvention.Cdecl)]
        public static extern void UpdatePresence(ref RichPresence presence);

        [DllImport("discord-rpc-w32.dll", EntryPoint = "Discord_RunCallbacks", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RunCallbacks();

        [DllImport("discord-rpc-w32.dll", EntryPoint = "Discord_Shutdown", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Shutdown();
    }
    public class RichPresenceModule : EverestModule
    {
        public static RichPresenceModule Instance;

        public override Type SettingsType => typeof(RichPresenceModuleSettings);
        public static RichPresenceModuleSettings Settings => (RichPresenceModuleSettings)Instance._Settings;

        // The methods we want to hook.
        private readonly static MethodInfo m_Update = typeof(Player).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static DiscordRpc.RichPresence presence;
        private static int sessionDeathCounter = 0;
        private static int lastDeathValue = 0;


        public RichPresenceModule()
        {
            Instance = this;
            DiscordRpc.EventHandlers handlers = new DiscordRpc.EventHandlers();
            //I should probably put some handler here but i dont know how yet
            //handlers.readyCallback = ReadyCallback;
            //handlers.disconnectedCallback += DisconnectedCallback;
            //handlers.errorCallback += ErrorCallback;
            DiscordRpc.Initialize("410142275738402817", ref handlers, true, null);
        }

        public override void Load()
        {
            // Runtime hooks are quite different from static patches.
            Type t_RichPresenceModule = GetType();
            // [trampoline] = [method we want to hook] .Detour< [signature] >( [replacement method] );
            orig_Update = m_Update.Detour<d_Update>(t_RichPresenceModule.GetMethod("Update"));
        }

        public override void Unload()
        {
            DiscordRpc.Shutdown();
            // Let's just hope that nothing else detoured this, as this is depth-based...
            RuntimeDetour.Undetour(m_Update);
        }

        public delegate void d_Update(Player self);
        public static d_Update orig_Update;
        public static void Update(Player self)
        {
            orig_Update(self);

            if (Settings.Enabled)
            {
                try
                {
                    int amount = self.GetLevel().GetCurrentDeathCount();
                    if (amount == 0)
                    {
                        lastDeathValue = 0;
                    }
                    else if (amount != lastDeathValue)
                    {
                        sessionDeathCounter++;
                        presence.details = "Has died " + sessionDeathCounter.ToString() + " times this session";
                        DiscordRpc.UpdatePresence(ref presence);
                        lastDeathValue = amount;
                    }
                }
                catch (Exception e)
                {
                    presence.details = e.Message;
                    DiscordRpc.UpdatePresence(ref presence);
                }
            }
        }

    }
}
