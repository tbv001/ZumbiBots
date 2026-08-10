using System.Collections.Generic;
using UnityEngine;
using ZumbiBots.Classes;

namespace ZumbiBots.Components;

public class BotGameManager : MonoBehaviour
{
    private static readonly Dictionary<PlayerMain, BotBrain> BrainCache = [];
    private static readonly HashSet<PlayerMain> AliveScratchSet = [];
    private static int _processedBots;
    private float _processTime;
    public static bool IsBossActive;
    public static bool HelicopterArrived;

    private static BotBrain GetBrain(PlayerMain bot)
    {
        if (BrainCache.TryGetValue(bot, out var cached) && cached != null)
            return cached;

        var brain = bot.GetComponent<BotBrain>();
        if (brain != null)
            BrainCache[bot] = brain;

        return brain;
    }

    private static void PruneBrainCache(List<PlayerMain> players)
    {
        if (BrainCache.Count == 0)
            return;

        AliveScratchSet.Clear();
        foreach (var player in players)
        {
            if (player != null)
                AliveScratchSet.Add(player);
        }

        if (BrainCache.Count <= AliveScratchSet.Count)
            return;

        List<PlayerMain> stale = null;
        foreach (var key in BrainCache.Keys)
        {
            if (AliveScratchSet.Contains(key))
                continue;

            stale ??= [];
            stale.Add(key);
        }

        if (stale == null)
            return;

        foreach (var key in stale)
            BrainCache.Remove(key);
    }

    private static void AssignBotTargets(PlayerMain bot)
    {
        if (bot == null)
            return;

        var brain = GetBrain(bot);
        if (brain == null || bot.healthState != PlayerMain.HealthState.Alive)
            return;

        // Set bot target
        brain.CurrentTarget = null;
        if (BotTargetting.GetClosestAny(bot, out var currentTarget))
        {
            brain.CurrentTarget = currentTarget;
        }

        // Set bot active boss target
        brain.BossTarget = null;
        if (!HelicopterArrived && IsBossActive && BotTargetting.GetClosestBoss(bot, out var bossTarget))
        {
            brain.BossTarget = bossTarget;
        }

        // Set bot inactive boss target
        brain.InactiveBossPos = null;
        if (WavesController.instance != null && BossfightController.instance != null &&
            !IsBossActive && !HelicopterArrived &&
            !WavesController.instance.HaveToKillZombies && WavesController.instance.HaveToKillBoss)
        {
            var bossTier = WavesController.instance.CurrentlyEnabledBossTier;
            var bossType = BossfightController.instance.GetZombieTypeForTier(bossTier);
            if (BotTargetting.GetClosestInactiveBossForTier(bot, bossType, out var bossPos))
            {
                brain.InactiveBossPos = bossPos;
            }
        }

        // Set bot zombie from wave target
        brain.WaveTarget = null;
        if (!HelicopterArrived && BotTargetting.GetClosestWaveZombie(bot, out var waveTarget))
        {
            brain.WaveTarget = waveTarget;
        }

        // Set bot closest loot
        brain.ClosestLoot = null;
        if (BotInteraction.GetClosestLoot(bot, out var closestLoot, brain.HasGun, brain.HasFood, brain.HasDrink,
                brain.HasHeal))
        {
            brain.ClosestLoot = closestLoot;
        }
    }

    private static void ProcessBotTargetSlice(List<PlayerMain> players)
    {
        if (players.Count == 0)
            return;

        if (_processedBots >= players.Count)
            _processedBots = 0;

        AssignBotTargets(players[_processedBots]);

        _processedBots++;
        if (_processedBots >= players.Count)
            _processedBots = 0;
    }

    private static void AssignRevives(List<PlayerMain> players)
    {
        foreach (var player in players)
        {
            if (player == null)
                continue;

            var brain = GetBrain(player);
            if (brain != null)
                brain.TargetRevive = null;
        }

        for (var i = 0; i < players.Count; i++)
        {
            var dyingPlayer = players[i];
            if (dyingPlayer == null || dyingPlayer.healthState != PlayerMain.HealthState.Dying)
                continue;

            var minSqrDist = float.MaxValue;
            var dyingPos = dyingPlayer.transform.position;
            BotBrain closestBot = null;

            foreach (var bot in players)
            {
                if (bot == null || bot == dyingPlayer)
                    continue;

                var brain = GetBrain(bot);
                if (brain == null || bot.healthState != PlayerMain.HealthState.Alive)
                    continue;

                var sqrDist = (bot.transform.position - dyingPos).sqrMagnitude;
                if (!(sqrDist < minSqrDist))
                    continue;

                minSqrDist = sqrDist;
                closestBot = brain;
            }

            if (closestBot == null)
                continue;

            dyingPlayer.reviveInteraction.subID = i;
            closestBot.TargetRevive = dyingPlayer.reviveInteraction;
        }
    }

    private static void ManageHorde(List<PlayerMain> players)
    {
        Horde.ComputeHordes();

        foreach (var player in players)
        {
            if (player == null)
                continue;

            var brain = GetBrain(player);
            if (brain == null)
                continue;

            var playerPos = player.transform.position;
            var closestHorde = Horde.FindClosestHorde(playerPos, out var closestHordeCenter);
            brain.ClosestHordePos = closestHordeCenter;
            brain.ClosestZombieInHordePos = Horde.GetClosestZombieInClosestHorde(player, closestHorde);
            brain.ClosestHordeCount = Horde.GetClosestHordeCount(player, closestHorde);
        }
    }

    private void Update()
    {
        if (!BotManager.BotIsAvailable)
            return;

        if (MatchController.instance?.state != MatchController.MatchState.InGame)
            return;

        var players = PlayersController.instance?.players;
        if (players == null)
            return;

        PruneBrainCache(players);
        ProcessBotTargetSlice(players);

        _processTime += Time.deltaTime;
        if (_processTime < 0.1f)
            return;

        _processTime = 0f;

        IsBossActive = BotTargetting.IsABossActive();
        var heliLanding = HelicopterLanding.Instance;
        HelicopterArrived = heliLanding != null && heliLanding.HelicopterSpawned && !heliLanding.HelicopterLeaving &&
                            heliLanding.HelicopterIsLanded;

        AssignRevives(players);
        ManageHorde(players);
        BotInventory.PruneExpiredDroppedItems();
    }
}
