using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerInventory))]
internal static class PlayerInventoryPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerInventory.PutLootIntoPosition))]
    private static void SuppressBotLootNotifications(PlayerInventory __instance, ref bool notifyOnHUD)
    {
        if (Helpers.IsBot(__instance.playerMain))
            notifyOnHUD = false;
    }
}
