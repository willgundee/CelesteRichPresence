﻿using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.RichPresence
{
    [SettingName("RichPresence-deline")] // We're lazy.
    public class RichPresenceModuleSettings : EverestModuleSettings {

        public bool Enabled { get; set; } = false;

    }
}
