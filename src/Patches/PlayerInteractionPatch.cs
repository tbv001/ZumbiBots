using HarmonyLib;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerInteraction))]
internal static class PlayerInteractionPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("AddRaycastInteractions")]
    private static bool BotRaycastInteractions(PlayerInteraction __instance)
    {
        var playerMain = __instance.playerMain;
        if (!Helpers.IsBot(playerMain))
            return true;

        if (playerMain.cam != null && playerMain.cam.camTransform != null)
            return true;

        if (!Physics.Raycast(BotVision.GetBotHeadPosition(playerMain), BotVision.GetBotAimingDirection(playerMain),
                out var hitInfo, 1.5f, __instance.interactionMask) ||
            !hitInfo.collider.CompareTag("InteractionCollider"))
        {
            return false;
        }

        var component = hitInfo.collider.GetComponent<InteractionCollider>();
        if (component == null)
            return false;

        var interactableFurniture = component.targetObject as InteractableFurniture;
        if (interactableFurniture != null && interactableFurniture.targetFurniture.hasInteraction &&
            interactableFurniture.GetInteractableID() != InteractableObject.ID.Workbench &&
            interactableFurniture.GetInteractableID() != InteractableObject.ID.Vendor)
        {
            __instance.AddInteraction(component.targetObject, InteractionRaycasting.None);
        }

        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch("AddAllInteractions")]
    private static void AddBotLootInteractions(PlayerInteraction __instance)
    {
        var playerMain = __instance.playerMain;
        if (!Helpers.IsBot(playerMain))
            return;

        if (!BotInteraction.GetClosestLoot(playerMain, out var closestLoot))
            return;

        var sqrDist = Helpers.DistToSqr(closestLoot.transform.position, playerMain.transform.position);
        if (LootController.LootLootable(playerMain, closestLoot.transform.position, sqrDist))
        {
            __instance.AddInteraction(closestLoot, InteractionRaycasting.None);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("AddWorkbenchInteractions")]
    private static bool SkipBotWorkbenchInteractions(PlayerInteraction __instance)
    {
        return !Helpers.IsBot(__instance.playerMain);
    }

    [HarmonyPrefix]
    [HarmonyPatch("Interact")]
    private static bool BlockBotWorkbenchInteract(PlayerInteraction __instance, InteractableObject interactionTarget)
    {
        if (!Helpers.IsBot(__instance.playerMain))
            return true;

        if (interactionTarget == null)
            return true;

        var id = interactionTarget.GetInteractableID();
        if (id != InteractableObject.ID.Workbench && id != InteractableObject.ID.Vendor)
            return true;

        interactionTarget.OnInteractedWith();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("InteractWithLoot")]
    private static bool BotInteractWithLoot(PlayerInteraction __instance, InteractableObject interactableObj)
    {
        if (!Helpers.IsBot(__instance.playerMain)) return true;

        var playerMain = __instance.playerMain;
        var droppedLoot = interactableObj as DroppedLoot;
        if (droppedLoot != null && droppedLoot.IsSack)
        {
            if (MultiplayerController.instance.IsServer())
            {
                var sackData = LootSacksController.Instance.PullLoot(droppedLoot);
                foreach (var item in sackData.items)
                {
                    var inventoryItem = InventoryItem.CreateInventoryItem(item);
                    var lootTargetPosition = playerMain.inventory.FindPlaceFor(inventoryItem.id,
                        inventoryItem.stackCount, true, true);
                    if (lootTargetPosition == null)
                    {
                        playerMain.inventory.DropLoot(inventoryItem);
                    }
                    else
                    {
                        playerMain.inventory.PutLootIntoPosition(inventoryItem, lootTargetPosition, false);
                    }
                }
            }
            else
            {
                ClientController.instance.GetSpeaker.RequestLoot(droppedLoot.id);
            }
        }
        else if (droppedLoot != null)
        {
            var lootTargetPosition = playerMain.inventory.FindPlaceFor(droppedLoot.item.id,
                droppedLoot.item.stackCount, true, true);
            if (lootTargetPosition != null)
            {
                if (MultiplayerController.instance.IsServer())
                {
                    var inventoryItem = LootController.Instance.PullLoot(droppedLoot);
                    if (inventoryItem != null)
                        playerMain.inventory.PutLootIntoPosition(inventoryItem, lootTargetPosition, false);
                }
                else
                {
                    ClientController.instance.GetSpeaker.RequestLoot(droppedLoot.id);
                }
            }
        }

        playerMain.playerAudio.PlayCore(PlayerAudio.MiscID.PickLoot, false);
        return false;
    }
}
