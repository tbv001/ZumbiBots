using System.Linq;
using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(LootSacksController))]
internal static class LootSacksControllerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(LootSacksController.DropWholeInventory))]
    private static bool FixBotDeathSackOwnership(LootSacksController __instance, PlayerInventory playerInventory,
        UnityEngine.Vector3 position)
    {
        if (!Helpers.IsBot(playerInventory.playerMain))
            return true;

        var lootSackItemSet = new LootSackItemSet();
        lootSackItemSet.AddItems(playerInventory.equippedItem.Where(item => item is { IsNone: false }));
        lootSackItemSet.AddItems(playerInventory.storage.items);

        if (!lootSackItemSet.HasItems)
            return false;

        if (MultiplayerController.instance.IsServer())
        {
            __instance.SpawnDroppedSackServerSide(lootSackItemSet, playerInventory.playerMain.lobbyPlayer.lobbyID, 180f,
                position);
        }
        else
        {
            var clientSackId = Traverse.Create(__instance).Property<int>("PullClientSackID").Value;
            var sackDataBuffers = __instance.SackDataBuffers(clientSackId, lootSackItemSet);
            Traverse.Create(ClientController.instance.GetSpeaker).Method("SyncDroppedSack", sackDataBuffers).GetValue();
        }

        playerInventory.ClearContents();
        playerInventory.OnEquipmentChanged();
        return false;
    }
}
