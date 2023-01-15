using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AmmoCount.Tools;
using AmmoCount.Util;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using UnityEngine.UI;

namespace AmmoCount;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[HarmonyPatch]
public class AmmoCountPlugin : BaseUnityPlugin
{
    public enum Toggle
    {
        On = 1,
        Off = 0
    }

    internal const string ModName = "AmmoCount";
    internal const string ModVersion = "1.0.1";
    internal const string Author = "MadBuffoon";
    private const string ModGUID = Author + "." + ModName;
    private static readonly string ConfigFileName = ModGUID + ".cfg";
    private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

    internal static string ConnectionError = "";

    public static readonly ManualLogSource AmmoCountLogger =
        BepInEx.Logging.Logger.CreateLogSource(ModName);

    private static readonly ConfigSync ConfigSync = new(ModGUID)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

    public static Vector2 UIAnchorDrag;

    private readonly Harmony _harmony = new(ModGUID);
    // Delta drag

    public void Awake()
    {
        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
            "If on, the configuration is locked and can be changed by server admins only.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

        // UI Ammo Count
        UpdateDelayConfig = Config.Bind("2 - UI", "Update Rate", 0.3f,
            new ConfigDescription("The update in seconds.\nAuto rounds to the nearest 1/10 of a second",
                new AcceptableValueRange<float>(0.1f, 1.5f)));
        UIAnchor = Config.Bind("2 - UI", "Position", "0%, -22.5%", "Sets the Position");
        LocalScale = Config.Bind("2 - UI", "Scale", new Vector2(400.0f, 60.0f), "Sets the scale");
        AmmoTextConfig = Config.Bind("3 - Text", "Display Format", "{1}: {0}",
            "Sets how you view the text.\n{0} = current amount\n{1} Name");
        AmmoTextSizeConfig = Config.Bind("3 - Text", "Size", 25, "Changes the text font size.");
        AmmoTextAlign = Config.Bind("3 - Text", "Alignment", TextAnchor.MiddleCenter, "Where the text will Align");
        AmmoTotalCountConfig = Config.Bind("3 - Text", "Show Total Amount", true,
            "Shows the total amount of the currently equipped arrow/bolt you have on");
        AmmoTextColor1Config = Config.Bind("4 - Color", "Stage 1", new Color(1f, 1f, 1, 1f),
            "Stage 1 Color for 50 and above");
        AmmoTextStage1Config = Config.Bind("4 - Color", "Stage 1 Change Point", 49,
            "At what point it will go to the next stage");
        AmmoTextColor2Config = Config.Bind("4 - Color", "Stage 2", new Color(1f, 0.89f, 0.41f, 1f),
            "Stage 2 Color for 25 to 49");
        AmmoTextStage2Config = Config.Bind("4 - Color", "Stage 2 Change Point", 24,
            "At what point it will go to the next stage");
        AmmoTextColor3Config = Config.Bind("4 - Color", "Stage 3", new Color(1f, 0.64f, 0.41f, 1f),
            "Stage 3 Color for 6 to 24");
        AmmoTextStage3Config = Config.Bind("4 - Color", "Stage 3 Change Point", 5,
            "At what point it will go to the next stage");
        AmmoTextColor4Config = Config.Bind("4 - Color", "Stage 4", new Color(1f, 0.25f, 0.25f, 1f),
            "Stage 3 Color for 5 and below");
        AmmoNameColorConfig = Config.Bind("4 - Color", "Ammo Name", new Color(1f, 0.8482759f, 0f, 1f),
            "Color of the current ammo Name");


        var assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        SetupWatcher();
    }

    private void OnDestroy()
    {
        Config.Save();
    }

