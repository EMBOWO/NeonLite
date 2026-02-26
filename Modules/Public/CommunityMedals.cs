using HarmonyLib;
using I2.Loc;
using MelonLoader;
using MelonLoader.TinyJSON;
using NeonLite.Modules.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable IDE0130
namespace NeonLite.Modules
{
    [Module(10)]
    public static class CommunityMedals
    {
#pragma warning disable CS0414
        const bool priority = false;
        static bool active = false;

        // All times (*including* bronze/silver/gold/ace)
        public static Dictionary<string, long[]> medalTimes = [];

        const string filename = "communitymedals.json";
        const string URL = "https://raw.githubusercontent.com/DerelictJade/NeonLite/main/Resources/communitymedals.json";
        

        // All stamps (null for bronze/silver/gold/ace)
        public static Sprite[] Stamps { get; private set; }
        // All crystals (bronze for not done, silver+ for done, ... , modded)
        public static Sprite[] Crystals { get; private set; }
        // All medals
        public static Sprite[] Medals { get; private set; }
        // All colors (including custom ones for pre-dev)
        public static Color[] Colors { get; private set; } = [
            new Color32(0xD1, 0x66, 0x20, 0xFF),
            new Color32(0x54, 0x54, 0x54, 0xFF),
            new Color32(0xD1, 0x9C, 0x38, 0xFF),
            new Color32(0x49, 0xA6, 0x9F, 0xFF),
            new(0.420f, 0.015f, 0.043f),
            new(0.388f, 0.8f, 0.388f),
            new(0.674f, 0.313f, 0.913f),
            new(0.043f, 0.317f, 0.901f),
            new(0.976f, 0.341f, 0f), // TOPAZ COLOR
            new(1.0f, 0.333f, 0.988f) // BLOOD DIAMOND COLOR
        ];

        private static Sprite[] imageCache = new Sprite[6];
        private static Sprite[] existingCache = new Sprite[6];
        private static Dictionary<string, Sprite> embeddedImages = new Dictionary<string, Sprite>();
        private static string[] pastPaths = new string[6];

        public enum MedalEnum
        {
            Bronze,
            Silver,
            Gold,
            Ace,
            Dev,
            Emerald,
            Amethyst,
            Sapphire,
            Topaz,
            Blud
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static MedalEnum E(int i) => (MedalEnum)i;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int I(MedalEnum e) => (int)e;


        static bool assetReadyUnderlying;
        public static bool Ready
        {
            get { return assetReadyUnderlying && medalTimes.Count != 0; }
            private set
            {
                assetReadyUnderlying = value;
            }
        }

        public static event Action AssetsFinished;
        static bool fetched;
        static bool loaded;

        public static MelonPreferences_Entry<bool> setting;
        internal static MelonPreferences_Entry<bool> oldStyle;
        internal static MelonPreferences_Entry<bool> hideOld;
        internal static MelonPreferences_Entry<bool> hideLeaderboard;
        internal static MelonPreferences_Entry<bool> leaderboardSaphPlus;
        public static MelonPreferences_Entry<float> hueShift;
        internal static MelonPreferences_Entry<string> overrideURL;
        internal static MelonPreferences_Entry<Color32> topazColor;
        internal static MelonPreferences_Entry<Color32> bdColor;
        internal static MelonPreferences_Entry<string> topazImagePath;
        internal static MelonPreferences_Entry<string> bdImagePath;
        internal static MelonPreferences_Entry<string> topazStampPath;
        internal static MelonPreferences_Entry<string> bdStampPath;
        internal static MelonPreferences_Entry<string> topazCrystalPath;
        internal static MelonPreferences_Entry<string> bdCrystalPath;

        internal static MelonPreferences_Entry<bool> uploadGlobal;
        public static MelonPreferences_Entry<bool> showExtendedGlobal;

        public static Material HueShiftMat { get; private set; } = null;
        static Material defaultMat;

#if !XBOX
        const string LB_FILE = "nl_commedal";
#endif

