using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using I2.Loc;
using MelonLoader;
using MelonLoader.TinyJSON;
using NeonLite.Modules.UI;
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
        const string URL = "https://raw.githubusercontent.com/Faustas156/NeonLite/main/Resources/communitymedals.json";


        // All stamps (null for bronze/silver/gold/ace)
        public static Sprite[] Stamps => [.. _medalDatas.Select(x => x.sStamp)];
        // All crystals (bronze for not done, silver+ for done, ... , modded)
        public static Sprite[] Crystals => [.. _medalDatas.Select(x => x.sCrystal)];
        // All medals
        public static Sprite[] Medals => [.. _medalDatas.Select(x => x.sMedal)];
        // All colors (including custom ones for pre-dev)
        public static Color[] Colors => [.. _medalDatas.Select(x => x.color)];

        public class MedalData
        {
            public Color color;
            public Sprite sMedal;
            public Sprite sStamp;
            public Sprite sCrystal;
            public bool hidden = false;
            public string popup;
            public string name;
            public int rank;

            public Dictionary<string, long> times;
        }

        // All medal datas. Times field is only set for extensions
        public static MedalData[] MedalDatas => [.. _medalDatas];
        static readonly List<MedalData> _medalDatas = new(I(MedalEnum.Plus));

        // This enum only supports up to non-extension

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
            Plus
        }

        private static Sprite[] existingCache = new Sprite[3];
        private static Sprite[] imageCache;
        private static string[] pastPaths;

        private static List<string> wrUpdated = new List<string>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MedalEnum E(int i) => (MedalEnum)i;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int I(MedalEnum e) => (int)e;

        static bool assetReadyUnderlying;
        public static bool Ready
        {
            get { return assetReadyUnderlying && medalTimes.Count != 0; }
            private set
            {
                assetReadyUnderlying = value;
            }
        }

        public enum DisplayStyle
        {
            Rolling,
            Static,
            Stamps
        }

        public static event Action AssetsFinished;
        static bool fetched;
        static bool loaded;

        static bool pastCustomStandardMedals;
        static bool pastRecordMedals;
        static bool pastRecordHidden;

        public static MelonPreferences_Entry<bool> setting;
        public static MelonPreferences_Entry<DisplayStyle> style;
        internal static MelonPreferences_Entry<bool> hideOld;
        internal static MelonPreferences_Entry<bool> hideLeaderboard;
        internal static MelonPreferences_Entry<bool> customStandardMedals;
        internal static MelonPreferences_Entry<bool> recordMedals;
        internal static MelonPreferences_Entry<bool> recordHidden;

        public static MelonPreferences_Entry<float> hueShift;
        internal static MelonPreferences_Entry<string> overrideURL;
        internal static List<MelonPreferences_Entry<Color>> medalColors = new List<MelonPreferences_Entry<Color>>();
        internal static List<MelonPreferences_Entry<string>> medalImagePaths = new List<MelonPreferences_Entry<string>>();
        internal static List<MelonPreferences_Entry<string>> stampImagePaths = new List<MelonPreferences_Entry<string>>();
        internal static List<MelonPreferences_Entry<string>> crystalImagePaths = new List<MelonPreferences_Entry<string>>();

#if !XBOX
        internal static MelonPreferences_Entry<bool> uploadGlobal;
        public static MelonPreferences_Entry<LBDisplay> showGlobalMedals;
#endif
        public static Material HueShiftMat { get; private set; } = null;
        static Material defaultMat;

#if !XBOX
        const string LB_FILE = "nl_commedal";
