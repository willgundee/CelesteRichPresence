﻿using FMOD.Studio;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    public abstract class EverestModule {

        /// <summary>
        /// Used by Everest itself to store any module metadata.
        /// 
        /// The metadata is usually parsed from meta.yaml in the archive.
        /// 
        /// You can override this property to provide dynamic metadata at runtime.
        /// Note that this doesn't affect mod loading.
        /// </summary>
        public virtual EverestModuleMetadata Metadata { get; set; }

        /// <summary>
        /// The type used for the settings object. Used for serialization, among other things.
        /// </summary>
        public abstract Type SettingsType { get; }
        /// <summary>
        /// Any settings stored across runs. Everest loads this before Load gets invoked.
        /// Define your custom property returning _Settings typecasted as your custom settings type.
        /// </summary>
        public virtual EverestModuleSettings _Settings { get; set; }

        /// <summary>
        /// Load the mod settings. Loads the settings from {Everest.PathSettings}/{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void LoadSettings() {
            string path = Path.Combine(Everest.PathSettings, Metadata.Name + ".yaml");
            if (!File.Exists(path))
                return;
            using (Stream stream = File.OpenRead(path))
            using (StreamReader reader = new StreamReader(path))
                _Settings = (EverestModuleSettings) YamlHelper.Deserializer.Deserialize(reader, SettingsType);
        }

        /// <summary>
        /// Save the mod settings. Saves the settings to {Everest.PathSettings}/{Metadata.Name}.yaml by default.
        /// </summary>
        public virtual void SaveSettings() {
            string path = Path.Combine(Everest.PathSettings, Metadata.Name + ".yaml");
            if (File.Exists(path))
                File.Delete(path);
            using (Stream stream = File.OpenWrite(path))
            using (StreamWriter writer = new StreamWriter(stream))
                YamlHelper.Serializer.Serialize(writer, _Settings, SettingsType);
        }

        /// <summary>
        /// Perform any initializing actions after all modd have been loaded.
        /// Do not depend on any specific order in which the mods get initialized.
        /// </summary>
        public abstract void Load();

        /// <summary>
        /// Unload any unmanaged resources allocated by the mod (f.e. textures) and
        /// undo any changes performed by the mod.
        /// </summary>
        public abstract void Unload();

        private Type _PrevSettingsType;
        private PropertyInfo[] _PrevSettingsProps;
        /// <summary>
        /// Create the mod menu subsection including the section header in the given menu.
        /// The default implementation uses reflection to attempt creating a menu.
        /// </summary>
        /// <param name="menu">Menu to add the section to.</param>
        /// <param name="inGame">Whether we're in-game (paused) or in the main menu.</param>
        /// <param name="snapshot">The Level.PauseSnapshot</param>
        public virtual void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            Type type = SettingsType;
            EverestModuleSettings settings = _Settings;
            if (type == null || settings == null)
                return;

            // The default name prefix.
            string nameDefaultPrefix = $"modoptions_{type.Name.ToLowerInvariant()}_";
            if (nameDefaultPrefix.EndsWith("Settings"))
                nameDefaultPrefix = nameDefaultPrefix.Substring(0, nameDefaultPrefix.Length - 8);

            // Any attributes we may want to get and read from later.
            SettingInGameAttribute attribInGame;
            SettingRangeAttribute attribRange;

            // If the settings type has got the InGame attrib, only show it in the matching situation.
            if ((attribInGame = type.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                attribInGame.InGame != inGame)
                return;

            // The settings subheader.
            string name; // We lazily reuse this field for the props later on.
            name = type.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}title";
            if (Dialog.Has(name))
                name = Dialog.Clean(name);
            else
                name = Metadata.Name.SpacedPascalCase();

            menu.Add(new TextMenu.SubHeader(name));

            PropertyInfo[] props;
            if (type == _PrevSettingsType) {
                props = _PrevSettingsProps;
            } else {
                _PrevSettingsProps = props = type.GetProperties();
                _PrevSettingsType = type;
            }

            foreach (PropertyInfo prop in props) {
                if ((attribInGame = prop.GetCustomAttribute<SettingInGameAttribute>()) != null &&
                    attribInGame.InGame != inGame)
                    continue;

                if (prop.GetCustomAttribute<SettingIgnoreAttribute>() != null)
                    continue;

                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                name = prop.GetCustomAttribute<SettingNameAttribute>()?.Name ?? $"{nameDefaultPrefix}{prop.Name.ToLowerInvariant()}";
                if (Dialog.Has(name))
                    name = Dialog.Clean(name);
                else
                    name = prop.Name.SpacedPascalCase();

                bool needsRelaunch = prop.GetCustomAttribute<SettingNeedsRelaunchAttribute>() != null;

                TextMenu.Item item = null;
                Type propType = prop.PropertyType;
                object value = prop.GetValue(settings);

                // Create the matching item based off of the type and attributes.

                if (propType == typeof(bool)) {
                    item =
                        new TextMenu.OnOff(name, (bool) value)
                        .Change(v => prop.SetValue(settings, v))
                        .NeedsRelaunch(needsRelaunch)
                    ;

                } else if (
                    propType == typeof(int) &&
                    (attribRange = prop.GetCustomAttribute<SettingRangeAttribute>()) != null
                ) {
                    item =
                        new TextMenu.Slider(name, i => i.ToString(), attribRange.Min, attribRange.Max, (int) value)
                        .Change(v => prop.SetValue(settings, v))
                        .NeedsRelaunch(needsRelaunch)
                    ;
                }

                if (item == null)
                    continue;
                menu.Add(item);
            }

        }

    }
}
