#pragma warning disable IDE0130
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonLite.Modules
{
    [Module]
    public static class PastSight
    {
        const bool priority = false;
        const bool active = true;

        static float timer = 1;
        static float Opacity => AxKEasing.EaseInCubic(0, 1, Math.Abs(timer));

        public static bool Active => timer < 0;

        public static event Action<bool> OnActive;

        static MelonPreferences_Entry<Key> key;

        static readonly Dictionary<object, Extra> registered = [];

        static void Setup()
        {
            key = Settings.Add(Settings.h, "UI", "pastSight", "Past Sight Keybind",
                "Holding this key down will let you see the last run's time instead of your PB in places where you would normally see it.", Key.RightAlt);
        }

        static void Activate(bool _)
        {
            NeonLite.holder.AddComponent<PastSightBehavior>();

            Patching.AddPatch(typeof(LevelInfo), "SetLevel", PreSetLevelInfo, Patching.PatchTarget.Prefix);
            Patching.AddPatch(typeof(LevelInfo), "SetLevel", PostSetLevelInfo, Patching.PatchTarget.Postfix);
        }

        public static long GetTimePastSight(this LevelStats stats)
        {
            if (Active)
                return stats.GetTimeLastMicroseconds();
            return stats.GetTimeBestMicroseconds();
        }

        class Extra
        {
            public LevelData level;
        }

        class LevelInfoExtra : Extra
        {
            public bool isNew;
            public string bestTimeKey;
        }

        static void PreSetLevelInfo(LevelInfo __instance, LevelData level, bool isNewScore)
        {
            if (!level)
                return;

            if (!registered.TryGetValue(__instance, out var extr))
            {
                extr = new LevelInfoExtra();
                registered.Add(__instance, extr);
            }
            var extra = extr as LevelInfoExtra;
            extra.level = level;
            extra.isNew = isNewScore;
        }
        static void PostSetLevelInfo(LevelInfo __instance, LevelData level)
        {
            if (!level)
                return;

            if (!registered.TryGetValue(__instance, out var extr))
            {
                extr = new LevelInfoExtra();
                registered.Add(__instance, extr);
            }
            var extra = extr as LevelInfoExtra;

            const string prevtime = "NeonLite/LEVELINFO_TIME_PREVIOUS";

            var stats = GameDataManager.GetLevelStats(level.levelID);
            if (stats == null)
                return;

            if (__instance._levelBestTimeDescription_Localized.localizationKey != prevtime)
                extra.bestTimeKey = __instance._levelBestTimeDescription_Localized.localizationKey;

            __instance._levelBestTime.text = Helpers.FormatTime(stats.GetTimePastSight() / 1000, split: '.', cutoff: true);
            if (Active)
                __instance._levelBestTimeDescription_Localized.SetKey(prevtime);
            else if (__instance._levelBestTimeDescription_Localized.localizationKey != extra.bestTimeKey)
                __instance._levelBestTimeDescription_Localized.SetKey(extra.bestTimeKey);

            // ensure alpha

            __instance._levelBestTimeDescription.alpha = Opacity;
            __instance._levelBestTime.alpha = Opacity;

            if (!level.isSidequest)
                __instance._levelMedal.SetAlpha(Opacity);
            else
                __instance._crystalHolderFilledImage.SetAlpha(Opacity);

        }

        class PastSightBehavior : MonoBehaviour
        {
            const float SPEED = 0.25f;

            void Update()
            {
                var sign = Math.Sign(timer);
                bool pressed = false;
                if (Keyboard.current != null && Keyboard.current[key.Value].isPressed)
                    pressed = true;

                if (pressed)
                {
                    timer -= Time.unscaledDeltaTime / SPEED;
                    if (timer < -1)
                        timer = -1;
                }
                else
                {
                    timer += Time.unscaledDeltaTime / SPEED;
                    if (timer > 1)
                        timer = 1;
                }

                SetOpacity();
                if (sign != Math.Sign(timer))
                    OnFlip();
            }

            static void SetOpacity()
            {
                foreach (var kv in registered)
                {
                    if (kv.Key is LevelInfo levelInfo)
                    {
                        levelInfo._levelBestTimeDescription.alpha = Opacity;
                        levelInfo._levelBestTime.alpha = Opacity;

                        if (!kv.Value.level.isSidequest)
                            levelInfo._levelMedal.SetAlpha(Opacity);
                        else
                            levelInfo._crystalHolderFilledImage.SetAlpha(Opacity);
                    }
                }

            }

            static void OnFlip()
            {
                OnActive?.Invoke(Active);
                foreach (var kv in registered)
                {
                    if (kv.Key is LevelInfo levelInfo)
                    {
                        var extra = kv.Value as LevelInfoExtra;
                        levelInfo.SetLevel(extra.level, false, extra.isNew, true);
                    }
                }
            }
        }
    }
}