    /*[HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
    static class HudUpdatePatch
    {
        static void Postfix(Hud __instance)
        {
            var InstanceSend = __instance;
            
            
        } 
       
    }*/
    private static IEnumerator UpdateChangesConfig()
    {
        while (true)
        {
            var UpdateDelay = Math.Round(UpdateDelayConfig.Value * 10) / 10;
            UpdateDelayConfig.Value = (float)UpdateDelay;
            HudAwakePatch.ammoPanelRectTransform.sizeDelta = LocalScale.Value;
            HudAwakePatch.ammoPanelText.fontSize = AmmoTextSizeConfig.Value;
            HudAwakePatch.ammoPanelText.alignment = AmmoTextAlign.Value;
            try
            {
                var split = UIAnchor.Value.Split(',');
                UIAnchorDrag = new Vector2(
                    split[0].Trim().EndsWith("%")
                        ? float.Parse(split[0].Trim().Substring(0, split[0].Trim().Length - 1)) / 100f * Screen.width
                        : float.Parse(split[0].Trim()),
                    split[1].Trim().EndsWith("%")
                        ? float.Parse(split[1].Trim().Substring(0, split[1].Trim().Length - 1)) / 100f * Screen.height
                        : float.Parse(split[1].Trim()));
                HudAwakePatch.ammoPanelRectTransform.anchoredPosition = UIAnchorDrag;
            }
            catch
            {
            }

            yield return new WaitForSeconds(1);
        }
    }

    private static IEnumerator AmmoCountUpdateTrigger()
    {
        while (true)
        {
            AmmoUpdate();
            var UpdateDelay = Mathf.Round(UpdateDelayConfig.Value * 10) / 10;
            UpdateDelayConfig.Value = UpdateDelay;
            yield return new WaitForSeconds(UpdateDelay);
        }
    }

    private static void AmmoUpdate()
    {
        var player = Player.m_localPlayer;
        ItemDrop.ItemData ammoItem = null;
        if (player == null || player.IsDead() || Hud.instance == null) return;
        if (ammoItem != null && (!player.GetInventory().ContainsItem(ammoItem) ||
                                 ammoItem.m_shared.m_ammoType != player.GetCurrentWeapon().m_shared.m_ammoType))
            ammoItem = null;
        if (player.GetCurrentWeapon().m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow ||
            player.GetCurrentWeapon().m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft)
        {
            ammoItem ??= player.GetInventory().GetAmmoItem(player.GetCurrentWeapon().m_shared.m_ammoType);
            if (ammoItem == null)
            {
                HudAwakePatch.ammoPanelText.text = string.Empty;
            }
            else if (player.m_ammoItem != null)
            {
                //
                var ammoAmount = 0;
                if (AmmoTotalCountConfig.Value)
                {
                    foreach (var CurrentAmmo in player.GetInventory().GetAllItems())
                    {
                        if (player.m_ammoItem.m_shared.m_name == CurrentAmmo.m_shared.m_name)
                        {
                            ammoAmount += CurrentAmmo.m_stack;
                        }
                    }
                }
                else
                {
                    ammoAmount = Player.m_localPlayer.m_ammoItem.m_stack;
                }

                //
                try
                {
                    Color ammoAmountColor;
                    if (ammoAmount > AmmoTextStage1Config.Value)
                    {
                        ammoAmountColor = AmmoTextColor1Config.Value;
                    }
                    else if (ammoAmount > AmmoTextStage2Config.Value)
                    {
                        ammoAmountColor = AmmoTextColor2Config.Value;
                    }
                    else if (ammoAmount > AmmoTextStage3Config.Value)
                    {
                        ammoAmountColor = AmmoTextColor3Config.Value;
                    }
                    else
                    {
                        ammoAmountColor = AmmoTextColor4Config.Value;
                    }

                    //
                    // Player.m_localPlayer.m_ammoItem.GetIcon()
                    var ammoNameColored =
                        Helper.ColorString(
                            Localization.instance.Localize(Player.m_localPlayer.m_ammoItem.m_shared.m_name),
                            AmmoNameColorConfig.Value);
                    var ammoCountColoerd =
                        Helper.ColorString(Helper.FormatNumberSimple(ammoAmount), ammoAmountColor);
                    HudAwakePatch.ammoPanelText.text =
                        string.Format(AmmoTextConfig.Value, ammoCountColoerd, ammoNameColored);
                }
                catch
                {
                    HudAwakePatch.ammoPanelText.text = string.Empty;
                }
            }
        }
        else
        {
            HudAwakePatch.ammoPanelText.text = string.Empty;
        }
    }