#endif

        static void Setup()
        {
            setting = Settings.Add(Settings.h, "Medals", "comMedals", "Community Medals", "Shows new community medals past the developer red times to aim for.", true);
            hueShift = Settings.Add(Settings.h, "Medals", "hueShift", "Hue Shift", "Changes the hue of *all* medals (and related) help aid colorblind users in telling them apart.", 0f, new MelonLoader.Preferences.ValueRange<float>(0, 1));
            style = Settings.Add(Settings.h, "Medals", "style", "Display Style", "Whether to display the medals as a rolling list, a static list, or using stamps.", DisplayStyle.Rolling);
            hideOld = Settings.Add(Settings.h, "Medals", "hideOld", "Hide Times", "Hides unachieved medal times.", false);
            hideLeaderboard = Settings.Add(Settings.h, "Medals", "hideLeaderboard", "Hide Leaderboard Medals", "Unachieved medals will appear the same as your own on the leaderboards.", false);
#if !XBOX
            uploadGlobal = Settings.Add(Settings.h, "Medals", "uploadGlobal", "Upload to Global", "Whether to upload your medal data to global. This uploads all your level medals for other mods to potentially look at. Works with extended medals.", true);
            showGlobalMedals = Settings.Add(Settings.h, "Medals", "showGlobalMedals", "Global Medals to Display", "Which medals to show on the global leaderboard.", LBDisplay.Dev | LBDisplay.Emerald | LBDisplay.Amethyst | LBDisplay.Sapphire);
#endif
            customStandardMedals = Settings.Add(Settings.h, "Medals", "customStandardMedals", "Custom Standard Medal Images", "Use custom images for emerald, amethyst, and sapphire medals. Reload level to take effect.", false);
            recordMedals = Settings.Add(Settings.h, "Medals", "worldRecordMedals", "World Record Medals", "Turn on a separate medal for world records.", true);
            recordHidden = Settings.Add(Settings.h, "Medals", "recordHidden", "Hide World Record Medals", "World Record Medals do not show up until you obtain them.", true);

            pastCustomStandardMedals = customStandardMedals.Value;
            pastRecordMedals = recordMedals.Value;
            pastRecordHidden = recordHidden.Value;

            overrideURL = Settings.Add(Settings.h, "Medals", "overrideURL", "Extension URL", "Specifies additional community medals JSON URL to apply on top of the existing community medals. Restart game to customize images.", "");

            SetupVariableSettings();

            active = setting.SetupForModule(Activate, static (_, after) => after);
            hueShift.OnEntryValueChanged.Subscribe(static (_, after) => HueShiftMat?.SetFloat("_Shift", after));
            overrideURL.OnEntryValueChanged.Subscribe(static (_, after) => RefetchMedals());

            NeonLite.OnBundleLoad += AssetsDone;
#if !XBOX
            SteamLBFiles.OnLBWrite += OnSteamLBWrite;
            SteamLBFiles.RegisterForLoad(LB_FILE, OnSteamLBRead);
#endif
        }

        static void SetupVariableSettings()
        {
            List<string> names = [
                "Bronze",
                "Silver",
                "Gold",
                "Ace",
                "Dev",
                "Emerald",
                "Amethyst",
                "Sapphire",
            ];
            List<Color> colors = [
                new Color32(0xD1, 0x66, 0x20, 0xFF),
                new Color32(0x54, 0x54, 0x54, 0xFF),
                new Color32(0xD1, 0x9C, 0x38, 0xFF),
                new Color32(0x49, 0xA6, 0x9F, 0xFF),
                new(0.420f, 0.015f, 0.043f),
                new(0.388f, 0.8f, 0.388f),
                new(0.674f, 0.313f, 0.913f),
                new(0.043f, 0.317f, 0.901f)
            ];
            bool PreLoad(List<Color> cs, List<string> ns, string js)
            {
                List<int> ranks = new List<int>();
                try
                {
                    var variant = JSON.Load(js) as ProxyObject;
                    if (!variant.Keys.Contains("_metadata"))
                        return false; //old-style extension

                    var metadata = variant["_metadata"] as ProxyArray;
                    foreach (var medal in metadata.Cast<ProxyObject>())
                    {
                        int rank = medal["rank"];
                        if (ranks.Contains(rank))
                        {
                            NeonLite.Logger.Warning($"Duplicate medal rank {rank} already exists, ignoring");
                            continue;
                        }
                        ColorUtility.TryParseHtmlString(medal["color"], out var color);
                        colors.Add(color);
                        names.Add($"{medal["rank"]}");
                        NeonLite.Logger.DebugMsg("Added " + color + " and " + $"{medal["rank"]}");
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
            void FetchNext(string next)
            {
                var split = next.Split(['\n'], 2);
                var url = split[0].Trim();

                NeonLite.Logger.DebugMsg($"ext fetch {url}");

                Helpers.DownloadURL(url, request =>
                {
                    var load = request.result == UnityEngine.Networking.UnityWebRequest.Result.Success && PreLoad(colors, names, request.downloadHandler.text);
                    if (!load)
                        NeonLite.Logger.Warning($"Failed to load extended community medals from URL {url}.");

                    if (split.Length <= 1)
                    {
                        colors.Add(new Color32(255, 85, 252, 255));
                        names.Add("Record");
                        AddVariableSettings();
                        NeonLite.Logger.Msg("Finished preloading extended community medals!");
                    }
                    else
                        FetchNext(split[1]);
                });
            }
            FetchNext(overrideURL.Value);

            void AddVariableSettings()
            {
                NeonLite.Logger.DebugMsg("Updating Variable Settings, names ");
                foreach (var name in names)
                {
                    NeonLite.Logger.DebugMsg(name);
                }
                for (int i = I(MedalEnum.Emerald); i < colors.Count; i++)
                {
                    if (i < medalColors.Count + I(MedalEnum.Emerald)) continue;
                    Color c = colors[i];
                    string name = names[i];
                    NeonLite.Logger.DebugMsg("Adding " + name + " color");
                    medalColors.Add(Settings.Add(Settings.h, "Medals", $"{name}Color", $"{name} Color", $"Color for {name} times.", c));
                }

                for (int i = I(MedalEnum.Emerald); i < names.Count; i++)
                {
                    if (i < medalImagePaths.Count + I(MedalEnum.Emerald)) continue;
                    string name = names[i];
                    medalImagePaths.Add(Settings.Add(Settings.h, "Medals", $"{name}MedalImagePath", $"Custom {name} Medal Image", $"Set a custom {name} medal image by entering the path to a local image. Reload level to take effect.", ""));
                }

                for (int i = I(MedalEnum.Emerald); i < names.Count; i++)
                {
                    if (i < stampImagePaths.Count + I(MedalEnum.Emerald)) continue;
                    string name = names[i];
                    stampImagePaths.Add(Settings.Add(Settings.h, "Medals", $"{name}StampImagePath", $"Custom {name} Stamp Image", $"Set a custom {name} stamp image by entering the path to a local image. Reload level to take effect.", ""));
                }

                for (int i = I(MedalEnum.Emerald); i < names.Count; i++)
                {
                    if (i < crystalImagePaths.Count + I(MedalEnum.Emerald)) continue;
                    string name = names[i];
                    crystalImagePaths.Add(Settings.Add(Settings.h, "Medals", $"{name}CrystalImagePath", $"Custom {name} Crystal Image", $"Set a custom {name} crystal image by entering the path to a local image. Reload level to take effect.", ""));
                }
            }
        }

        static bool Load(string js)
        {
            try
            {
                var variant = JSON.Load(js) as ProxyObject;

                foreach (var pk in variant)
                {
                    var level = NeonLite.Game.GetGameData().GetLevelData(pk.Key);

                    List<long> community = [.. pk.Value as ProxyArray];
                    List<long> initial = [];

                    if (level || !level.isSidequest)
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

        static readonly Dictionary<int, MedalData> extensions = [];
        static bool LoadExtension(string js)
        {
            try
            {
                var variant = JSON.Load(js) as ProxyObject;
                if (!variant.Keys.Contains("_metadata"))
                    return Load(js); // old-style extension

                var metadata = variant["_metadata"] as ProxyArray;
                int index = 0;
                foreach (var medal in metadata.Cast<ProxyObject>())
                {
                    int rank = medal["rank"];
                    if (extensions.ContainsKey(rank))
                    {
                        NeonLite.Logger.Warning($"Duplicate medal rank {rank} already exists, ignoring");
                        continue;
                    }
                    ColorUtility.TryParseHtmlString(medal["color"], out var color);
                    var data = new MedalData()
                    {
                        color = color,
                        popup = medal["popup"],
                        hidden = medal["hidden"],
                        name = $"{medal["rank"]}",
                        rank = medal["rank"],

                        times = []
                    };

                    foreach (var pk in variant)
                    {
                        if (pk.Key == "_metadata")
                            continue;

                        long value = long.MinValue;
                        if (pk.Value is ProxyNumber num)
                            value = num;
                        else if (pk.Value is ProxyArray arr)
                            value = arr[index];

                        data.times.Add(pk.Key, value);
                    }

                    Helpers.DownloadURL(medal["medali"], res =>
                    {
                        if (res.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                            return;
                        void SetTex() => data.sMedal = LoadSpriteData(Medals[0], res.downloadHandler.data);
                        if (!Ready)
                            AssetsFinished += SetTex;
                        else
                            SetTex();
                    });
                    Helpers.DownloadURL(medal["stampi"], res =>
                    {
                        if (res.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                            return;
                        void SetTex() => data.sStamp = LoadSpriteData(Stamps[I(MedalEnum.Dev)], res.downloadHandler.data);
                        if (!Ready)
                            AssetsFinished += SetTex;
                        else
                            SetTex();
                    });
                    Helpers.DownloadURL(medal["crysti"], res =>
                    {
                        if (res.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                            return;
                        void SetTex() => data.sCrystal = LoadSpriteData(Crystals[0], res.downloadHandler.data);
                        if (!Ready)
                            AssetsFinished += SetTex;
                        else
                            SetTex();
                    });

                    extensions.Add(rank, data);
                    ++index;
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

        static void LoadRecords()
        {
            NeonLite.Logger.DebugMsg("Loading Records...");
            int rank = extensions
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value)
                    .ToArray()[extensions.Count - 1].rank + 100;
            var data = new MedalData()
            {
                color = new Color32(255, 85, 252, 255),
                popup = "NLEM/RESULTS_MEDAL_WR",
                hidden = recordMedals.Value ? recordHidden.Value : true,
                name = "Record",

                times = []
            };

            string path = Path.Combine(Helpers.GetSaveDirectory(), "NeonLite", "records.json");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, Resources.records.GetUTF8String());
            }
            string js = File.ReadAllText(path);
            var variant = JSON.Load(js) as ProxyObject;

            foreach (var pk in NeonLite.Game.GetGameData().GetLevelDataIDsList(false))
            {
                if (variant.Keys.Contains(pk))
                    data.times.Add(pk, variant[pk].ToInt64(null));
            }

            void SetTexMedal() => data.sMedal = LoadSpriteData(Medals[0], Resources.MedalRecord.GetBytes());
            if (!Ready)
                AssetsFinished += SetTexMedal;
            else
                SetTexMedal();

            void SetTexStamp() => data.sStamp = LoadSpriteData(Stamps[I(MedalEnum.Dev)], Resources.MikeyRecord.GetBytes());
            if (!Ready)
                AssetsFinished += SetTexStamp;
            else
                SetTexStamp();

            void SetTexCrystal() => data.sCrystal = LoadSpriteData(Crystals[0], Resources.CrystalRecord.GetBytes());
            if (!Ready)
                AssetsFinished += SetTexCrystal;
            else
                SetTexCrystal();

            extensions.Add(rank, data);
            NeonLite.Logger.DebugMsg("Finished Loading Records");
        }

        static Sprite LoadSpriteData(Sprite sBase, byte[] data)
        {
            var t = sBase.texture;
            var newTex = new Texture2D(t.width, t.height, t.format, t.mipmapCount, false)
            {
                wrapMode = t.wrapMode,
                filterMode = t.filterMode
            };
            newTex.LoadImage(data, true);

            return Sprite.Create(newTex,
                new Rect(0, 0, newTex.width, newTex.height),
                new Vector2(newTex.width, newTex.height) * sBase.pivot / new Vector2(t.width, t.height),
                sBase.pixelsPerUnit);
        }

        static void FinalizeExtensions()
        {
            var exts = extensions
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value)
                    .ToArray();

            bool hide = false;
            foreach (var ext in exts)
            {
                if (ext.hidden)
                    hide = true;
                if (hide)
                    ext.hidden = true;

                foreach (var kv in ext.times)
                {
                    if (!medalTimes.ContainsKey(kv.Key))
                        continue;
                    medalTimes[kv.Key] = [.. medalTimes[kv.Key], kv.Value];
                }

                _medalDatas.Add(ext);
            }
        }

        public static int GetMedalIndex(string level, long time = -1)
        {
            var stats = GameDataManager.GetLevelStats(level);
            if (!stats.GetCompleted())
                return -1;

            if (time == -1)
                time = stats._timeBestMicroseconds;

            var times = medalTimes[level];
            var cap = Math.Min(times.Length, _medalDatas.Count);
            for (int i = cap - 1; i >= 0; i--)
            {
                if (time <= times[i])
                {
                    int index = recordMedals.Value ? i : Math.Min(i, _medalDatas.Count - 2);
                    return index;
                }
            }
            return 0;
        }

        public static void RefetchMedals()
        {
            fetched = false;
            OnLevelLoad(null);
        }

        static bool fetching = false;
        static void DownloadMedals()
        {
            if (fetching)
                return;
            fetched = true;
            extensions.Clear();
            if (_medalDatas.Count > I(MedalEnum.Plus)) // remove all exts
                _medalDatas.RemoveRange(I(MedalEnum.Plus), _medalDatas.Count - I(MedalEnum.Plus));

            fetching = true;
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
                    if (!Load(Resources.communitymedals.GetUTF8String()))
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
                                var load = request.result == UnityEngine.Networking.UnityWebRequest.Result.Success && LoadExtension(request.downloadHandler.text);
                                if (!load)
                                    NeonLite.Logger.Warning($"Failed to load extended community medals from URL {url}.");

                                if (split.Length <= 1)
                                {
                                    LoadRecords();
                                    NeonLite.Logger.Msg("Finished loading extended community medals!");
                                    FinalizeExtensions();
                                }
                                else
                                    FetchNext(split[1]);
                            });
                        }
                        FetchNext(overrideURL.Value);
                    }
                    else
                        NeonLite.Logger.Msg("Fetched community medals!");
                }

                fetching = false;
            });
        }

        internal static void OnLevelLoad(LevelData _)
        {

            if (!fetched)
            {
                medalTimes.Clear();
                DownloadMedals();
            }

            if (loaded)
                AssetsDone(NeonLite.bundle);

            UpdateMedals(NeonLite.bundle);
        }

        static void UpdateMedals(AssetBundle bundle)
        {
            if (recordHidden.Value != pastRecordHidden)
            {
                _medalDatas[_medalDatas.Count - 1].hidden = recordHidden.Value;
                pastRecordHidden = recordHidden.Value;
            }

            if (recordMedals.Value != pastRecordMedals)
            {
                _medalDatas[_medalDatas.Count - 1].hidden = recordMedals.Value ? recordHidden.Value : true;
                pastRecordMedals = recordMedals.Value;
            }

            for (int i = I(MedalEnum.Emerald); i < Colors.Length; i++)
            {
                _medalDatas[i].color = medalColors[i - I(MedalEnum.Emerald)].Value;
            }

            if (!Ready || existingCache[0] == null || pastPaths == null)
                return;

            if (customStandardMedals.Value || !customStandardMedals.Value && pastCustomStandardMedals)
            {
                _medalDatas[5].sMedal = LoadSprite(0, 0, null, bundle);
                _medalDatas[5].sStamp = LoadSprite(0, 1, null, bundle);
                _medalDatas[5].sCrystal = LoadSprite(0, 2, null, bundle);
                _medalDatas[6].sMedal = LoadSprite(1, 0, null, bundle);
                _medalDatas[6].sStamp = LoadSprite(1, 1, null, bundle);
                _medalDatas[6].sCrystal = LoadSprite(1, 2, null, bundle);
                _medalDatas[7].sMedal = LoadSprite(2, 0, null, bundle);
                _medalDatas[7].sStamp = LoadSprite(2, 1, null, bundle);
                _medalDatas[7].sCrystal = LoadSprite(2, 2, null, bundle);
                pastCustomStandardMedals = customStandardMedals.Value;
            }

            for (int i = 8; i < _medalDatas.Count; i++)
            {
                _medalDatas[i].sMedal = LoadSprite(i - I(MedalEnum.Emerald), 0, existingCache[0], null);
                _medalDatas[i].sStamp = LoadSprite(i - I(MedalEnum.Emerald), 1, existingCache[1], null);
                _medalDatas[i].sCrystal = LoadSprite(i - I(MedalEnum.Emerald), 2, existingCache[2], null);
            }
        }

        static void Activate(bool activate)
        {
            OnLevelLoad(null);

            Patching.TogglePatch(activate, typeof(LevelInfo), "SetLevel", Helpers.HM(PostSetLevel).SetPriority(Priority.First), Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(MenuButtonLevel), "SetLevelData", PostSetLevelData, Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(LeaderboardScore), "SetScore", PostSetScore, Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(Game), "OnLevelWin", PreOnWin, Patching.PatchTarget.Prefix);
            Patching.TogglePatch(activate, typeof(MenuScreenResults), "SetMedal", PostSetMedal, Patching.PatchTarget.Postfix);

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

        private static Sprite LoadSprite(int medalNum, int type, Sprite existing, AssetBundle bundle)
        {
            NeonLite.Logger.DebugMsg("Loading medal " + medalNum + " type " + type);
            int id = medalNum * 3 + type;
            string[] paths =
            {
                "Assets/Sprites/MedalEmerald.png",
                "Assets/Sprites/MikeyEmerald.png",
                "Assets/Sprites/CrystalEmerald.png",
                "Assets/Sprites/MedalAmethyst.png",
                "Assets/Sprites/MikeyAmethyst.png",
                "Assets/Sprites/CrystalAmethyst.png",
                "Assets/Sprites/MedalSapphire.png",
                "Assets/Sprites/MikeySapphire.png",
                "Assets/Sprites/CrystalSapphire.png",
            };

            string[] customPaths = new string[3 * (_medalDatas.Count - I(MedalEnum.Emerald))];
            int ind = 0;
            for (int i = 0; i < _medalDatas.Count - I(MedalEnum.Emerald); i++)
            {
                customPaths[ind++] = medalImagePaths[i].Value;
                customPaths[ind++] = stampImagePaths[i].Value;
                customPaths[ind++] = crystalImagePaths[i].Value;
            }

            if (!customStandardMedals.Value && id < 9) return bundle.LoadAsset<Sprite>(paths[id]);

            if (customPaths[id] == "" || !File.Exists(customPaths[id]))
            {
                switch (id % 3)
                {
                    case 0:
                        return _medalDatas[id / 3 + I(MedalEnum.Emerald)].sMedal;
                    case 1:
                        return _medalDatas[id / 3 + I(MedalEnum.Emerald)].sStamp;
                    case 2:
                        return _medalDatas[id / 3 + I(MedalEnum.Emerald)].sCrystal;
                }
            }

            if (pastPaths[id] == null || customPaths[id] != pastPaths[id])
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

        static void AssetsDone(AssetBundle bundle)
        {
            loaded = true;
            NeonLite.Logger.DebugMsg("CommunityMedals onBundleLoad");
            if (!NeonLite.activateLate)
                return;
            loaded = false;

            var gamedata = NeonLite.Game.GetGameData();
            var levelInfo = ((MenuScreenStaging)MainMenu.Instance()._screenStaging)
                    ._leaderboardsAndLevelInfoRef
                    .levelInfoRef;
            var devStamp = levelInfo.devStamp.transform
                    .Find("MikeyStampGraphic").GetComponent<Image>().sprite;

            existingCache[0] = gamedata.medalSprite_Bronze;
            existingCache[1] = devStamp;
            existingCache[2] = levelInfo._crystalSpriteSidequestFilled;

            Sprite[] medals = [
                gamedata.medalSprite_Bronze,
                gamedata.medalSprite_Silver,
                gamedata.medalSprite_Gold,
                gamedata.medalSprite_Ace,
                gamedata.medalSprite_Dev,
                bundle.LoadAsset<Sprite>("Assets/Sprites/MedalEmerald.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MedalAmethyst.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MedalSapphire.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MedalPlus.png"),
            ];

            Sprite[] stamps = [
                null,
                null,
                null,
                null,
                devStamp,
                bundle.LoadAsset<Sprite>("Assets/Sprites/MikeyEmerald.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MikeyAmethyst.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MikeySapphire.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/MikeyPlus.png"),
            ];

            Sprite[] crystals = [
                levelInfo._crystalSpriteSidequestEmpty,
                levelInfo._crystalSpriteSidequestFilled,
                levelInfo._crystalSpriteSidequestFilled,
                levelInfo._crystalSpriteSidequestFilled,
                levelInfo._crystalSpriteSidequestFilled,
                bundle.LoadAsset<Sprite>("Assets/Sprites/CrystalEmerald.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/CrystalAmethyst.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/CrystalSapphire.png"),
                bundle.LoadAsset<Sprite>("Assets/Sprites/CrystalPlus.png"),
            ];

            Color[] colors = [
                new Color32(0xD1, 0x66, 0x20, 0xFF),
                new Color32(0x54, 0x54, 0x54, 0xFF),
                new Color32(0xD1, 0x9C, 0x38, 0xFF),
                new Color32(0x49, 0xA6, 0x9F, 0xFF),
                new(0.420f, 0.015f, 0.043f),
                new(0.388f, 0.8f, 0.388f),
                new(0.674f, 0.313f, 0.913f),
                new(0.043f, 0.317f, 0.901f)
            ];

            string[] locales = [
                "Interface/RESULTS_MEDAL_BRONZE",
                "Interface/RESULTS_MEDAL_SILVER",
                "Interface/RESULTS_MEDAL_GOLD",
                "Interface/RESULTS_MEDAL_ACE",
                "NeonLite/RESULTS_MEDAL_DEV",
                "NeonLite/RESULTS_MEDAL_EMERALD",
                "NeonLite/RESULTS_MEDAL_AMETHYST",
                "NeonLite/RESULTS_MEDAL_SAPPHIRE"
            ];

            string[] names = [
                "Bronze",
                "Silver",
                "Gold",
                "Ace",
                "Dev",
                "Emerald",
                "Amethyst",
                "Sapphire",
                "Plus"
            ];

            int[] ranks = [
                0,
                10,
                20,
                30,
                40,
                50,
                60,
                70,
                100
            ];

            _medalDatas.Clear();

            for (int i = 0; i < I(MedalEnum.Plus); ++i)
            {
                var data = new MedalData()
                {
                    sMedal = medals[i],
                    sStamp = stamps[i],
                    sCrystal = crystals[i],
                    color = colors[i],
                    popup = locales[i],
                    name = names[i],
                    rank = ranks[i]
                };
                _medalDatas.Add(data);
            }

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

            if (!wrUpdated.Contains(level.levelID))
            {
                long wrTime = SpeedrunCom.GetLevelWR(level.levelID);

                if (wrTime != long.MinValue)
                {
                    wrTime += 999; //add 999 microseconds to be one below next millisecond
                    communityTimes[communityTimes.Length - 1] = wrTime;

                    string path = Path.Combine(Helpers.GetSaveDirectory(), "NeonLite", "records.json");
                    string js = File.ReadAllText(path);
                    var jsonObj = JSON.Load(js) as ProxyObject;
                    jsonObj[level.levelID] = new ProxyNumber(wrTime);
                    File.WriteAllText(path, JSON.Dump(jsonObj));

                    wrUpdated.Add(level.levelID);
                }
            }

            int medalEarned = GetMedalIndex(level.levelID);
            var data = _medalDatas[medalEarned];

            {
                // pastsight compatibility
                int pastSight = GetMedalIndex(level.levelID, levelStats.GetTimePastSight(true));
                if (!level.isSidequest)
                    __instance._levelMedal.sprite = Medals[pastSight];
                else
                    __instance._crystalHolderFilledImage.sprite = Crystals[pastSight];
            }

            if (style.Value != DisplayStyle.Rolling)
            {
                if (medalEarned < I(MedalEnum.Dev) && (!level.isSidequest || !levelStats.GetCompleted() || style.Value == DisplayStyle.Stamps))
                {
                    aceImage.sprite = Medals[I(MedalEnum.Ace)];
                    goldImage.sprite = Medals[I(MedalEnum.Gold)];
                    silverImage.sprite = Medals[I(MedalEnum.Silver)];
                    return;
                }
            }

            stamps[1].sprite = data.sStamp;
            stamps[2].sprite = data.sStamp;

            var cap = communityTimes.Length;
            var lastVis = _medalDatas
                .Select((data, i) => (data, i))
                .Reverse()
                .FirstOrDefault(di => !di.data.hidden && cap > di.i).i;

            int startingMedal = I(MedalEnum.Emerald);
            if (style.Value == DisplayStyle.Rolling)
            {
                startingMedal = Math.Min(medalEarned + 2, lastVis) - 2;
                startingMedal = Math.Max(I(MedalEnum.Silver), startingMedal); // make sure bronze doens't exist

                if (level.isSidequest)
                {
                    if (medalEarned < I(MedalEnum.Dev))
                        return; // we can't show rolling for sidequests
                    if (medalEarned == I(MedalEnum.Dev)) // start at emmy anyway, we have no dex
                        startingMedal = I(MedalEnum.Emerald);
                }
            }

            NeonLite.Logger.DebugMsg(E(startingMedal));

            if (style.Value != DisplayStyle.Stamps)
            {
                __instance.devStamp.SetActive(false);

                if (level.isSidequest)
                {
                    aceImage.sprite = Crystals[startingMedal + 2];
                    goldImage.sprite = Crystals[startingMedal + 1];
                    silverImage.sprite = Crystals[startingMedal + 0];
                    aceImage.preserveAspect = true;
                    goldImage.preserveAspect = true;
                    silverImage.preserveAspect = true;

                    __instance._medalInfoHolder.SetActive(true);
                    __instance._emptyFrameFiller.SetActive(false);
                }
                else
                {
                    aceImage.sprite = Medals[startingMedal + 2];
                    goldImage.sprite = Medals[startingMedal + 1];
                    silverImage.sprite = Medals[startingMedal + 0];
                }

                __instance._aceMedalBG.SetActive(medalEarned >= (startingMedal + 2));
                __instance._goldMedalBG.SetActive(medalEarned >= (startingMedal + 1));
                __instance._silverMedalBG.SetActive(medalEarned >= (startingMedal + 0));

                if (communityTimes[startingMedal + 2] == long.MinValue)
                {
                    __instance._aceMedalTime.text = "Loading...";
                }
                else
                {
                    __instance._aceMedalTime.text = (string)styleTime.Invoke(__instance, [
                        Helpers.FormatTime(communityTimes[startingMedal + 2] / 1000, true, '.', true),
                    medalEarned >= (startingMedal + 2)]);

                    medalTimes[level.levelID][startingMedal + 2] = communityTimes[startingMedal + 2];
                }
                __instance._goldMedalTime.text = (string)styleTime.Invoke(__instance, [
                    Helpers.FormatTime(communityTimes[startingMedal + 1] / 1000, true, '.', true),
                    medalEarned >= (startingMedal + 1)]);
                __instance._silverMedalTime.text = (string)styleTime.Invoke(__instance, [
                    Helpers.FormatTime(communityTimes[startingMedal + 0] / 1000, true, '.', true),
                    medalEarned >= (startingMedal + 0)]);

                if (hideOld.Value)
                {
                    string hiddenTime = "?:??.???";
                    if (medalEarned < startingMedal + 2)
                        __instance._aceMedalTime.text = hiddenTime;
                    if (medalEarned < startingMedal + 1)
                        __instance._goldMedalTime.text = hiddenTime;
                    if (medalEarned < startingMedal + 0)
                        __instance._silverMedalTime.text = hiddenTime;
                }
            }
            else
            {
                aceImage.sprite = Medals[I(MedalEnum.Ace)];
                goldImage.sprite = Medals[I(MedalEnum.Gold)];
                silverImage.sprite = Medals[I(MedalEnum.Silver)];
            }

            if (style.Value == DisplayStyle.Stamps ||
                (style.Value == DisplayStyle.Static && medalEarned >= I(MedalEnum.Plus)) ||
                (style.Value == DisplayStyle.Rolling && medalEarned > lastVis))
            {
                __instance.devStamp.SetActive(true);
                __instance.devTime.SetText(medalEarned != _medalDatas.Count - 1 ? Helpers.FormatTime(communityTimes[medalEarned] / 1000, medalEarned != I(MedalEnum.Dev) || ShowMS.extended.Value, '.', true) : "WORLD RECORD");
                __instance.devTime.color = AdjustedColor(Colors[medalEarned]);

                if (medalEarned + 1 < cap && !_medalDatas[medalEarned + 1].hidden)
                {
                    TextMeshProUGUI nextTime = FindOrCreateNextTime(__instance);
                    nextTime.SetText(Helpers.FormatTime(communityTimes[medalEarned + 1] / 1000, true, '.', true));
                    nextTime.color = AdjustedColor(Colors[medalEarned + 1]);
                    nextTime.enabled = !hideOld.Value;
                }
                else
                    DestroyNextTime(__instance);

                if (level.isSidequest)
                    __instance._medalInfoHolder.SetActive(true);
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
            if (levelData == null || !medalTimes.ContainsKey(levelData.levelID)) return;

            int medalEarned = GetMedalIndex(levelData.levelID, newData._scoreValueMilliseconds * 1000);
            AdjustMaterial(__instance._medal);

            int userMedal = GetMedalIndex(levelData.levelID); // medal the user has on this level
            if (hideLeaderboard.Value && medalEarned >= I(MedalEnum.Dev))
            {
                if (medalEarned > userMedal)
                    medalEarned = Math.Max(userMedal, I(MedalEnum.Ace)); // ensure the medal to be displayed is only as high as the user's, but only as low as ace
            }

            var cap = medalTimes[levelData.levelID].Length;
            var lastVis = _medalDatas
                .Select((data, i) => (data, i))
                .Reverse()
                .FirstOrDefault(di => !di.data.hidden && cap > di.i).i;

            lastVis = Math.Max(lastVis, userMedal);
            medalEarned = Math.Min(medalEarned, lastVis);

            if (!levelData.isSidequest)
            {
                __instance._medal.sprite = Medals[medalEarned];
                __instance._medal.gameObject.SetActive(true);
            }
            else if (medalEarned > (int)MedalEnum.Dev)
            {
                __instance._medal.preserveAspect = true;
                __instance._medal.sprite = Crystals[medalEarned];
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

            if (!(medalEarned == 4 || (medalEarned == 0 && previousMedal == 4) || levelStats.IsNewBest()) || (modded == GetMedalIndex(level.levelID, lastBest) && modded != _medalDatas.Count - 1 || !levelStats.IsNewBest() || !recordMedals.Value))
                return;
            if (oldInsightLevel == 4)
            {
                __instance._pityEarned_Localized.SetKey(""); // disable that, we're at max
                __instance._insightEarned_Localized.SetKey(""); // disable this too, we're at max
            }
            else if (modded >= I(MedalEnum.Emerald))
                __instance._insightEarned_Localized.SetKey("NeonLite/RESULTS_MEDAL_MODDED_INSIGHT");
            if (modded <= I(MedalEnum.Dev)) // don't do anything else on dev and under
                return;

            string locKey = _medalDatas[modded].popup;

            __instance._levelCompleteMedalText_Localized.SetKey(locKey);

            ____medalEarned = 4;
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

        [Flags]
        public enum LBDisplay
        {
            None = 0,
            Bronze = 1 << 0,
            Silver = 1 << 1,
            Gold = 1 << 2,
            Ace = 1 << 3,
            Dev = 1 << 4,
            Emerald = 1 << 5,
            Amethyst = 1 << 6,
            Sapphire = 1 << 7,
            Extended = 1 << 8,
        }

        static void OnSteamLBRead(BinaryReader reader, int length, LeaderboardScore score)
        {
            var ver = reader.ReadByte();

            List<(Color, int)> medals = [];

            var medalshow = showGlobalMedals.Value;

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
                        {
                            var count = reader.ReadByte();
                            if (medalshow.HasFlag((LBDisplay)(1 << i)) && count != 0)
                                medals.Add((Colors[i], count));
                        }

                        var anyOver = reader.ReadBoolean();

                        if (anyOver && medalshow.HasFlag(LBDisplay.Extended))
                        {
                            // alright we got the CrAAAAYZ shit
                            // skip the combined saph byte
                            reader.ReadByte();

                            // read the solo saph byte
                            var count = reader.ReadByte();
                            if (medalshow.HasFlag(LBDisplay.Sapphire) && count != 0)
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

                                if (count != 0)
                                    medals.Add((col, count));
                            }
                        }
                        else
                        {
                            // read the combined byte and we're done
                            var count = reader.ReadByte();
                            if (medalshow.HasFlag(LBDisplay.Sapphire) && count != 0)
                                medals.Add((Colors[I(MedalEnum.Sapphire)], count));
                        }


                        break;
                    }
            }

            StringBuilder builder = new(); // we make the demon now
            const string COLORED = "<size=155%><voffset=-0.09em>\u2022</voffset></size><size=30%> </size>";
            const int MARGIN = 12;

            medals.Reverse();
            foreach ((var color, var count) in medals)
            {
                Color.RGBToHSV(color, out var h, out var s, out var v);
                h -= hueShift.Value;
                while (h < 0)
                    h += 1;

                s -= 0.1f;
                v += 0.15f;
                if (v > 1)
                    v = 1;

                var cstr = ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(h, s, v));

                builder.Append($"<color=#{cstr}>{COLORED}</color>{count}<size=80%> </size>");
            }

            var tmp = Utils.InstantiateUI(score._scoreValue.gameObject, "MedalCount", score.transform).GetComponent<TextMeshProUGUI>();

            tmp.rectTransform.pivot = new(1, 0.5f);
            tmp.enableAutoSizing = false;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.richText = true;
            tmp.text = builder.ToString();
            tmp.fontSize = 16;
            tmp.margin = new(0, 0, MARGIN, 0);

            var username = score._username.rectTransform;
            tmp.rectTransform.position = username.TransformPoint(new(username.rect.xMax, username.rect.center.y));

            var csf = tmp.GetOrAddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutRebuilder.ForceRebuildLayoutImmediate(tmp.rectTransform);

            var tomove = tmp.rectTransform.rect.width + MARGIN;
            username.ResizeWithPivot(new Vector2(-tomove, 0));
        }
#endif
    }
}