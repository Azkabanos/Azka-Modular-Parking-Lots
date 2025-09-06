using ICities;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ColossalFramework.UI;
using CitiesHarmony.API;
using HarmonyLib;

namespace AzkaModularParkingLots
{
    internal static class AMPL
    {
        public const string ModName = "Azka Modular Parking Lots";
        public const string Description = "Dependency of AMPL assets.";
        public const string HarmonyId = "com.azka.ampl";

        public static void Log(string msg) => Debug.Log($"[{ModName}] {msg}");
    }

    // ------------------------------------------------------------
    // IUserMod + Loading
    // ------------------------------------------------------------
    public class Mod : IUserMod
    {
        public string Name => AMPL.ModName;
        public string Description => AMPL.Description;

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(Patcher.PatchAll);

            try
            {
                var exceptionPanel = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                exceptionPanel.SetMessage(
                    AMPL.ModName,
                    "Game restart is required after turning on this mod for it to work properly.",
                    false);
                AMPL.Log("Game restart is required after turning on this mod for it to work properly.");
            }
            catch { }
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled) Patcher.UnpatchAll();
        }
    }

    // ------------------------------------------------------------
    // RequireElectric Applier
    // ------------------------------------------------------------
    public static class RequireElectricApplier
    {
        private const string TargetPrefabName = "3448781215.ARM W P E_Data";
        private static bool _applied = false;

        public static void ApplyRequireElectric(LoadMode mode)
        {
            if (_applied || (mode != LoadMode.NewGame && mode != LoadMode.LoadGame)) return;

            try
            {
                PropInfo prop = PrefabCollection<PropInfo>.FindLoaded(TargetPrefabName);
                if (prop == null)
                {
                    AMPL.Log($"Failed to set m_flag = RequireElectric: prefab {TargetPrefabName} not found.");
                    return;
                }

                if (prop.m_parkingSpaces == null || prop.m_parkingSpaces.Length < 2)
                {
                    AMPL.Log($"Failed to set m_flag = RequireElectric: asset {prop.name} has invalid parking space data.");
                    return;
                }

                for (int j = 0; j < 2; j++)
                {
                    var ps = prop.m_parkingSpaces[j];
                    ps.m_flags |= PropInfo.ParkingFlags.RequireElectric;
                    prop.m_parkingSpaces[j] = ps;
                }

                AMPL.Log($"m_flag = RequireElectric set for parking spaces in {prop.name}.");
                _applied = true;
            }
            catch (Exception e)
            {
                AMPL.Log("Exception while setting m_flag = RequireElectric for parking spaces: " + e);
            }
        }
    }

    public class LoadingExt : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode) => RequireElectricApplier.ApplyRequireElectric(mode);
    }

    // ------------------------------------------------------------
    // PlacementMode Changer
    // ------------------------------------------------------------
    [HarmonyPatch]
    public static class PlacementModeChanger
    {
        private static readonly HashSet<string> TargetAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "3563113487.AMPL Pavement Normal_Data",
            "3563113487.AMPL Pavement Disabled_Data",
            "3563113487.AMPL Pavement Custom_Data",
            "3563113487.AMPL Pavement Electric_Data",
            "3563113487.AMPL Gravel_Data"
        };

        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BuildingTool), nameof(BuildingTool.SimulationStep));
            yield return AccessTools.Method(typeof(BuildingTool), "CreateBuilding");
            yield return AccessTools.Method(typeof(BuildingAI), nameof(BuildingAI.CheckBuildPosition));
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo m_placementMode = AccessTools.Field(typeof(BuildingInfo), nameof(BuildingInfo.m_placementMode));
            MethodInfo getPlacementMode = AccessTools.Method(typeof(PlacementModeChanger), nameof(GetPlacementMode));

            foreach (var instruction in instructions)
            {
                if (instruction.LoadsField(m_placementMode))
                {
                    yield return new CodeInstruction(OpCodes.Call, getPlacementMode);
                }
                else yield return instruction;
            }
        }

        private static BuildingInfo.PlacementMode GetPlacementMode(BuildingInfo buildingInfo)
        {
            if (buildingInfo == null) return BuildingInfo.PlacementMode.OnGround;
            return TargetAssets.Contains(buildingInfo.name)
                ? BuildingInfo.PlacementMode.Roadside
                : buildingInfo.m_placementMode;
        }
    }

    // ------------------------------------------------------------
    // Patcher
    // ------------------------------------------------------------
    public static class Patcher
    {
        private static bool patched = false;

        public static void PatchAll()
        {
            if (patched) return;
            patched = true;

            try
            {
                var harmony = new Harmony(AMPL.HarmonyId);
                harmony.PatchAll(typeof(Patcher).Assembly);
            }
            catch (Exception e)
            {
                AMPL.Log("Harmony PatchAll failed: " + e);
            }
        }

        public static void UnpatchAll()
        {
            if (!patched) return;
            try
            {
                var harmony = new Harmony(AMPL.HarmonyId);
                harmony.UnpatchAll(AMPL.HarmonyId);
            }
            catch (Exception e)
            {
                AMPL.Log("Harmony UnpatchAll failed: " + e);
            }
            patched = false;
        }
    }
}