    private void SetupWatcher()
    {
        FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
        watcher.Changed += ReadConfigValues;
        watcher.Created += ReadConfigValues;
        watcher.Renamed += ReadConfigValues;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(ConfigFileFullPath)) return;
        try
        {
            AmmoCountLogger.LogDebug("ReadConfigValues called");
            Config.Reload();
        }
        catch
        {
            AmmoCountLogger.LogError($"There was an issue loading your {ConfigFileName}");
            AmmoCountLogger.LogError("Please check your config entries for spelling and format!");
        }
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
    public static class HudAwakePatch
    {
        public static GameObject ammoPanel;
        public static Text ammoPanelText; // So we can update this anywhere and have the reference to the text component

        public static RectTransform
            ammoPanelRectTransform; // So we can update this anywhere and have the reference to the text component

        private static void Postfix(Hud __instance)
        {
            ammoPanel = new GameObject("MadBuffoonAmmoPanel");
            ammoPanel.transform.SetParent(__instance.m_rootObject.transform);
            ammoPanelRectTransform =
                ammoPanel
                    .AddComponent<
                        RectTransform>(); // If you want to save the position of the ammo panel or something in the future
            ammoPanel.AddComponent<DragControl>();
            ammoPanelRectTransform.transform.SetParent(ammoPanel.transform);
            //ammoPanel.transform.position = new Vector3(AmmoCountPlugin.UIAnchor.x, AmmoCountPlugin.UIAnchor.y, 0);

            // Make the ammo panel on the left middle of the screen
            ammoPanelRectTransform.sizeDelta = LocalScale.Value;
            ammoPanelText = ammoPanel.AddComponent<Text>();
            ammoPanelText.font = __instance.m_actionName.font;
            ammoPanelText.fontSize = AmmoTextSizeConfig.Value;
            ammoPanelText.text = "";

            //ammoPanelText.text = $"{AmmoName}: {ammoAmount}/{ammoTotal}";

            __instance.StartCoroutine(AmmoCountUpdateTrigger());
            __instance.StartCoroutine(UpdateChangesConfig());
        }
    }


    #region ConfigOptions

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;

    public static ConfigEntry<float> UpdateDelayConfig;

    public static ConfigEntry<string> UIAnchor = null!;
    public static ConfigEntry<Vector2> LocalScale = null!;

    public static ConfigEntry<string> AmmoTextConfig;
    public static ConfigEntry<int> AmmoTextSizeConfig;
    public static ConfigEntry<TextAnchor> AmmoTextAlign;
    public static ConfigEntry<bool> AmmoTotalCountConfig;

    public static ConfigEntry<Color> AmmoTextColor1Config;
    public static ConfigEntry<int> AmmoTextStage1Config;
    public static ConfigEntry<Color> AmmoTextColor2Config;
    public static ConfigEntry<int> AmmoTextStage2Config;
    public static ConfigEntry<Color> AmmoTextColor3Config;
    public static ConfigEntry<int> AmmoTextStage3Config;
    public static ConfigEntry<Color> AmmoTextColor4Config;

    public static ConfigEntry<Color> AmmoNameColorConfig;


    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
        bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription =
            new(
                description.Description +
                (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues, description.Tags);
        var configEntry = Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        var syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description,
        bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private class ConfigurationManagerAttributes
    {
        public bool? Browsable = false;
    }

    private class AcceptableShortcuts : AcceptableValueBase
    {
        public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
        {
        }

        public override object Clamp(object value)
        {
            return value;
        }

        public override bool IsValid(object value)
        {
            return true;
        }

        public override string ToDescriptionString()
        {
            return "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }
    }

    #endregion
}