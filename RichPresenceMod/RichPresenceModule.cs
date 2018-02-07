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
    public class DiscordRpc //this class comes from https://github.com/nostrenz/cshap-discord-rpc-demo
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
        DiscordRpc.EventHandlers handlers;

        public override Type SettingsType => typeof(RichPresenceModuleSettings);
        public static RichPresenceModuleSettings Settings => (RichPresenceModuleSettings)Instance._Settings;

        // The methods we want to hook.
        private readonly static MethodInfo m_Die = typeof(Player).GetMethod("Die", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static DiscordRpc.RichPresence presence;
        private static int sessionDeathCounter = 0;



        /// <summary>
        /// Calls ReadyCallback(), DisconnectedCallback(), ErrorCallback().
        /// </summary>

        public RichPresenceModule()
        {
            Instance = this;
            handlers = new DiscordRpc.EventHandlers();
            DiscordRpc.Initialize("410142275738402817", ref handlers, true, null);
            //DiscordRpc.Shutdown(); //Fix the crash but makes the mod pointless
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit); //doesnt fix the crash
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            DiscordRpc.Shutdown();//doesnt fix the crash
        }

        public override void Load()
        {
            // Runtime hooks are quite different from static patches.
            Type t_RichPresenceModule = GetType();
            // [trampoline] = [method we want to hook] .Detour< [signature] >( [replacement method] );
            orig_Die = m_Die.Detour<d_Die>(t_RichPresenceModule.GetMethod("Die"));
        }

        public override void Unload()
        {
            // Let's just hope that nothing else detoured this, as this is depth-based...
            RuntimeDetour.Undetour(m_Die);
        }

        public delegate PlayerDeadBody d_Die(Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats);
        public static d_Die orig_Die;
        public static PlayerDeadBody Die(Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
        {
            sessionDeathCounter++;
            presence.details = "Has died " + sessionDeathCounter.ToString() + " times this session";
            DiscordRpc.UpdatePresence(ref presence);
            return orig_Die(self,direction,evenIfInvincible,registerDeathInStats);
        }

    }
}
