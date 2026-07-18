using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(InventoryDisplay))]
internal static class InventoryDisplayPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(InventoryDisplay.Show))]
    private static bool DontDisplayBotInventory(PlayerInventory inventory)
    {
        return !Helpers.IsBot(inventory.playerMain);
    }
}
