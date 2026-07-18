using HarmonyLib;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(LootController))]
internal static class LootControllerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(LootController.LootLootable))]
    private static bool BotCheckLootables(PlayerMain player, Vector3 lootPos, float distanceSqr, ref bool __result)
    {
        if (Helpers.IsBot(player))
        {
            if (LootController.Instance == null)
            {
                __result = false;
                return false;
            }

            var limit = Traverse.Create(LootController.Instance).Field<float>("lootInteractDistance").Value;
            __result = distanceSqr < limit * limit;

            return false;
        }

        return true;
    }
}
