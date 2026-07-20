using UnityEngine;
using ZumbiBots.Classes;

namespace ZumbiBots.Components;

public class BotGameManager : MonoBehaviour
{
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

        AssignRevives();
        ManageHorde();
        BotInventory.PruneExpiredDroppedItems();
    }
}
