using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using DrakiaXYZ.VersionChecker;
using EFT;
using HarmonyLib;
using SAIN.Components;
using SAIN.Editor;
using SAIN.Helpers;
using SAIN.Patches.Generic;
using SAIN.Patches.Hearing;
using SAIN.Patches.Vision;
using SAIN.Plugin;
using SAIN.Preset;
using SAIN.Preset.GlobalSettings;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static SAIN.AssemblyInfoClass;

namespace SAIN
{
    [BepInPlugin(SAINGUID, SAINName, SAINVersion)]
    [BepInDependency(BigBrainGUID, BigBrainVersion)]
    [BepInDependency(WaypointsGUID, WaypointsVersion)]
    [BepInDependency(SPTGUID, SPTVersion)]
    [BepInProcess(EscapeFromTarkov)]
    [BepInIncompatibility("com.dvize.BushNoESP")]
    [BepInIncompatibility("com.dvize.NoGrenadeESP")]
    public class SAINPlugin : BaseUnityPlugin
    {
        public static DebugSettings DebugSettings => LoadedPreset.GlobalSettings.Debug;
        public static bool DebugMode => DebugSettings.GlobalDebugMode;
        public static bool DrawDebugGizmos => DebugSettings.DrawDebugGizmos;
        public static PresetEditorDefaults EditorDefaults => PresetHandler.EditorDefaults;

        public static SoloDecision ForceSoloDecision = SoloDecision.None;

        public static SquadDecision ForceSquadDecision = SquadDecision.None;

        public static SelfDecision ForceSelfDecision = SelfDecision.None;

        private void Awake()
        {
            if (!VersionChecker.CheckEftVersion(Logger, Info, Config))
            {
                throw new Exception("Invalid EFT Version");
            }

            //new DefaultBrainsClass();

            PresetHandler.Init();
            BindConfigs();
            Patches();
            BigBrainHandler.Init();
            Vector.Init();
        }

        private void BindConfigs()
        {
            string category = "SAIN Editor";

            NextDebugOverlay = Config.Bind(category, "Next Debug Overlay", new KeyboardShortcut(KeyCode.LeftBracket), "Change The Debug Overlay with DrakiaXYZs Debug Overlay");
            PreviousDebugOverlay = Config.Bind(category, "Previous Debug Overlay", new KeyboardShortcut(KeyCode.RightBracket), "Change The Debug Overlay with DrakiaXYZs Debug Overlay");

            OpenEditorButton = Config.Bind(category, "Open Editor", false, "Opens the Editor on press");
            OpenEditorConfigEntry = Config.Bind(category, "Open Editor Shortcut", new KeyboardShortcut(KeyCode.F6), "The keyboard shortcut that toggles editor");
        }

        public static ConfigEntry<KeyboardShortcut> NextDebugOverlay { get; private set; }
        public static ConfigEntry<KeyboardShortcut> PreviousDebugOverlay { get; private set; }
        public static ConfigEntry<bool> OpenEditorButton { get; private set; }
        public static ConfigEntry<KeyboardShortcut> OpenEditorConfigEntry { get; private set; }