        static void Setup()
        {
            setting = Settings.Add(Settings.h, "Medals", "comMedals", "Community Medals", "Shows new community medals past the developer red times to aim for.", true);
            hueShift = Settings.Add(Settings.h, "Medals", "hueShift", "Hue Shift", "Changes the hue of *all* medals (and related) help aid colorblind users in telling them apart.", 0f, new MelonLoader.Preferences.ValueRange<float>(0, 1));
            oldStyle = Settings.Add(Settings.h, "Medals", "oldStyle", "Stamp Style", "Display the community medals in the level info as it was pre-3.0.0.", false);
            hideOld = Settings.Add(Settings.h, "Medals", "hideOld", "Hide Times", "Hides unachieved medal times.", false);
            hideLeaderboard = Settings.Add(Settings.h, "Medals", "hideLeaderboard", "Hide Leaderboard Medals", "Unachieved medals will appear the same as your own on the leaderboards.", false);
            uploadGlobal = Settings.Add(Settings.h, "Medals", "uploadGlobal", "Upload to Global", "Whether to upload your medal data to global. This uploads all your level medals for other mods to potentially look at. Works with extended medals.", true);
            leaderboardSaphPlus = Settings.Add(Settings.h, "Medals", "leadeerboardSaphPlus", "Show Saph+ on Leaderboard", "Show medals higher than Sapphire on leaderboards", true);
            overrideURL = Settings.Add(Settings.h, "Medals", "overrideURL", "Extension URL", "Specifies additional community medals JSON URL to apply on top of the existing community medals.", "");
            showExtendedGlobal = Settings.Add(Settings.h, "Medals", "showExtendedGlobal", "Show Extended Medals on Global", "Show other people's extended medals on the global leaderboard, even if you don't have them yourself.", false);
            topazColor = Settings.Add(Settings.h, "Medals", "topazColor", "Topaz Color", "Color for Topaz times.", new Color32(249, 87, 0, 255));
            bdColor = Settings.Add(Settings.h, "Medals", "bdColor", "Blood Diamond Color", "Color for Blood Diamond times.", new Color32(187, 10, 30, 255));
            topazImagePath = Settings.Add(Settings.h, "Medals", "topazImagePath", "Custom Topaz Medal Image", "Set a custom topaz medal image by entering the path to a local image.\nMake sure to remove quotes!", "");
            bdImagePath = Settings.Add(Settings.h, "Medals", "bdImagePath", "Custom Blood Diamond Medal Image", "Set a custom blood diamond medal image by entering the path to a local image.\nMake sure to remove quotes!", "");
            topazStampPath = Settings.Add(Settings.h, "Medals", "topazStampPath", "Custom Topaz Stamp Image", "Set a custom topaz stamp image by entering the path to a local image.\nMake sure to remove quotes!", "");
            bdStampPath = Settings.Add(Settings.h, "Medals", "bdStampPath", "Custom Blood Diamond Stamp Image", "Set a custom blood diamond stamp image by entering the path to a local image.\nMake sure to remove quotes!", "");
            topazCrystalPath = Settings.Add(Settings.h, "Medals", "topazCrystalPath", "Custom Topaz Crystal Image", "Set a custom topaz crystal image by entering the path to a local image.\nMake sure to remove quotes!", "");
            bdCrystalPath = Settings.Add(Settings.h, "Medals", "bdCrystalPath", "Custom Blood Diamond Crystal Image", "Set a custom blood diamond crystal image by entering the path to a local image.\nMake sure to remove quotes!", "");

            active = setting.SetupForModule(Activate, static (_, after) => after);
            hueShift.OnEntryValueChanged.Subscribe(static (_, after) => HueShiftMat?.SetFloat("_Shift", after));
            overrideURL.OnEntryValueChanged.Subscribe(static (_, after) => RefetchMedals());

            NeonLite.OnBundleLoad += AssetsDone;
#if !XBOX
            SteamLBFiles.OnLBWrite += OnSteamLBWrite;
            SteamLBFiles.RegisterForLoad(LB_FILE, OnSteamLBRead);
#endif
        }

        static bool Load(string js)
        {
            try
            {
                var variant = JSON.Load(js) as ProxyObject;

                foreach (var pk in variant)
                {
                    var level = NeonLite.Game.GetGameData().GetLevelData(pk.Key);
                    if (level == null)
                        continue;

                    List<long> community = [.. pk.Value as ProxyArray];
                    while (community.Count < 4)
                        community.Add(long.MinValue);

                    List<long> initial = [];

                    if (!level.isSidequest)
                    {
                        initial = [
                            long.MaxValue,
                            Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeSilver()),
                            Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeGold()),
                            Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeAce()),
                            Utils.ConvertSeconds_FloatToMicroseconds(level.GetTimeDev()),
                        ];
                    }
                    else
                    {
                        initial = [
                            long.MaxValue,
                            long.MaxValue,
                            long.MaxValue,
                            long.MaxValue,
                            long.MinValue, // so it travels down and hits ace instead of dev
                        ];

                    }

