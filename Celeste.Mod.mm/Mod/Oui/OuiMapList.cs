﻿using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class OuiMapList : Oui {

        private TextMenu menu;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private int type = 1;
        private int side = 0;

        private List<TextMenuExt.IItemExt> items = new List<TextMenuExt.IItemExt>();

        public OuiMapList() {
        }
        
        public TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            menu = new TextMenu();
            items.Clear();

            menu.Add(new TextMenu.Header(Dialog.Clean("maplist_title")));

            if (menu.Height > menu.ScrollableMinSize) {
                menu.Position.Y = menu.ScrollTargetY;
            }

            menu.Add(new TextMenu.SubHeader(Dialog.Clean("maplist_filters")));

            // TODO: List level set types!
            menu.Add(new TextMenu.Slider(Dialog.Clean("maplist_type"), value => {
                if (value == 0)
                    return Dialog.Clean("levelset_celeste");
                if (value == 1)
                    return Dialog.Clean("maplist_type_allmods");
                return "";
            }, 0, 1, type).Change(value => {
                type = value;
                ReloadItems();
            }));

            menu.Add(new TextMenu.Slider(Dialog.Clean("maplist_side"), value => ((char) ('A' + value)).ToString(), 0, Enum.GetValues(typeof(AreaMode)).Length - 1, side).Change(value => {
                side = value;
                ReloadItems();
            }));

            menu.Add(new TextMenu.SubHeader(Dialog.Clean("maplist_list")));

            ReloadItems();

            return menu;
        }

        private void ReloadItems() {
            foreach (TextMenu.Item item in items)
                menu.Remove(item);
            items.Clear();

            int min = 0;
            int max = AreaData.Areas.Count;
            if (type == 0) {
                max = 10;
            } else {
                min = 10;
            }

            if (type >= 2) {
                // TODO: Filter by levelset!
            }

            string levelSet = null;
            string name;

            for (int i = min; i < max; i++) {
                AreaData area = AreaData.Areas[i];
                if (!area.HasMode((AreaMode) side))
                    continue;

                if (levelSet != area.GetLevelSet()) {
                    levelSet = area.GetLevelSet();
                    if (levelSet != "Celeste") {
                        if (string.IsNullOrEmpty(levelSet)) {
                            name = Dialog.Clean("levelset_");
                        } else {
                            name = levelSet;
                            name = ("levelset_" + name).DialogCleanOrNull() ?? name.DialogCleanOrNull() ?? name.SpacedPascalCase();
                        }
                        TextMenuExt.SubHeaderExt levelSetHeader = new TextMenuExt.SubHeaderExt(name);
                        levelSetHeader.Alpha = 0f;
                        menu.Add(levelSetHeader);
                        items.Add(levelSetHeader);
                    }
                }

                name = area.Name;
                name = name.DialogCleanOrNull() ?? name.SpacedPascalCase();

                TextMenuExt.ButtonExt button = new TextMenuExt.ButtonExt(name);
                button.Alpha = 0f;

                if (area.Icon != "areas/null")
                    button.Icon = area.Icon;
                button.IconWidth = 128f;

                if (i < 10 && i > SaveData.Instance.UnlockedAreas)
                    button.Disabled = true;
                if (side == 1 && !SaveData.Instance.Areas[i].Cassette)
                    button.Disabled = true;
                if (side >= 2 && SaveData.Instance.UnlockedModes < (side + 1))
                    button.Disabled = true;

                menu.Add(button.Pressed(() => {
                    Inspect(area, (AreaMode) side);
                }));
                items.Add(button);
            }

            // Do this afterwards as the menu has now properly updated its size.
            for (int i = 0; i < items.Count; i++)
                Add(new Coroutine(FadeIn(i, items[i])));
        }

        private IEnumerator FadeIn(int i, TextMenuExt.IItemExt item) {
            yield return 0.03f * i;
            float ease = 0f;

            Vector2 offset = item.Offset;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                ease = Ease.CubeOut(p);
                item.Alpha = ease;
                item.Offset = offset + new Vector2(0f, 64f * (1f - ease));
                yield return null;
            }

            item.Alpha = 1f;
            item.Offset = offset;
        }

        private void ReloadMenu() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (menu != null) {
                position = menu.Position;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = CreateMenu(false, null);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }

            Scene.Add(menu);
        }

        public override IEnumerator Enter(Oui from) {
            ReloadMenu();

            menu.Visible = (Visible = true);
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
            yield break;
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play("event:/ui/main/whoosh_large_out");
            Overworld.Maddy.Show = true;
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;
            yield break;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play("event:/ui/main/button_back");
                Overworld.Goto<OuiChapterSelect>();
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            base.Render();
        }

        public void Inspect(AreaData area, AreaMode mode = AreaMode.Normal) {
            Focused = false;
            Audio.Play("event:/ui/world_map/icon/select");
            SaveData.Instance.LastArea = area.ToKey(mode);
            Overworld.Mountain.Model.EaseState(area.MountainState);
            Overworld.Goto<OuiChapterPanel>();
        }

        public void Start(AreaData area, AreaMode mode = AreaMode.Normal, string checkpoint = null) {
            Focused = false;
            Audio.Play("event:/ui/world_map/chapter/checkpoint_start");
            Add(new Coroutine(StartRoutine(area, mode, checkpoint)));
        }

        private IEnumerator StartRoutine(AreaData area, AreaMode mode = AreaMode.Normal, string checkpoint = null) {
            Overworld.Maddy.Hide(false);
            area.Wipe(Overworld, false, null);
            Audio.SetMusic(null, true, true);
            Audio.SetAmbience(null, true);
            if ((area.ID == 0 || area.ID == 9) && checkpoint == null && mode == AreaMode.Normal) {
                Overworld.RendererList.UpdateLists();
                Overworld.RendererList.MoveToFront(Overworld.Snow);
            }
            yield return 0.5f;
            LevelEnter.Go(new Session(area.ToKey(mode), checkpoint), false);
            yield break;
        }

    }
}