        private void Patches()
        {
            var patches = new List<Type>() {
                //typeof(Patches.Generic.ShallRunAwayGrenadePatch),
                //typeof(Patches.Generic.DisableLookSensorPatch),

                typeof(Patches.Generic.SetEnvironmentPatch),
                typeof(Patches.Generic.FixItemTakerPatch),
                typeof(Patches.Generic.FixItemTakerPatch2),
                typeof(Patches.Generic.FixPatrolDataPatch),
                typeof(Patches.Generic.SetPanicPointPatch),
                typeof(Patches.Generic.AddPointToSearchPatch),
                typeof(Patches.Generic.HaveSeenEnemyPatch),
                typeof(Patches.Generic.StopSetToNavMeshPatch),
                typeof(Patches.Generic.TurnDamnLightOffPatch),
                typeof(Patches.Generic.RotateClampPatch),
                typeof(Patches.Generic.HealCancelPatch),
                typeof(Patches.Generic.GetBotController),
                typeof(Patches.Generic.GetBotSpawner),
                typeof(Patches.Generic.GrenadeThrownActionPatch),
                typeof(Patches.Generic.GrenadeExplosionActionPatch),
                typeof(Patches.Generic.AimRotateSpeedPatch),
                typeof(Patches.Generic.OnMakingShotRecoilPatch),
                typeof(Patches.Generic.BotGroupAddEnemyPatch),
                typeof(Patches.Generic.ForceNoHeadAimPatch),
                typeof(Patches.Generic.NoTeleportPatch),
                typeof(Patches.Generic.ShallKnowEnemyPatch),
                typeof(Patches.Generic.ShallKnowEnemyLatePatch),
                //typeof(Patches.Generic.SkipLookForCoverPatch),
                typeof(Patches.Generic.BotMemoryAddEnemyPatch),

                //typeof(Patches.Generic.SteeringPatch),
                typeof(Patches.Generic.EncumberedPatch),
                typeof(Patches.Generic.InBunkerPatch),
                typeof(Patches.Generic.DoorOpenerPatch),

                typeof(Patches.Hearing.TryPlayShootSoundPatch),
                typeof(Patches.Hearing.OnMakingShotPatch),
                typeof(Patches.Hearing.HearingSensorPatch),

                typeof(Patches.Hearing.BulletImpactPatch),
                //typeof(Patches.Generic.BulletImpactPatch2),
                typeof(Patches.Hearing.TreeSoundPatch),
                typeof(Patches.Hearing.DoorBreachSoundPatch),
                typeof(Patches.Hearing.DoorOpenSoundPatch),
                typeof(Patches.Hearing.FootstepSoundPatch),
                typeof(Patches.Hearing.JumpSoundPatch),
                typeof(Patches.Hearing.DryShotPatch),
                typeof(Patches.Hearing.FallSoundPatch),
                typeof(Patches.Hearing.TurnSoundPatch),
                typeof(Patches.Hearing.ProneSoundPatch),
                typeof(Patches.Hearing.SoundClipNameCheckerPatch),
                typeof(Patches.Hearing.AimSoundPatch),
                typeof(Patches.Hearing.LootingSoundPatch),
                typeof(Patches.Hearing.SetInHandsGrenadePatch),
                typeof(Patches.Hearing.SetInHandsFoodPatch),
                typeof(Patches.Hearing.SetInHandsMedsPatch),

                typeof(Patches.Talk.JumpPainPatch),
                typeof(Patches.Talk.PlayerHurtPatch),
                typeof(Patches.Talk.PlayerTalkPatch),
                typeof(Patches.Talk.BotTalkPatch),
                typeof(Patches.Talk.BotTalkManualUpdatePatch),

                typeof(Patches.Vision.DisableLookUpdatePatch),
                typeof(Patches.Vision.SetPartPriorityPatch),
                typeof(Patches.Vision.GlobalLookSettingsPatch),
                typeof(Patches.Vision.WeatherTimeVisibleDistancePatch),
                typeof(Patches.Vision.NoAIESPPatch),
                typeof(Patches.Vision.BotLightTurnOnPatch),
                typeof(Patches.Vision.VisionSpeedPatch),
                typeof(Patches.Vision.VisionDistancePatch),
                typeof(Patches.Vision.CheckFlashlightPatch),

                typeof(Patches.Shoot.Aim.AimOffsetPatch),
                typeof(Patches.Shoot.Aim.AimTimePatch),
                typeof(Patches.Shoot.Aim.ScatterPatch),
                typeof(Patches.Shoot.Aim.WeaponPresetPatch),

                typeof(Patches.Shoot.Recoil.RecoilPatch),
                typeof(Patches.Shoot.Recoil.LoseRecoilPatch),
                typeof(Patches.Shoot.Recoil.EndRecoilPatch),
                typeof(Patches.Shoot.RateOfFire.FullAutoPatch),
                typeof(Patches.Shoot.RateOfFire.SemiAutoPatch),
                typeof(Patches.Shoot.RateOfFire.SemiAutoPatch2),
                typeof(Patches.Shoot.RateOfFire.SemiAutoPatch3),

                typeof(Patches.Components.AddComponentPatch),
                typeof(Patches.Components.AddGameWorldPatch),
            };

            // Reflection go brrrrrrrrrrrrrr
            MethodInfo enableMethod = AccessTools.Method(typeof(ModulePatch), "Enable");
            foreach (var patch in patches)
            {
                if (!typeof(ModulePatch).IsAssignableFrom(patch))
                {
                    Logger.LogError($"Type {patch.Name} is not a ModulePatch");
                    continue;
                }

                try
                {
                    enableMethod.Invoke(Activator.CreateInstance(patch), null);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }
            }
        }

        public static SAINPresetClass LoadedPreset => PresetHandler.LoadedPreset;

        public static SAINBotController BotController => GameWorldHandler.SAINBotController;

        private void Update()
        {
            ModDetection.Update();
            SAINEditor.Update();
        }

        private void Start() => SAINEditor.Init();

        private void LateUpdate() => SAINEditor.LateUpdate();

        private void OnGUI() => SAINEditor.OnGUI();

        public static bool IsBotExluded(BotOwner botOwner) => SAINEnableClass.IsSAINDisabledForBot(botOwner);
    }
}
