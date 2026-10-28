using BepInEx;
using BepInEx.Configuration;
using Digitalroot.Modding.Framework.Common.Attributes;
using Digitalroot.Modding.Framework.Names.Vanilla;
using Digitalroot.Modding.Framework.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Digitalroot.Valheim.BugFreeMistlands
{
  [BepInPlugin(Guid, Name, Version)]
  [BepInDependency(Jotunn.Main.ModGuid)]
  [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
  [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
  public partial class Main : BaseUnityPlugin, ITraceableLogging
  {
    #region Configs and Local vars

    private Harmony _harmony;
    public static Main Instance { get; private set; }

    [UsedImplicitly]
    public static ConfigEntry<int> NexusId { get; private set; }

    public const string EvilDvergerPreFabName = "bfml_EvilDverger";
    public const string EvilDvergerMagePreFabName = "bfml_EvilDvergerMage";
    public const string EvilBonemassPreFabName = "bfml_EvilBonemass";
    public const string QueenAltarName = "offeraltar_queen";

    private static readonly string[] EvilBonemassParticlePaths =
    {
      "Visual/particles/ooz"
      , "Visual/particles/wetsplsh"
      , "Visual/particles/wetsplsh (1)"
    };

    private const string EvilBonemassLightPath = "Visual/Point light";

    private const string EvilBonemassHeadsPath = "Visual/model/heads";

    private const string EvilBonemassBodyPath = "Visual/model/bonemass";

    private const string EvilBonemassMudMaterialName = "Bonemass_mud";

    #endregion

    public Main()
    {
      Instance = this;
      #if DEBUG
      EnableTrace = true;
      Log.RegisterSource(Instance); // Enable logging to a './BepInEx/logs' file.
      Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
      #else
      EnableTrace = false;
      #endif
    }

    [UsedImplicitly]
    private void Awake()
    {
      try
      {
        if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        NexusId = Config.Bind(PluginConfigSection.General, "NexusID", 0000, new ConfigDescription("Nexus mod ID for updates", null, new ConfigurationManagerAttributes { Browsable = false, ReadOnly = true }));
        SynchronizationManager.OnConfigurationSynchronized += SynchronizationManager_OnConfigurationSynchronized;
        CreatureManager.OnVanillaCreaturesAvailable += CreatureManager_OnVanillaCreaturesAvailable;
        _harmony = Harmony.CreateAndPatchAll(typeof(Main).Assembly, Guid);
      }
      catch (Exception e)
      {
        Log.Error(Instance, e);
      }
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
      try
      {
        if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        _harmony?.UnpatchSelf();
        SynchronizationManager.OnConfigurationSynchronized -= SynchronizationManager_OnConfigurationSynchronized;
        CreatureManager.OnVanillaCreaturesAvailable -= CreatureManager_OnVanillaCreaturesAvailable;
      }
      catch (Exception e)
      {
        Log.Error(Instance, e);
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Assisted)]
    private CustomCreature CreateEvilBonemassPrefab()
    {
      var clonedBonemassPrefab = PrefabManager.Instance.CreateClonedPrefab(EvilBonemassPreFabName, BossNames.Bonemass);
      var seekerQueenPrefab = PrefabManager.Cache.GetPrefab<GameObject>(BossNames.SeekerQueen);
      var queenCharacter = seekerQueenPrefab.GetComponent<Character>();
      var bonemassHumanoid = clonedBonemassPrefab.GetComponent<Humanoid>();

      bonemassHumanoid.m_boss = queenCharacter.m_boss;
      bonemassHumanoid.m_bossEvent = queenCharacter.m_bossEvent;
      bonemassHumanoid.m_defeatSetGlobalKey = queenCharacter.m_defeatSetGlobalKey;

      if (!clonedBonemassPrefab.TryGetComponent(out CharacterDrop drop))
      {
        drop = clonedBonemassPrefab.AddComponent<CharacterDrop>();
      }

      drop.m_drops = seekerQueenPrefab.GetComponent<CharacterDrop>().m_drops
                                      .Select(d => new CharacterDrop.Drop
                                      {
                                        m_prefab = d.m_prefab
                                        , m_amountMin = d.m_amountMin
                                        , m_amountMax = d.m_amountMax
                                        , m_chance = d.m_chance
                                        , m_onePerPlayer = d.m_onePerPlayer
                                        , m_levelMultiplier = d.m_levelMultiplier
                                        , m_dontScale = d.m_dontScale
                                      })
                                      .ToList();

      bonemassHumanoid.m_health = queenCharacter.m_health; // Copy HP

      // Recolor
      MakeBonemassEvil(clonedBonemassPrefab, Color.red);
      RecolorBonemassBody(clonedBonemassPrefab, Color.red);
      ShrinkBonemass(clonedBonemassPrefab, 0.5f);
      clonedBonemassPrefab.GetComponent<BaseAI>().m_pathAgentType = Pathfinding.AgentType.TrollSize;

      return new CustomCreature(clonedBonemassPrefab
                                , true
                                , new CreatureConfig
                                {
                                  Name = "$bfml_evil_bonemass"
                                  , Faction = Character.Faction.Boss
                                });
    }

    private CustomCreature CreateEvilDvergerPrefab()
    {
      var clonedPrefab = PrefabManager.Instance.CreateClonedPrefab(EvilDvergerPreFabName, EnemyNames.Dverger);

      SetEyeColorToEvil(clonedPrefab);

      return new CustomCreature(clonedPrefab, true, new CreatureConfig
      {
        Name = "$bfml_evil_dverger"
        , Faction = Character.Faction.MistlandsMonsters
        , UseCumulativeLevelEffects = true
        , Group = string.Empty
        , DropConfigs = new[]
        {
          new DropConfig
          {
            Chance = 80f
            , Item = ItemDropNames.Carapace
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 100f
            , Item = ItemDropNames.BugMeat
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 5f
            , Item = ItemDropNames.TrophySeeker
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 50f
            , Item = ItemDropNames.Coins
            , LevelMultiplier = false
            , MinAmount = 10
            , MaxAmount = 25
          }
          , new DropConfig
          {
            Chance = 5f
            , Item = ItemDropNames.TrophyTick
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 50f
            , Item = ItemDropNames.GiantBloodSack
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 10f
            , Item = ItemDropNames.RoyalJelly
            , LevelMultiplier = false
            , MaxAmount = 1
          }
        }
        , SpawnConfigs = new[]
        {
          new SpawnConfig
          {
            Biome = Heightmap.Biome.Mistlands
            , BiomeArea = Heightmap.BiomeArea.Median
            , HuntPlayer = false
            , MaxSpawned = 2
            , Name = "evil_dverger_spawn"
          }
        }
      });
    }

    private CustomCreature CreateEvilDvergerMagePrefab()
    {
      var clonedPrefab = PrefabManager.Instance.CreateClonedPrefab(EvilDvergerMagePreFabName, EnemyNames.DvergerMage);
      SetEyeColorToEvil(clonedPrefab);
      // m_defaultItems : 0
      // m_randomWeapon : 0
      // m_randomArmor : 0
      // m_randomShield : Male/Female Hair
      // m_randomSets : Fire/Ice/Healer
      //  Fire: Dverger_melee, DvergerStaffFire_fireball, DvergerStaffFire, DvergerSuitFire, DvergerStaffFire_clusterbomb
      //  Ice: Dverger_melee, DvergerStaffIce_icebolt, DvergerStaffIce, DvergerSuitIce, DvergerStaffNova
      //  Healer: DvergerStaffSupport_buff, DvergerStaffHeal_heal, DvergerStaffHeal, DvergerSuitSupport, DvergerMistile
      // m_randomItems : 0

      return new CustomCreature(clonedPrefab, true, new CreatureConfig
      {
        Name = "$bfml_evil_dverger_mage"
        , Faction = Character.Faction.MistlandsMonsters
        , UseCumulativeLevelEffects = true
        , Group = string.Empty
        , DropConfigs = new[]
        {
          new DropConfig
          {
            Chance = 90f
            , Item = ItemDropNames.Carapace
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 100f
            , Item = ItemDropNames.BugMeat
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 85f
            , Item = ItemDropNames.Mandible
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 5f
            , Item = ItemDropNames.TrophySeekerBrute
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 5f
            , Item = ItemDropNames.TrophyGjall
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 50f
            , Item = ItemDropNames.Coins
            , LevelMultiplier = false
            , MinAmount = 10
            , MaxAmount = 30
          }
          , new DropConfig
          {
            Chance = 50f
            , Item = ItemDropNames.Bilebag
            , LevelMultiplier = false
            , MaxAmount = 1
          }
          , new DropConfig
          {
            Chance = 10f
            , Item = ItemDropNames.RoyalJelly
            , LevelMultiplier = false
            , MaxAmount = 2
          }
        }
        , SpawnConfigs = new[]
        {
          new SpawnConfig
          {
            Biome = Heightmap.Biome.Mistlands
            , BiomeArea = Heightmap.BiomeArea.Median
            , HuntPlayer = false
            , MaxSpawned = 1
            , Name = "evil_dverger_mage_spawn"
          }
        }
      });
    }

    #region Helper Methods

    private void CreatureManager_OnVanillaCreaturesAvailable()
    {
      try
      {
        if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        CreatureManager.Instance.AddCreature(CreateEvilDvergerPrefab());
        CreatureManager.Instance.AddCreature(CreateEvilDvergerMagePrefab());
        CreatureManager.Instance.AddCreature(CreateEvilBonemassPrefab());
      }
      catch (Exception e)
      {
        Log.Error(Instance, e);
      }
      finally
      {
        CreatureManager.OnVanillaCreaturesAvailable -= CreatureManager_OnVanillaCreaturesAvailable;
      }
    }
    
    [AiInfo(AiInfoMetaData.Ai_Generated)]
    public static OfferingBowl FindClosestQueenAltar(Vector3 position, float maxDistance = 100f)
    {
      if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
      OfferingBowl closest = null;
      var best = maxDistance * maxDistance;

      foreach (var bowl in FindObjectsByType<OfferingBowl>(FindObjectsSortMode.None))
      {
        if (bowl.name != QueenAltarName) continue;
        var magnitude = (bowl.transform.position - position).sqrMagnitude;
        if (magnitude < best)
        {
          best = magnitude;
          closest = bowl;
        }
      }

      if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name} [{closest?.name ?? "Null"}]");
      return closest;
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static void MakeBonemassEvil(GameObject prefab, Color color)
    {
      try
      {
        var root = prefab.transform;

        foreach (var path in EvilBonemassParticlePaths)
        {
          var target = root.Find(path);
          if (target == null)
          {
            Log.Warning(Instance, $"{nameof(Main)}.{nameof(MakeBonemassEvil)} [{prefab.name}] missing '{path}'");
            continue;
          }

          foreach (var ps in target.GetComponentsInChildren<ParticleSystem>(true))
          {
            TintParticleSystem(ps, color);
          }
        }

        var lightTransform = root.Find(EvilBonemassLightPath);
        if (lightTransform != null && lightTransform.TryGetComponent(out Light light))
        {
          light.color = color;
        }
        else
        {
          Log.Warning(Instance, $"{nameof(Main)}.{nameof(MakeBonemassEvil)} [{prefab.name}] missing Light on '{EvilBonemassLightPath}'");
        }

        var heads = root.Find(EvilBonemassHeadsPath);
        if (heads != null)
        {
          heads.gameObject.SetActive(false);
        }
        else
        {
          Log.Warning(Instance, $"{nameof(Main)}.{nameof(MakeBonemassEvil)} [{prefab.name}] missing '{EvilBonemassHeadsPath}'");
        }
      }
      catch (Exception ex)
      {
        Log.Error(Instance, ex);
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static ParticleSystem.MinMaxGradient Recolor(ParticleSystem.MinMaxGradient source, Color color)
    {
      switch (source.mode)
      {
        case ParticleSystemGradientMode.Color:
          return new ParticleSystem.MinMaxGradient(Tint(source.color, color));

        case ParticleSystemGradientMode.TwoColors:
          return new ParticleSystem.MinMaxGradient(Tint(source.colorMin, color), Tint(source.colorMax, color));

        case ParticleSystemGradientMode.Gradient:
          return new ParticleSystem.MinMaxGradient(Recolor(source.gradient, color));

        case ParticleSystemGradientMode.TwoGradients:
          return new ParticleSystem.MinMaxGradient(Recolor(source.gradientMin, color), Recolor(source.gradientMax, color));

        case ParticleSystemGradientMode.RandomColor:
          return new ParticleSystem.MinMaxGradient(Recolor(source.gradient, color)) { mode = ParticleSystemGradientMode.RandomColor };

        default:
          return source;
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static Gradient Recolor(Gradient source, Color color)
    {
      var result = new Gradient { mode = source.mode };
      result.SetKeys(source.colorKeys.Select(k => new GradientColorKey(Tint(k.color, color), k.time)).ToArray()
                     , source.alphaKeys);
      return result;
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static void RecolorBonemassBody(GameObject prefab, Color color)
    {
      try
      {
        var body = prefab.transform.Find(EvilBonemassBodyPath);
        if (body == null || !body.TryGetComponent(out Renderer renderer))
        {
          Log.Warning(Instance, $"{nameof(Main)}.{nameof(RecolorBonemassBody)} [{prefab.name}] missing Renderer on '{EvilBonemassBodyPath}'");
          return;
        }

        var materials = renderer.sharedMaterials;
        var slot = Array.FindIndex(materials, m => m != null && m.name == EvilBonemassMudMaterialName);
        if (slot < 0)
        {
          Log.Warning(Instance, $"{nameof(Main)}.{nameof(RecolorBonemassBody)} [{prefab.name}] missing material '{EvilBonemassMudMaterialName}'");
          return;
        }

        var material = new Material(materials[slot]) { name = materials[slot].name + "_evil" };

        foreach (var property in new[] { "_FlowColor", "_EmissiveColor", "_SSS" })
        {
          if (material.HasProperty(property)) material.SetColor(property, Tint(material.GetColor(property), color));
        }

        Color.RGBToHSV(color, out var targetHue, out _, out _);
        foreach (var property in new[] { "_MainTex", "_FlowTexture" })
        {
          if (!material.HasProperty(property)) continue;
          var texture = material.GetTexture(property);
          if (texture == null) continue;

          var recolored = RecolorTextureHueBand(texture, targetHue, minHue: 0.15f, maxHue: 0.45f, minSaturation: 0.15f);
          if (recolored != null) material.SetTexture(property, recolored);
        }

        materials[slot] = material;
        renderer.sharedMaterials = materials;
      }
      catch (Exception ex)
      {
        Log.Error(Instance, ex);
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static Texture2D RecolorTextureHueBand(Texture source, float targetHue, float minHue, float maxHue, float minSaturation)
    {
      // if (Modding.Framework.Utils.ValheimUtils.IsDedicated) return null;
      if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null; // dedicated server

      var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
      var previous = RenderTexture.active;
      try
      {
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, false)
        {
          name = source.name + "_evil"
          , wrapMode = source.wrapMode
          , filterMode = source.filterMode
          , anisoLevel = source.anisoLevel
        };
        result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);

        var pixels = result.GetPixels32();
        for (var i = 0; i < pixels.Length; i++)
        {
          Color c = pixels[i];
          Color.RGBToHSV(c, out var h, out var s, out var v);
          if (h < minHue || h > maxHue || s < minSaturation) continue;
          var recolored = Color.HSVToRGB(targetHue, s, v);
          recolored.a = c.a;
          pixels[i] = recolored;
        }

        result.SetPixels32(pixels);
        result.Apply(true, false);
        result.Compress(true);     // back to DXT so it isn't 4x the memory of the original
        result.Apply(false, true); // drop the CPU copy
        return result;
      }
      finally
      {
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    public static IEnumerator ReplaceSeekerQueen(Humanoid queen, ZNetView queenView, float altarTimeoutSeconds = 30f)
    {
      while (queenView != null && queenView.IsValid() && !queenView.IsOwner())
      {
        yield return new WaitForSeconds(0.5f);
      }

      if (queenView == null || !queenView.IsValid()) yield break;
      var queenPosition = queen.transform.position;
      if (Instance.EnableTrace) Log.Trace(Instance, $"{nameof(Main)}.{nameof(ReplaceSeekerQueen)} [Removing {BossNames.SeekerQueen}]");
      ZNetScene.instance.Destroy(queen.gameObject);
      var deadline = Time.time + altarTimeoutSeconds;
      OfferingBowl altar;
      while ((altar = FindClosestQueenAltar(queenPosition)) == null || altar.m_nview == null || !altar.m_nview.IsValid())
      {
        if (Time.time > deadline)
        {
          yield break;
        }

        yield return new WaitForSeconds(0.5f);
      }

      altar.m_nview.ClaimOwnership();
      if (altar.IsBossSpawnQueued()) yield break;
      if (altar.CanSpawnBoss(altar.GetSpawnPosition(), out var spawnPoint))
      {
        altar.SpawnBoss(spawnPoint);
      }
      else
      {
        Log.Warning(Instance, $"{QueenAltarName} at {altar.transform.position} found no valid boss spawn point");
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static void SetEyeColorToEvil(GameObject prefab)
    {
      try
      {
        var levelEffects = prefab.GetComponentInChildren<LevelEffects>(true);
        var sharedMaterials = levelEffects.m_mainRender.sharedMaterials;
        var sharedMaterial = new Material(sharedMaterials[0]) { name = sharedMaterials[0].name + "_evil" };
        sharedMaterial.EnableKeyword("_EMISSION");
        sharedMaterial.SetColor("_EmissionColor", Color.red * 2f);
        sharedMaterials[0] = sharedMaterial;
        levelEffects.m_mainRender.sharedMaterials = sharedMaterials;

        foreach (var levelSetup in levelEffects.m_levelSetups)
        {
          levelSetup.m_setEmissiveColor = true;
          levelSetup.m_emissiveColor = Color.red * 2f;
        }
      }
      catch (Exception ex)
      {
        Log.Error(Instance, ex);
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static void ShrinkBonemass(GameObject prefab, float factor)
    {
      var root = prefab.transform;

      if (prefab.TryGetComponent(out CapsuleCollider capsule))
      {
        capsule.center *= factor;
        capsule.height *= factor;
        capsule.radius *= factor;
      }

      foreach (Transform child in root)
      {
        if (child.name == "Visual")
        {
          child.localScale *= factor;
          child.localPosition *= factor;
          continue;
        }

        child.localPosition *= factor;
        child.localScale *= factor;
      }
    }

    private void SynchronizationManager_OnConfigurationSynchronized(object sender, ConfigurationSynchronizationEventArgs e)
    {
      try
      {
        if (Instance.EnableTrace) Log.Trace(Instance, $"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}.{MethodBase.GetCurrentMethod()?.Name}");
        // Placeholder for now.
      }
      catch (Exception ex)
      {
        Log.Error(Instance, ex);
      }
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static Color Tint(Color source, Color target)
    {
      Color.RGBToHSV(target, out var h, out var s, out _);
      Color.RGBToHSV(source, out _, out _, out var v);
      var result = Color.HSVToRGB(h, s, v, true);
      result.a = source.a;
      return result;
    }

    [AiInfo(AiInfoMetaData.Ai_Generated)]
    private static void TintParticleSystem(ParticleSystem ps, Color color)
    {
      var main = ps.main;
      main.startColor = Recolor(main.startColor, color);

      var colorOverLifetime = ps.colorOverLifetime;
      if (colorOverLifetime.enabled) colorOverLifetime.color = Recolor(colorOverLifetime.color, color);

      var colorBySpeed = ps.colorBySpeed;
      if (colorBySpeed.enabled) colorBySpeed.color = Recolor(colorBySpeed.color, color);

      if (!ps.TryGetComponent(out ParticleSystemRenderer renderer) || renderer.sharedMaterial == null) return;

      var material = new Material(renderer.sharedMaterial) { name = renderer.sharedMaterial.name + "_evil" };
      foreach (var property in new[] { "_Color", "_TintColor" })
      {
        if (material.HasProperty(property)) material.SetColor(property, Tint(material.GetColor(property), color));
      }

      if (material.HasProperty("_EmissionColor"))
      {
        var emission = material.GetColor("_EmissionColor");
        var intensity = emission.maxColorComponent;
        if (intensity > 0f) material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * intensity);
      }

      renderer.sharedMaterial = material;
    }

    #endregion

    #region Implementation of ITraceableLogging

    /// <inheritdoc />
    public string Source => Namespace;

    /// <inheritdoc />
    public bool EnableTrace { get; }

    #endregion
  }
}
