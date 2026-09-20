using System;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace BigOrb;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency("IceBoxStudio.BigWalk.ModSettingsMenu", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public const string Guid = "AdamMady.ModerationImprovements";
    public const string Name = "Moderation Improvements";
    public const string Version = "1.0.2";

    internal static ManualLogSource Logger;
    internal static ConfigEntry<float> FlyMaxSpeed;
    internal static ConfigEntry<float> FlyMaxAirSeconds;
    internal static ConfigEntry<bool> FlyAutoKick;
    internal static ConfigEntry<bool> GuardAutoBan;
    internal static ConfigEntry<bool> GuardBanAnonymous;
    internal static ConfigEntry<int> GuardVoiceLimit;
    internal static ConfigEntry<bool> GuardChatFilter;
    internal static ConfigEntry<bool> ChimeEnabled;
    internal static ConfigEntry<float> ChimeVolume;
    internal static ConfigEntry<KeyCode> MenuKey;
    private static ConfigFile InternalConfig;



    public override void Load()
    {
        Logger = Log;
        MenuKey = Config.Bind("Controls", "MenuKey", KeyCode.L, "Open or close moderation menu.");
        InternalConfig = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "ModerationImprovements.Internal.cfg"), true);
        FlyMaxSpeed = InternalConfig.Bind("anticheat", "maxSpeed", 16f, "Sustained horizontal m/s before a player is flagged");
        FlyMaxAirSeconds = InternalConfig.Bind("anticheat", "maxAirSeconds", 6f, "Seconds of steady climbing before a player is flagged");
        FlyAutoKick = InternalConfig.Bind("anticheat", "autoKick", false, "Automatically kick flagged fly/speed cheaters (off = report only)");

        GuardAutoBan = InternalConfig.Bind("guard", "autoBan", true, "Address-ban a connection that is proven to be a modded client (spoofed identity, voice flood). Off = alert and disconnect only");
        GuardBanAnonymous = InternalConfig.Bind("guard", "banAnonymousLogins", true, "Treat an EOS account with no linked Steam/PSN/Xbox account (anonymous device-id login) as a modded client");
        GuardVoiceLimit = InternalConfig.Bind("guard", "voicePacketsPerSecond", 120, "Voice packets per second a connection may send before the rest are dropped (a talking player sends ~50). 0 = off");
        GuardChatFilter = InternalConfig.Bind("guard", "dropFakeSystemChat", true, "Drop guest chat that imitates a system message (\"host has been removed\" etc.)");
        ChimeEnabled = InternalConfig.Bind("chime", "enabled", true, "Play a local chime when players join or leave.");
        ChimeVolume = InternalConfig.Bind("chime", "volume", .45f, new ConfigDescription("Join/leave chime volume.", new AcceptableValueRange<float>(0f, 1f)));

        OrbState.Init();

        ClassInjector.RegisterTypeInIl2Cpp<OrbBehaviour>();
        var go = new GameObject("BigOrbHolder");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<OrbBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<OrbMenu>();
        go.AddComponent<OrbMenu>();

        TryRegisterSettings();

        var harmony = new Harmony(Guid);
        Patches.PatchAllSafe(harmony);
        Guard.Voice.Patch(harmony);
        Guard.Eos.Load();
        Logger.LogInfo(Name + " loaded. Press " + MenuKey.Value + " for the in-game menu.");
    }

    private static void TryRegisterSettings()
    {
        try
        {
            var optionsType = Type.GetType("ModSettingsMenu.Api.ModSettingsModOptions, ModSettingsMenu", false);
            var registryType = Type.GetType("ModSettingsMenu.Api.ModSettingsRegistry, ModSettingsMenu", false);
            if (optionsType == null || registryType == null) return;
            var options = Activator.CreateInstance(optionsType);
            optionsType.GetProperty("Name")?.SetValue(options, Name);
            optionsType.GetProperty("Author")?.SetValue(options, "AdamMady");
            optionsType.GetProperty("Version")?.SetValue(options, Version);
            registryType.GetMethod("Register", new[] { typeof(string), optionsType })?.Invoke(null, new[] { (object)Guid, options });
        }
        catch (Exception ex) { Logger.LogWarning("Mod Settings registration failed: " + ex.GetBaseException().Message); }
    }
}
