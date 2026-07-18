using HarmonyLib;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(ExplosionController))]
internal static class ExplosionControllerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("ProcessExplosion")]
    private static void ProcessExplosionForBots(ExplosionController __instance, Vector3 origin,
        Explosion.ID explosionID, int sourceLobbyID, bool isZombieExplosion)
    {
        if (!MultiplayerController.instance.IsServer()) return;

        var explosionSettings = __instance.predefExplosion[(int)(explosionID - 1)];
        var sourcePlayerId = (isZombieExplosion ? (-1) : sourceLobbyID);
        var sourcePlayer = PlayersController.instance.GetPlayer(sourcePlayerId);

        foreach (var player in PlayersController.instance.players)
        {
            if (player != null && Helpers.IsBot(player))
                ProcessExplosionAgainstBot(__instance, origin, explosionSettings, sourcePlayer, player);
        }
    }

    private static void ProcessExplosionAgainstBot(ExplosionController instance, Vector3 origin,
        Explosion explosionSettings, PlayerMain sourcePlayer, PlayerMain bot)
    {
        var result =
            instance.ExplosionResultFor(origin, bot.transform.position, bot.transform, explosionSettings, false);

        if (result.damage > 1f)
        {
            var damageAmount = result.damage;
            if (sourcePlayer != null)
                damageAmount *= (sourcePlayer != bot) ? 0.1f : 0.5f;

            var damage = new Damage(damageAmount, bot.transform.position,
                new PlayerDamage(result.direction, 0.5f, true, 1f));
            Damage.ProcessDamage(bot, damage, true);
        }

        if (result.hitDistanceFactor > 0f && explosionSettings.seAmount > 0f)
            bot.statusEffects.AddEffect(explosionSettings.seID, explosionSettings.seAmount, 0f);
    }
}
