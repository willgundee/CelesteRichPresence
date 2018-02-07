﻿#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_Player : Player {

        // We're effectively in Player, but still need to "expose" private fields to our mod.
        private bool wasDashB;
        private Level level;

        public patch_Player(Vector2 position, PlayerSpriteMode spriteMode)
            : base(position, spriteMode) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        private void CreateTrail() {
            TrailManager.Add(this, GetCurrentTrailColor(), 1f);
        }

        public Color GetCurrentTrailColor() => GetTrailColor(wasDashB);
        private Color GetTrailColor(bool wasDashB) {
            return wasDashB ? NormalHairColor : UsedHairColor;
        }

        public Level GetLevel()
        {
            return level;
        }

    }
    public static class PlayerExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static Color GetCurrentTrailColor(this Player self)
            => ((patch_Player) self).GetCurrentTrailColor();

        public static Level GetLevel(this Player self)
            => ((patch_Player)self).GetLevel();

    }
}