                    medalTimes[pk.Key] = [
                        .. initial,
                    .. community
                    ];
                }
            }
            catch (Exception e)
            {
                medalTimes.Clear();
                NeonLite.Logger.Error("Failed to parse community medals:");
                NeonLite.Logger.Error(e);
                return false;
            }
            return true;
        }

        public static int GetMedalIndex(string level, long time = -1)
        {
            var stats = GameDataManager.GetLevelStats(level);
            if (!stats.GetCompleted())
                return -1;

            if (time == -1)
                time = stats._timeBestMicroseconds;

            var times = medalTimes[level];
            for (int i = times.Length - 1; i >= 0; i--)
            {
                if (time <= times[i])
                    return i;
            }
            return 0;
        }

        public static void RefetchMedals()
        {
            fetched = false;
            OnLevelLoad(null);
        }

        static void DownloadOGMedals()
        {
            fetched = true;
            Helpers.DownloadURL(URL, request =>
            {
                string backup = Path.Combine(Helpers.GetSaveDirectory(), "NeonLite", filename);
                Helpers.CreateDirectories(backup);
                var load = request.result == UnityEngine.Networking.UnityWebRequest.Result.Success && Load(request.downloadHandler.text);
                if (load)
                    File.WriteAllText(backup, request.downloadHandler.text);
                else if (!File.Exists(backup) || !Load(File.ReadAllText(backup)))
                {
                    NeonLite.Logger.Warning("Could not load up to date community medals. Loading the backup resource; this could be really outdated!");
                    if (!Load(Encoding.UTF8.GetString(Resources.r.communitymedals)))
                        NeonLite.Logger.Error("Failed to load community medals.");
                }
                else
                    load = true;

                fetched = load;

                if (load)
                {
                    if (overrideURL.Value != "")
                    {
                        void FetchNext(string next)
                        {
                            var split = next.Split(['\n'], 2);
                            var url = split[0].Trim();

                            Helpers.DownloadURL(url, request =>
                            {
                                var load = request.result == UnityEngine.Networking.UnityWebRequest.Result.Success && Load(request.downloadHandler.text);
                                if (!load)
                                    NeonLite.Logger.Warning($"Failed to load extended community medals from URL {url}.");

                                if (split.Length <= 1)
                                    NeonLite.Logger.Msg("Finished loading extended community medals!");
                                else
                                    FetchNext(split[1]);
                            });
                        }
                        FetchNext(overrideURL.Value);
                    }
                    else
                        NeonLite.Logger.Msg("Fetched community medals!");
                }
            });
        }

        internal static void OnLevelLoad(LevelData _)
        {

            if (!fetched)
            {
                medalTimes.Clear();

                DownloadOGMedals();
            }

            if (loaded)
                AssetsDone(NeonLite.bundle);

            UpdateMedals();
        }

        static void UpdateMedals()
        {
            Colors[8] = topazColor.Value;
            Colors[9] = bdColor.Value;

            for (int i = 0; i < 6; i++)
            {
                if (existingCache[i] == null)
                    return;
            }

            Medals[8] = LoadSprite(0, existingCache[0]);
            Medals[9] = LoadSprite(1, existingCache[1]);
            Stamps[8] = LoadSprite(2, existingCache[2]);
            Stamps[9] = LoadSprite(3, existingCache[3]);
            Crystals[8] = LoadSprite(4, existingCache[4]);
            Crystals[9] = LoadSprite(5, existingCache[5]);
        }

        static void Activate(bool activate)
        {
            OnLevelLoad(null);

            Patching.TogglePatch(activate, typeof(LevelInfo), "SetLevel", Helpers.HM(PostSetLevel).SetPriority(Priority.First), Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(MenuButtonLevel), "SetLevelData", PostSetLevelData, Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(LeaderboardScore), "SetScore", PostSetScore, Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(Game), "OnLevelWin", PreOnWin, Patching.PatchTarget.Prefix);
            Patching.TogglePatch(activate, typeof(MenuScreenResults), "SetMedal", PostSetMedal, Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(MenuScreenResults), "OnSetVisible", PostSetVisible, Patching.PatchTarget.Postfix);

            if (!activate)
            {
                foreach (var li in UnityEngine.Object.FindObjectsOfType<LevelInfo>())
                    PostSetLevel(li, null); // for !active, revert stuff -- for active, setup some small stuff
            }

            active = activate;
        }

        public static Color AdjustedColor(Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            h -= hueShift.Value;
            while (h < 0)
                h += 1;
            return Color.HSVToRGB(h, s, v);
        }
        public static void AdjustMaterial(Graphic graphic)
        {
            if (graphic.material != HueShiftMat)
                graphic.material = HueShiftMat;
        }

        private static Sprite LoadSprite(int id, Sprite existing)
        {
            string[] paths =
            {
                "NeonLite.Assets.Sprites.MedalTopaz.png",
                "NeonLite.Assets.Sprites.MedalBlud.png",
                "NeonLite.Assets.Sprites.MikeyTopaz.png",
                "NeonLite.Assets.Sprites.MikeyBlud.png",
                "NeonLite.Assets.Sprites.CrystalTopaz.png",
                "NeonLite.Assets.Sprites.CrystalBlud.png",
            };
            string[] customPaths =
            {
                topazImagePath.Value,
                bdImagePath.Value,
                topazStampPath.Value,
                bdStampPath.Value,
                topazCrystalPath.Value,
                bdCrystalPath.Value,
            };

            if (customPaths[id] == "" || !File.Exists(customPaths[id])) return LoadEmbeddedSprite(paths[id], existing);

            if (imageCache[id] == null || customPaths[id] != pastPaths[id])
            {
                byte[] fileData = File.ReadAllBytes(customPaths[id]);

                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(fileData);

                Sprite sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f)
                );

                if (imageCache[id] != null)
                {
                    UnityEngine.Object.Destroy(imageCache[id].texture);
                    UnityEngine.Object.Destroy(imageCache[id]);
                }

                imageCache[id] = sprite;
                pastPaths[id] = customPaths[id];
            }
            return imageCache[id];
        }

        private static Sprite LoadEmbeddedSprite(string resourceName, Sprite existing)
        {
            if (embeddedImages.TryGetValue(resourceName, out var cached) && cached != null)
                return cached;

            var asm = Assembly.GetExecutingAssembly();
            using Stream s = asm.GetManifestResourceStream(resourceName);
            if (s == null)
                return null;

            byte[] bytes = new byte[s.Length];
            s.Read(bytes, 0, bytes.Length);

            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                existing.pivot
            );

            embeddedImages[resourceName] = sprite;
            return sprite;
        }

        static void AssetsDone(AssetBundle bundle)
        {
            loaded = true;
            NeonLite.Logger.DebugMsg("CommunityMedals onBundleLoad");
            if (!NeonLite.activateLate)
                return;
            loaded = false;

            var gamedata = NeonLite.Game.GetGameData();

            Medals = [
                gamedata.medalSprite_Bronze,
                gamedata.medalSprite_Silver,
                gamedata.medalSprite_Gold,
                gamedata.medalSprite_Ace,
                gamedata.medalSprite_Dev,
                bundle.LoadAsset<Sprite>("Assets/Sprites/MedalEmerald.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MedalAmethyst.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MedalSapphire.png"),
                LoadSprite(0, gamedata.medalSprite_Bronze),
                LoadSprite(1, gamedata.medalSprite_Bronze),

            ];

            existingCache[0] = gamedata.medalSprite_Bronze;
            existingCache[1] = gamedata.medalSprite_Bronze;

            var levelInfo = ((MenuScreenStaging)MainMenu.Instance()._screenStaging)
                    ._leaderboardsAndLevelInfoRef
                    .levelInfoRef;
            var devStamp = levelInfo.devStamp.transform
                    .Find("MikeyStampGraphic").GetComponent<Image>().sprite;

            Stamps = [
                null,
                null,
                null,
                null,
                devStamp,
                bundle.LoadAsset<Sprite>("Assets/Sprites/MikeyEmerald.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MikeyAmethyst.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MikeySapphire.png"),
                LoadSprite(2, devStamp),
                LoadSprite(3, devStamp),
            ];

            existingCache[2] = devStamp;
            existingCache[3] = devStamp;

            Crystals = [
                levelInfo._crystalSpriteSidequestEmpty,
                levelInfo._crystalSpriteSidequestFilled,
                levelInfo._crystalSpriteSidequestFilled,
                levelInfo._crystalSpriteSidequestFilled,
                levelInfo._crystalSpriteSidequestFilled,
                bundle.LoadAsset<Sprite>("Assets/Sprites/CrystalEmerald.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/CrystalAmethyst.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/CrystalSapphire.png"),
                LoadSprite(4, levelInfo._crystalSpriteSidequestFilled),
                LoadSprite(5, levelInfo._crystalSpriteSidequestFilled),
            ];

            existingCache[4] = levelInfo._crystalSpriteSidequestFilled;
            existingCache[5] = levelInfo._crystalSpriteSidequestFilled;

            HueShiftMat = bundle.LoadAsset<Material>("Assets/Material/HueShift.mat");
            HueShiftMat.SetFloat("_Shift", hueShift.Value);

            Ready = true;
            AssetsFinished?.Invoke();
        }

        static readonly MethodInfo styleTime = Helpers.Method(typeof(LevelInfo), "StyleMedalTime");

        static void PostSetLevel(LevelInfo __instance, LevelData level)
        {
            if (!defaultMat)
                defaultMat = __instance._crystalHolderFilledImage.material;

            if (!Ready)
                return;

            Image aceImage = __instance._aceMedalBG.transform.parent.Find("Medal Icon").GetComponent<Image>();
            Image goldImage = __instance._goldMedalBG.transform.parent.Find("Medal Icon").GetComponent<Image>();
            Image silverImage = __instance._silverMedalBG.transform.parent.Find("Medal Icon").GetComponent<Image>();

            Image[] stamps = __instance.devStamp.GetComponentsInChildren<Image>();
            if (stamps.Length < 3) return;

            if (!active || level == null || !medalTimes.ContainsKey(level.levelID))
            {
                aceImage.sprite = Medals[I(MedalEnum.Ace)];
                goldImage.sprite = Medals[I(MedalEnum.Gold)];
                silverImage.sprite = Medals[I(MedalEnum.Silver)];

                stamps[1].sprite = Stamps[I(MedalEnum.Dev)];
                stamps[2].sprite = Stamps[I(MedalEnum.Dev)];

                __instance.devTime.color = Colors[I(MedalEnum.Dev)];
                DestroyNextTime(__instance);

                return;
            }


            GameData gameData = NeonLite.Game.GetGameData();
            LevelStats levelStats = gameData.GetLevelStats(level.levelID);

            if (!levelStats.GetCompleted()) return;

            AdjustMaterial(stamps[1]);
            AdjustMaterial(stamps[2]);

            AdjustMaterial(aceImage);
            AdjustMaterial(goldImage);
            AdjustMaterial(silverImage);

            AdjustMaterial(__instance._levelMedal);
            if (level.isSidequest)
                AdjustMaterial(__instance._crystalHolderFilledImage);
            else
                __instance._crystalHolderFilledImage.material = defaultMat;

            long[] communityTimes = medalTimes[level.levelID];
            int medalEarned = GetMedalIndex(level.levelID);

            if (medalEarned < I(MedalEnum.Dev) && (!level.isSidequest || !levelStats.GetCompleted() || oldStyle.Value))
            {
                aceImage.sprite = Medals[I(MedalEnum.Ace)];
                goldImage.sprite = Medals[I(MedalEnum.Gold)];
                silverImage.sprite = Medals[I(MedalEnum.Silver)];
                return;
            }

            {
                // pastsight compatibility
                int pastSight = GetMedalIndex(level.levelID, levelStats.GetTimePastSight());
                if (!level.isSidequest)
                    __instance._levelMedal.sprite = Medals[pastSight];
                else
                    __instance._crystalHolderFilledImage.sprite = Crystals[pastSight];
            }

            stamps[1].sprite = Stamps[medalEarned];
            stamps[2].sprite = Stamps[medalEarned];

            if (oldStyle.Value)
            {
                aceImage.sprite = Medals[I(MedalEnum.Ace)];
                goldImage.sprite = Medals[I(MedalEnum.Gold)];
                silverImage.sprite = Medals[I(MedalEnum.Silver)];

                __instance.devTime.SetText(Helpers.FormatTime(communityTimes[medalEarned] / 1000, medalEarned != I(MedalEnum.Dev) || ShowMS.extended.Value, '.', true));
                __instance.devTime.color = AdjustedColor(Colors[medalEarned]);
                if (medalEarned < I(MedalEnum.Sapphire))
                {
                    TextMeshProUGUI nextTime = FindOrCreateNextTime(__instance);
                    nextTime.SetText(Helpers.FormatTime(communityTimes[medalEarned + 1] / 1000, true, '.', true));
                    nextTime.color = AdjustedColor(Colors[medalEarned + 1]);
                    nextTime.enabled = !hideOld.Value;
                }
                else
                    DestroyNextTime(__instance);

                if (level.isSidequest)
                {
                    __instance._medalInfoHolder.SetActive(true);
                    __instance.devStamp.SetActive(true);
                }
            }
            else
            {
                if (level.isSidequest)
                {
                    aceImage.sprite = Crystals[I(MedalEnum.Sapphire)];
                    goldImage.sprite = Crystals[I(MedalEnum.Amethyst)];
                    silverImage.sprite = Crystals[I(MedalEnum.Emerald)];
                    aceImage.preserveAspect = true;
                    goldImage.preserveAspect = true;
                    silverImage.preserveAspect = true;

                    __instance._medalInfoHolder.SetActive(true);
                    __instance._emptyFrameFiller.SetActive(false);
                }
                else
                {
                    aceImage.sprite = Medals[I(MedalEnum.Sapphire)];
                    goldImage.sprite = Medals[I(MedalEnum.Amethyst)];
                    silverImage.sprite = Medals[I(MedalEnum.Emerald)];
                }

                __instance._aceMedalBG.SetActive(medalEarned >= I(MedalEnum.Sapphire));
                __instance._goldMedalBG.SetActive(medalEarned >= I(MedalEnum.Amethyst));
                __instance._silverMedalBG.SetActive(medalEarned >= I(MedalEnum.Emerald));

                __instance._aceMedalTime.text = (string)styleTime.Invoke(__instance, [
                    Helpers.FormatTime(communityTimes[I(MedalEnum.Sapphire)] / 1000, true, '.', true),
                    medalEarned >= (int)MedalEnum.Sapphire]);
                __instance._goldMedalTime.text = (string)styleTime.Invoke(__instance, [
                    Helpers.FormatTime(communityTimes[I(MedalEnum.Amethyst)] / 1000, true, '.', true),
                    medalEarned >= (int)MedalEnum.Amethyst]);
                __instance._silverMedalTime.text = (string)styleTime.Invoke(__instance, [
                    Helpers.FormatTime(communityTimes[I(MedalEnum.Emerald)] / 1000, true, '.', true),
                    medalEarned >= (int)MedalEnum.Emerald]);

                if (medalEarned >= (int)MedalEnum.Blud)
                {
                    __instance.devStamp.SetActive(true);
                    __instance.devTime.text = Helpers.FormatTime(communityTimes[I(MedalEnum.Blud)] / 1000, true, '.', true);
                    __instance.devTime.color = AdjustedColor(Colors[medalEarned]);
                }
                else if (medalEarned >= (int)MedalEnum.Topaz)
                {
                    __instance.devStamp.SetActive(true);
                    __instance.devTime.text = Helpers.FormatTime(communityTimes[I(MedalEnum.Topaz)] / 1000, true, '.', true);
                    __instance.devTime.color = AdjustedColor(Colors[medalEarned]);
                }
                else
                    __instance.devStamp.SetActive(false);

                if (hideOld.Value)
                {
                    String hiddenTime = "?:??.???";
                    if (medalEarned < I(MedalEnum.Sapphire))
                        __instance._aceMedalTime.text = hiddenTime;
                    if (medalEarned < I(MedalEnum.Amethyst))
                        __instance._goldMedalTime.text = hiddenTime;
                    if (medalEarned < I(MedalEnum.Emerald))
                        __instance._silverMedalTime.text = hiddenTime;
                }
            }
        }

        static TextMeshProUGUI FindOrCreateNextTime(LevelInfo levelInfo)
        {
            Transform nextTime = levelInfo.devTime.transform.parent.Find("NextTimeGoalText");
            if (nextTime == null)
            {
                nextTime =
                    UnityEngine.Object.Instantiate(levelInfo.devTime.gameObject, levelInfo.devTime.transform.parent).transform;
                nextTime.name = "NextTimeGoalText";
                //nextTimeGameObject.transform.position += new Vector3(1.18f, -0.1f);
                nextTime.localPosition += new Vector3(254.88f, -21.6f);
                var rectTransform = nextTime as RectTransform;
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, 45);
                rectTransform.rotation = Quaternion.identity;
            }

            return nextTime.GetComponent<TextMeshProUGUI>();
        }

        static void DestroyNextTime(LevelInfo levelInfo)
        {
            Transform nextTime = levelInfo.devTime.transform.parent.Find("NextTimeGoalText");
            if (nextTime)
                UnityEngine.Object.Destroy(nextTime.gameObject);
        }

        static void PostSetLevelData(MenuButtonLevel __instance, LevelData ld)
        {
            if (!Ready || !medalTimes.ContainsKey(ld.levelID))
                return;

            AdjustMaterial(__instance._medal);
            if (ld.isSidequest)
                AdjustMaterial(__instance._imageLoreFilled);

            int medalEarned = GetMedalIndex(ld.levelID);

            if (medalEarned < I(MedalEnum.Dev))
                return;

            __instance._medal.sprite = Medals[medalEarned];
            __instance._imageLoreBacking.enabled = !ld.isSidequest;

            if (ld.isSidequest)
                __instance._imageLoreFilled.sprite = Crystals[medalEarned];
        }

        static readonly FieldInfo currentLevelData = Helpers.Field(typeof(Leaderboards), "currentLevelData");
        static void PostSetScore(LeaderboardScore __instance, ref ScoreData newData, bool globalNeonRankings)
        {
            if (!Ready || globalNeonRankings) return;

            Leaderboards leaderboard = __instance.GetComponentInParent<Leaderboards>();
            if (leaderboard == null) return; // somehow??
            LevelData levelData = (LevelData)currentLevelData.GetValue(leaderboard);
            MedalEnum highestMedalOnLeaderboard = leaderboardSaphPlus.Value ? MedalEnum.Blud : MedalEnum.Sapphire;
            if (levelData == null || !medalTimes.ContainsKey(levelData.levelID)) return;

            int medalEarned = GetMedalIndex(levelData.levelID, newData._scoreValueMilliseconds * 1000);
            AdjustMaterial(__instance._medal);

            if (hideLeaderboard.Value && medalEarned >= I(MedalEnum.Dev))
            {
                int userMedal = GetMedalIndex(levelData.levelID); // medal the user has on this level
                if (medalEarned > userMedal)
                    medalEarned = Math.Max(userMedal, I(MedalEnum.Ace)); // ensure the medal to be displayed is only as high as the user's, but only as low as ace
            }

            if (!levelData.isSidequest)
            {
                __instance._medal.sprite = Medals[Math.Min(medalEarned, I(highestMedalOnLeaderboard))];
                __instance._medal.gameObject.SetActive(true);
            }
            else if (medalEarned > (int)MedalEnum.Dev)
            {
                __instance._medal.preserveAspect = true;
                __instance._medal.sprite = Crystals[Math.Min(medalEarned, I(highestMedalOnLeaderboard))];
                __instance._medal.gameObject.SetActive(true);
            }
        }

        static long lastBest;
        static void PreOnWin() => lastBest = NeonLite.Game.GetGameData().GetLevelStats(NeonLite.Game.GetCurrentLevel().levelID)._timeBestMicroseconds;
        static void PostSetMedal(MenuScreenResults __instance, int medalEarned, int oldInsightLevel, int previousMedal, ref int ____medalEarned)
        {
            if (!Ready)
                return;

            NeonLite.Logger.DebugMsg($"{medalEarned} {oldInsightLevel} {previousMedal}");

            var level = NeonLite.Game.GetCurrentLevel();
            GameData gameData = NeonLite.Game.GetGameData();
            LevelStats levelStats = gameData.GetLevelStats(level.levelID);

            if (!medalTimes.ContainsKey(level.levelID))
                return;

            var modded = GetMedalIndex(level.levelID);
            __instance._levelCompleteMedalImage.sprite = Medals[modded];
            AdjustMaterial(__instance._levelCompleteMedalImage);

            if (!(medalEarned == 4 || (medalEarned == 0 && previousMedal == 4) || levelStats.IsNewBest()) || modded == GetMedalIndex(level.levelID, lastBest))
                return;
            if (oldInsightLevel == 4)
            {
                __instance._pityEarned_Localized.SetKey(""); // disable that, we're at max
                __instance._insightEarned_Localized.SetKey(""); // disable this too, we're at max
            }
            else if (modded >= I(MedalEnum.Emerald))
                __instance._insightEarned_Localized.SetKey("NeonLite/RESULTS_MEDAL_MODDED_INSIGHT");
            if (modded <= I(MedalEnum.Dev) || modded >= I(MedalEnum.Topaz)) // don't do anything else on dev and under
                return;

            string locKey = E(modded) switch
            {
                MedalEnum.Emerald => "NeonLite/RESULTS_MEDAL_EMERALD",
                MedalEnum.Amethyst => "NeonLite/RESULTS_MEDAL_AMETHYST",
                MedalEnum.Sapphire => "NeonLite/RESULTS_MEDAL_SAPPHIRE",
                _ => ""
            };

            __instance._levelCompleteMedalText_Localized.SetKey(locKey);

            ____medalEarned = 4;
        }

        static void PostSetVisible(MenuScreenResults __instance)
        {
            if (LevelRush.IsLevelRush())
                return;
            var split = __instance.levelComplete_Localized.textMeshProUGUI.text.Split();
            var level = NeonLite.Game.GetCurrentLevel();
            var name = LocalizationManager.GetTranslation(level.GetLevelDisplayName());
            if (string.IsNullOrEmpty(name))
                name = level.levelDisplayName;
            if (split.Length > 1)
                name = "  " + name;
            var list = split.ToList();
            var str = $"<nobr><alpha=#AA><size=40%><noparse>{name}</noparse></size></nobr><alpha=#FF><br>";
            if (split.Length < 2)
                list.Insert(list.Count - 1, str);
            else
                list[list.Count - 2] += str;
            __instance.levelComplete_Localized.textMeshProUGUI.text = string.Join("", list);
        }

