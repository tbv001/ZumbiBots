using UnityEngine;
using ZumbiBots.Classes;

namespace ZumbiBots.Components;

public class BotGameManager : MonoBehaviour
{
    private float _processTime;
    public static bool IsBossActive;

    private static void AssignTargets()
    {
        var players = PlayersController.instance?.players;
        if (players == null)
            return;

        foreach (var bot in players)
        {
            if (bot == null)
                continue;

            var brain = bot.GetComponent<BotBrain>();
            if (brain == null || bot.healthState != PlayerMain.HealthState.Alive)
                continue;

            brain.CurrentTarget = null;
            if (BotTargetting.GetClosestAny(bot, out var currentTarget))
            {
                brain.CurrentTarget = currentTarget;
            }
        }
    }

    private static void AssignBossTargets()
    {
        var players = PlayersController.instance?.players;
        if (players == null)
            return;

        foreach (var bot in players)
        {
            if (bot == null)
                continue;

            var brain = bot.GetComponent<BotBrain>();
            if (brain == null || bot.healthState != PlayerMain.HealthState.Alive)
                continue;

            brain.BossTarget = null;
            if (BotTargetting.GetClosestBoss(bot, out var bossTarget))
            {
                brain.BossTarget = bossTarget;
            }
        }
    }

    private static void AssignWaveTargets()
    {
        var players = PlayersController.instance?.players;
        if (players == null)
            return;

        foreach (var bot in players)
        {
            if (bot == null)
                continue;

            var brain = bot.GetComponent<BotBrain>();
            if (brain == null || bot.healthState != PlayerMain.HealthState.Alive)
                continue;

            brain.WaveTarget = null;
            if (BotTargetting.GetClosestWaveZombie(bot, out var waveTarget))
            {
                brain.WaveTarget = waveTarget;
            }
        }
    }

    private static void AssignRevives()
    {
        var players = PlayersController.instance?.players;
        if (players == null)
            return;

        foreach (var player in players)
        {
            if (player == null)
                continue;

            var brain = player.GetComponent<BotBrain>();
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

                var brain = bot.GetComponent<BotBrain>();
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

    private static void ManageHorde()
    {
        Horde.ComputeHordes();

        var players = PlayersController.instance?.players;
        if (players == null)
            return;

        foreach (var player in players)
        {
            if (player == null)
                continue;

            var brain = player.GetComponent<BotBrain>();
            if (brain == null)
                continue;

            brain.ClosestHordePos = Horde.GetClosestHorde(player);
            brain.ClosestZombieInHordePos = Horde.GetClosestZombieInClosestHorde(player);
            brain.ClosestHordeCount = Horde.GetClosestHordeCount(player);
        }
    }

    private void Update()
    {
        if (!BotManager.BotIsAvailable)
            return;

        if (MatchController.instance?.state != MatchController.MatchState.InGame)
            return;

        _processTime += Time.deltaTime;
        if (_processTime < 0.1f)
            return;

        _processTime = 0f;
        IsBossActive = BotTargetting.IsABossActive();

        AssignTargets();
        AssignBossTargets();
        AssignWaveTargets();
        AssignRevives();
        ManageHorde();
        BotInventory.PruneExpiredDroppedItems();
    }
}
