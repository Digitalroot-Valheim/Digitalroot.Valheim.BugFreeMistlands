using Digitalroot.Modding.Framework.Common.Attributes;
using Digitalroot.Modding.Framework.Logging;
using Digitalroot.Modding.Framework.Names.Vanilla;
using HarmonyLib;
using JetBrains.Annotations;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using UnityEngine;

namespace Digitalroot.Valheim.BugFreeMistlands
{
  [UsedImplicitly]
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  public class Patch
  {
    #region SpawnSystem

    [HarmonyPatch(typeof(SpawnSystem)), AiInfo(AiInfoMetaData.None)]
    public class PatchSpawnSystem
    {
      [HarmonyPostfix, HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Awake))]
      // ReSharper disable once IdentifierTypo
      private static void PostfixAwake(SpawnSystem __instance, ZNetView ___m_nview, List<SpawnSystemList> ___m_spawnLists)
      {
        try
        {
          if (___m_nview == null || !___m_nview.IsValid()) return;
          // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{__instance.name}] {___m_spawnLists.Count}");
          if (___m_spawnLists.Count > 0)
          {
            foreach (var spawnSystemList in ___m_spawnLists)
            {
              // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{spawnSystemList.name}] {spawnSystemList.m_spawners.Count}");
              switch (spawnSystemList.name)
              {
                case "_SpawnList_mistlands":
                  foreach (var spawnData in spawnSystemList.m_spawners)
                  {
                    // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{spawnData.m_name}] {spawnData.m_prefab.name}:{spawnData.m_biome}:{spawnData.m_enabled}");
                    switch (spawnData.m_prefab.name)
                    {
                      case nameof(EnemyNames.Seeker):
                      case nameof(EnemyNames.Gjall):
                      case nameof(EnemyNames.SeekerBrute):
                      case nameof(EnemyNames.SeekerBrood):
                      case nameof(EnemyNames.Tick):
                        if (spawnData.m_enabled)
                        {
                          spawnData.m_enabled = false;
                          // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{spawnData.m_name}] {spawnData.m_prefab.name}:{spawnData.m_biome}:{spawnData.m_enabled}");
                        }

                        break;
                    }
                  }

                  break;
              }
            }
          }
        }
        catch (Exception e)
        {
          Log.Error(Main.Instance, e);
        }
      }
    }

    #endregion

    #region CreatureSpawner

    [HarmonyPatch(typeof(CreatureSpawner)), AiInfo(AiInfoMetaData.None)]
    public class PatchCreatureSpawner
    {
      [HarmonyPostfix, HarmonyPatch(typeof(CreatureSpawner), nameof(CreatureSpawner.Awake))]
      // ReSharper disable once IdentifierTypo
      private static void PostfixAwake(CreatureSpawner __instance, ZNetView ___m_nview, ref GameObject ___m_creaturePrefab)
      {
        try
        {
          if (___m_nview == null || !___m_nview.IsValid() || ___m_creaturePrefab == null) return;
          // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{__instance.name}] {___m_creaturePrefab.name}");
          switch (___m_creaturePrefab.name)
          {
            case nameof(EnemyNames.Seeker):
              var dverger = CreatureManager.Instance.GetCreaturePrefab(Main.EvilDvergerPreFabName);
              if (dverger != null) ___m_creaturePrefab = dverger;
              break;

            case nameof(EnemyNames.Gjall):
            case nameof(EnemyNames.SeekerBrute):
              var mage = CreatureManager.Instance.GetCreaturePrefab(Main.EvilDvergerMagePreFabName);
              if (mage != null) ___m_creaturePrefab = mage;
              break;

            case nameof(EnemyNames.SeekerBrood):
            case nameof(EnemyNames.Tick):
              break;

            case nameof(BossNames.SeekerQueen):
              if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{__instance.name}] {___m_creaturePrefab.name}");
              // ___m_creaturePrefab = CreatureManager.Instance.GetCreaturePrefab(BossNames.Bonemass);
              break;
          }
        }
        catch (Exception e)
        {
          Log.Error(Main.Instance, e);
        }
      }

      [HarmonyPrefix, HarmonyPatch(typeof(CreatureSpawner), nameof(CreatureSpawner.Spawn))]
      private static bool PrefixSpawn(CreatureSpawner __instance, ZNetView ___m_nview, GameObject ___m_creaturePrefab)
      {
        try
        {
          if (___m_nview == null || !___m_nview.IsValid() || ___m_creaturePrefab == null) return true;

          switch (___m_creaturePrefab.name)
          {
            case nameof(EnemyNames.Seeker):
            case nameof(EnemyNames.Gjall):
            case nameof(EnemyNames.SeekerBrute):
            case nameof(EnemyNames.SeekerBrood):
            case nameof(EnemyNames.Tick):
            case nameof(BossNames.SeekerQueen):
              // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{__instance.name}] {___m_creaturePrefab.name}");
              return false;
          }
        }
        catch (Exception e)
        {
          Log.Error(Main.Instance, e);
        }

        return true;
      }
    }

    #endregion

    #region Humanoid

    [HarmonyPatch(typeof(Humanoid)), AiInfo(AiInfoMetaData.None)]
    public class PatchHumanoid
    {
      [HarmonyPostfix, HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Awake))]
      // ReSharper disable once IdentifierTypo
      private static void PostfixAwake(Humanoid __instance, ZNetView ___m_nview)
      {
        try
        {
          if (___m_nview == null || !___m_nview.IsValid() || __instance.IsPlayer()) return;

          // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{__instance.GetName()}] {__instance.m_name}:{__instance.name}");
          switch (__instance.m_name)
          {
            case "$enemy_seeker":
            case "$enemy_tick":
            case "$enemy_gjall":
            case "$enemy_babyseeker":
            case "$enemy_seekerbrute":
              if (!___m_nview.HasOwner())
              {
                ___m_nview.ClaimOwnership();
              }

              if (___m_nview.IsOwner())
              {
                ZNetScene.instance.Destroy(__instance.gameObject);
              }

              break;

            case "$enemy_seekerqueen":
              if (!___m_nview.HasOwner())
              {
                ___m_nview.ClaimOwnership();
              }

              // if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{__instance.GetName()}] {__instance.m_name}:{__instance.name}");
              Main.Instance.StartCoroutine(Main.ReplaceSeekerQueen(__instance, ___m_nview));
              break;
          }
        }
        catch (Exception e)
        {
          Log.Error(Main.Instance, e);
        }
      }
    }

    #endregion

    #region OfferingBowl

    [HarmonyPatch(typeof(OfferingBowl)), AiInfo(AiInfoMetaData.Ai_Assisted)]
    public class PatchOfferingBowl
    {
      [HarmonyPostfix, HarmonyPatch(typeof(OfferingBowl), nameof(OfferingBowl.Awake))]
      // ReSharper disable once IdentifierTypo
      private static void PostfixAwake(OfferingBowl __instance)
      {
        try
        {
          if (__instance.m_bossPrefab == null) return;
          switch (__instance.m_bossPrefab.name)
          {
            case nameof(BossNames.SeekerQueen):

              var replacement = CreatureManager.Instance.GetCreaturePrefab(Main.EvilBonemassPreFabName);
              if (replacement == null)
              {
                Log.Error(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} {Main.EvilBonemassPreFabName} is not registered; leaving {BossNames.SeekerQueen} on {__instance.name}");
                break;
              }

              if (Main.Instance.EnableTrace) Log.Trace(Main.Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{__instance.GetName()}] Replacing '{BossNames.SeekerQueen}' with '{Main.EvilBonemassPreFabName}'");
              __instance.m_bossPrefab = replacement;
              break;
          }
        }
        catch (Exception e)
        {
          Log.Error(Main.Instance, e);
        }
      }
    }

    #endregion
  }
}