#if !XBOX
        // file spec:
        // - byte: version
        // - byte[121]: level status (-1 for incomplete medal index otherwise)
        // - byte[bronze through amethyst]: count per medal
        // - bytebool: any further medals past saph
        // - byte: saph or saph+ count
        // if any further medals past saph: // it's arranged this way to make reading easier
        //   - byte: real saph count
        //   - for each further medal:
        //     - byte index (to match with level status table)
        //     - RRGGBB 3 byte color
        //     - byte count
        // a very innefficient filespec alignment wise but sizewise very compressed


        static string OnSteamLBWrite(BinaryWriter writer, SteamLBFiles.LBType type, bool _)
        {
            if (type != SteamLBFiles.LBType.Global || !uploadGlobal.Value)
                return null;
            writer.Write((byte)1); // VERSION

            Dictionary<int, byte> medalCounts = [];

            // print out all levels
            foreach (CampaignData campaign in NeonLite.Game.GetGameData().campaigns)
            {
                if (!Enum.IsDefined(typeof(CampaignData.CampaignType), campaign.campaignType))
                    continue;
                foreach (MissionData mission in campaign.missionData)
                {
                    if (!Enum.IsDefined(typeof(MissionData.MissionType), mission.missionType))
                        continue;
                    if (mission.missionID.Contains("GREEN")) // ignore that shit
                        continue;
                    foreach (LevelData level in mission.levels)
                    {
                        var m = GetMedalIndex(level.levelID);
                        if (!medalCounts.TryGetValue(m, out var c))
                            c = 0;
                        medalCounts[m] = ++c;

                        writer.Write((byte)m);
                    }
                }
            }

            // write all except pre-saph
            for (int i = -1; i < I(MedalEnum.Sapphire); ++i)
            {
                if (!medalCounts.TryGetValue(i, out var c))
                    c = 0;

                NeonLite.Logger.BetaMsg($"Medal UGC: Write {E(i)} {(int)c}");
                writer.Write(c);
            }

            // handle saph+ special
            bool anyOver = medalCounts.Any(kv => kv.Key > I(MedalEnum.Sapphire));
            writer.Write(anyOver);

            var saphpl = medalCounts.Where(kv => kv.Key >= I(MedalEnum.Sapphire)).Sum(kv => kv.Value);
            NeonLite.Logger.BetaMsg($"Medal UGC: Sapphire+ {saphpl}");

            writer.Write((byte)saphpl);

            if (anyOver)
            {
                // if we have any medals over saph, here's wherw we handle

                // write the ones that are ACTUALLY just saph
                if (!medalCounts.TryGetValue(I(MedalEnum.Sapphire), out var c))
                    c = 0;

                NeonLite.Logger.BetaMsg($"Medal UGC: Just Sapphire {(int)c}");

                writer.Write(c);

                // write bonus medals
                for (int i = I(MedalEnum.Sapphire) + 1; i <= medalCounts.Max(kv => kv.Key); ++i)
                {
                    if (!medalCounts.ContainsKey(i))
                        continue;
                    writer.Write((byte)i); // write the index for future use

                    // write they color
                    var col = Colors[i]; // this better be in there

                    writer.Write((byte)(col.r * 255));
                    writer.Write((byte)(col.g * 255));
                    writer.Write((byte)(col.b * 255));

                    writer.Write(medalCounts[i]);
                }
            }

            return LB_FILE;
        }

        static void OnSteamLBRead(BinaryReader reader, int length, LeaderboardScore score)
        {
            var ver = reader.ReadByte();

            List<(Color, int)> medals = [];

            switch (ver)
            {
                default:
                    NeonLite.Logger.Error($"Unknown community medal UGC version {ver}");
                    return;
                case 1:
                    {
                        const int LEVEL_COUNT = 121;
                        // that's right we're gonna cheat (do nothing w this, there for other mods)
                        reader.ReadBytes(LEVEL_COUNT);

                        // first, read any we haven't completed
                        reader.ReadByte(); // i haven't decided if im doing anything with this value

                        // read nonsaphs
                        for (int i = 0; i < I(MedalEnum.Sapphire); ++i)
                            medals.Add((Colors[i], reader.ReadByte()));

                        var anyOver = reader.ReadBoolean();

                        if (anyOver && showExtendedGlobal.Value)
                        {
                            // alright we got the CrAAAAYZ shit
                            // skip the combined saph byte
                            reader.ReadByte();

                            // read the solo saph byte
                            var count = reader.ReadByte();
                            medals.Add((Colors[I(MedalEnum.Sapphire)], count));
                            NeonLite.Logger.BetaMsg($"Medal UGC: Read just saph {count}");

                            while (reader.BaseStream.Position < length)
                            {
                                var index = reader.ReadByte(); //index, we do nothing with
                                NeonLite.Logger.BetaMsg($"Medal UGC: Read index {index}");

                                var col = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 0xFF);
                                NeonLite.Logger.BetaMsg($"Color? {col}");

                                count = reader.ReadByte();
                                NeonLite.Logger.BetaMsg($"Count? {count}");

                                medals.Add((col, count));
                            }
                        }
                        else
                        {
                            // read the combined byte and we're done
                            medals.Add((Colors[I(MedalEnum.Sapphire)], reader.ReadByte()));
                        }


                        break;
                    }
            }

            StringBuilder builder = new(); // we make the demon now
            const string COLORED = "<size=175%><voffset=-0.07em>\u2022</voffset></size><size=50%> </size>";
            const int MARGIN = 15;

            medals.Reverse();
            foreach ((var color, var count) in medals)
            {
                if (count == 0)
                    continue;

                Color.RGBToHSV(color, out var h, out var s, out var v);
                h -= hueShift.Value;
                while (h < 0)
                    h += 1;

                s -= 0.1f;
                v += 0.25f;

                var cstr = ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(h, s, v));

                builder.Append($"<color=#{cstr}>{COLORED}</color>{count} ");
            }

            var tmp = Utils.InstantiateUI(score._scoreValue.gameObject, "MedalCount", score.transform).GetComponent<TextMeshProUGUI>();

            tmp.rectTransform.pivot = new(1, 0.5f);
            tmp.enableAutoSizing = false;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.richText = true;
            tmp.text = builder.ToString();
            tmp.fontSize = 17;
            tmp.margin = new(0, 0, MARGIN, 0);

            var username = score._username.rectTransform;
            tmp.rectTransform.position = username.TransformPoint(new(username.rect.xMax, username.rect.center.y));

            var csf = tmp.GetOrAddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutRebuilder.ForceRebuildLayoutImmediate(tmp.rectTransform);

            // it's disgusting math time

            var tomove = tmp.rectTransform.rect.width + MARGIN;
            var upivot = username.pivot.x;

            var upos = username.localPosition;
            upos.x -= tomove * upivot;
            username.localPosition = upos;

            var usize = username.sizeDelta;
            usize.x -= tomove;
            username.sizeDelta = usize;
        }
#endif
    }
}
