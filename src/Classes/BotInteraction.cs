using System.Collections.Generic;
using UnityEngine;

namespace ZumbiBots.Classes;

public static class BotInteraction
{
    private static readonly Vector3 LootPosOffset = new(0f, 0.1f, 0f);
    private static readonly List<InteractableFurniture> NearbyBuffer = [];

    private static List<InteractableFurniture> GetNearbyFurniture(Vector3 pos)
    {
        NearbyBuffer.Clear();

        var hash = OptimizedFurnitureHash.instance;
        if (hash?.cells == null)
            return NearbyBuffer;

        var cx = Mathf.Clamp(Mathf.FloorToInt(pos.x / hash.cellSize), 0, hash.width);
        var cy = Mathf.Clamp(Mathf.FloorToInt(pos.z / hash.cellSize), 0, hash.height);
        for (var dx = -1; dx <= 1; dx++)
        {
            var ix = cx + dx;
            if (ix < 0 || ix >= hash.width)
                continue;

            for (var dy = -1; dy <= 1; dy++)
            {
                var iy = cy + dy;
                if (iy < 0 || iy >= hash.height)
                    continue;

                var furnitures = hash.cells[ix, iy].furnitures;
                if (furnitures != null)
                    NearbyBuffer.AddRange(furnitures);
            }
        }

        return NearbyBuffer;
    }

    public static void ForceInteract(PlayerMain playerMain, InteractableObject interactable)
    {
        if (playerMain == null || interactable == null)
            return;

        playerMain.interaction?.Interact(interactable, default);
    }

    public static Vector3 GetDoorHitPosition(InteractableFurniture door)
    {
        return door == null ? Vector3.zero : door.refFXTransform.position;
    }

    public static bool GetClosestInteractableDoor(PlayerMain player, out InteractableFurniture closestDoor,
        bool furtherRange = false)
    {
        closestDoor = null;

        if (player == null || OptimizedFurnitureHash.instance == null)
            return false;

        var furnitures = GetNearbyFurniture(player.transform.position);
        if (furnitures.Count == 0) return false;

        var minSqrDist = float.MaxValue;
        var playerPos = player.transform.position;
        var interactRange = furtherRange ? 5f : 2.25f;

        foreach (var furniture in furnitures)
        {
            if (furniture == null)
                continue;

            if (furniture.GetInteractableID() != InteractableObject.ID.Door || furniture.IsDestroyed ||
                !furniture.CurrentlyInteractable || !furniture.CanUnlockDoor(player))
                continue;

            var sqrDist = Helpers.DistToSqr(playerPos, furniture.InteractionPoint);
            if (!(sqrDist < minSqrDist) || !(sqrDist <= interactRange))
                continue;

            minSqrDist = sqrDist;
            closestDoor = furniture;
        }

        return closestDoor != null;
    }

    public static bool GetClosestDestroyableDoor(PlayerMain player, out Vector3? doorHitPosition)
    {
        doorHitPosition = null;

        if (player == null || OptimizedFurnitureHash.instance == null)
            return false;

        var furnitures = GetNearbyFurniture(player.transform.position);
        if (furnitures.Count == 0)
            return false;

        var minSqrDist = float.MaxValue;
        var playerPos = player.transform.position;

        foreach (var furniture in furnitures)
        {
            if (furniture == null)
                continue;

            if (furniture.GetInteractableID() != InteractableObject.ID.Door || furniture.IsDestroyed ||
                !furniture.CurrentlyInteractable || !furniture.CanUnlockDoor(player))
                continue;

            var sqrDist = Helpers.DistToSqr(playerPos, furniture.InteractionPoint);
            if (!(sqrDist < minSqrDist) || !(sqrDist <= 5f))
                continue;

            minSqrDist = sqrDist;
            doorHitPosition = GetDoorHitPosition(furniture);
        }

        return doorHitPosition != null;
    }

    public static bool GetClosestUnlitPyre(PlayerMain player, out PyreInteractable closestPyre)
    {
        closestPyre = null;

        if (player == null || OptimizedFurnitureHash.instance == null)
            return false;

        var hash = OptimizedFurnitureHash.instance;
        if (hash.cells == null)
            return false;

        var minSqrDist = float.MaxValue;
        var playerPos = player.transform.position;
        for (var x = 0; x < hash.width; x++)
        {
            for (var y = 0; y < hash.height; y++)
            {
                var furnitures = hash.cells[x, y].furnitures;
                if (furnitures == null)
                    continue;

                foreach (var furniture in furnitures)
                {
                    if (furniture == null)
                        continue;

                    if (furniture is not PyreInteractable pyre)
                        continue;

                    if (pyre.IsLit)
                        continue;

                    var sqrDist = Helpers.DistToSqr(playerPos, pyre.InteractionPoint);
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        closestPyre = pyre;
                    }
                }
            }
        }

        return closestPyre != null;
    }

    public static bool CanLoot(DroppedLoot loot, int lobbyId)
    {
        if (loot == null)
            return false;

        if (!loot.IsSack)
            return true;

        return loot.reservedPlayerID switch
        {
            null => false,
            -1 => true,
            _ => lobbyId == loot.reservedPlayerID.Value
        };
    }

    private static bool IsLootBlockedByDoor(PlayerMain player, Vector3 lootPosition)
    {
        if (OptimizedFurnitureHash.instance == null)
            return false;

        var furnitures = GetNearbyFurniture(lootPosition);
        if (furnitures.Count == 0)
            return false;

        var botHeadPos = BotVision.GetBotHeadPosition(player);
        var raycastLootPos = lootPosition + LootPosOffset;
        if (BotVision.IsPosVisible(botHeadPos, raycastLootPos))
            return false;

        const float sqrDistLimit = 25f * 25f;
        foreach (var furniture in furnitures)
        {
            if (furniture == null)
                continue;

            if (furniture.GetInteractableID() != InteractableObject.ID.Door)
                continue;

            if (furniture.IsDestroyed || !furniture.CurrentlyInteractable)
                continue;

            if (furniture.DoorState != DoorState.Locked)
                continue;

            // Bots are having trouble pathfinding to areas with locked doors, so it's best to keep this disabled for now
            // if (furniture.CanUnlockDoor(player))
            //     continue;

            var sqrDist = Helpers.DistToSqr(lootPosition, furniture.InteractionPoint);
            if (sqrDist <= sqrDistLimit)
                return true;
        }

        return false;
    }

    private static bool IsLootUnderwater(Vector3 lootPosition)
    {
        if (AuxiliarMapObjects.instance == null)
            return false;

        var waterY = AuxiliarMapObjects.instance.WaterY;
        return lootPosition.y <= waterY;
    }

    public static bool GetClosestLoot(PlayerMain player, out InteractableObject closestLoot,
        bool hasGun = true, bool hasFood = true, bool hasDrink = true, bool hasHeal = true)
    {
        closestLoot = null;

        if (player == null || MapHash.instance == null)
            return false;

        var playerPos = player.transform.position;
        var coord = MapHash.instance.GetHashCoord(playerPos);
        var lobbyId = player.lobbyPlayer?.lobbyID ?? -1;

        InteractableObject bestLoot = null;
        var bestSqrDist = float.MaxValue;
        var bestPriority = -1;
        var bestIsSack = false;

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var adjacentCoord = new IntVec2(coord.x + dx, coord.y + dy);
                if (!MapHash.instance.ContainsCell(adjacentCoord))
                    continue;

                var cell = MapHash.instance.GetCell(adjacentCoord);
                if (cell?.loot == null)
                    continue;

                foreach (var loot in cell.loot)
                {
                    if (loot == null)
                        continue;

                    if (!CanLoot(loot, lobbyId))
                        continue;

                    if (IsLootBlockedByDoor(player, loot.transform.position))
                        continue;

                    if (IsLootUnderwater(loot.transform.position))
                        continue;

                    if (BotInventory.IsRecentlyDroppedByBot(player, loot))
                        continue;

                    var sqrDist = Helpers.DistToSqr(playerPos, loot.transform.position);
                    var priority = BotInventory.GetLootPriority(loot.item, hasGun, hasFood, hasDrink, hasHeal);
                    var isSack = loot.IsSack;

                    if (!IsBetterLoot(priority, sqrDist, isSack, bestPriority, bestSqrDist, bestIsSack))
                        continue;

                    bestPriority = priority;
                    bestSqrDist = sqrDist;
                    bestIsSack = isSack;
                    bestLoot = loot;
                }
            }
        }

        closestLoot = bestLoot;
        return closestLoot != null;
    }

    private static bool IsBetterLoot(int newPriority, float newSqrDist, bool newIsSack, int bestPriority,
        float bestSqrDist, bool bestIsSack)
    {
        if (bestPriority < 0)
            return true;

        switch (newIsSack)
        {
            case true when !bestIsSack:
                return true;

            case false when bestIsSack:
                return false;
        }

        // Wins if 2x closer than best priority
        if (newPriority > bestPriority)
            return bestSqrDist <= 0f || newSqrDist <= 4f * bestSqrDist;

        // Wins if 2x farther than candidate priority
        if (newPriority < bestPriority)
            return newSqrDist > 0f && bestSqrDist > 4f * newSqrDist;

        return newSqrDist < bestSqrDist;
    }

    public static bool TryGetClosestVaultSpot(Vector3 pos, Vector3 moveDir, out Vector3 vaultPoint)
    {
        vaultPoint = Vector3.zero;

        var vaultHash = VaultHash.Instance;
        if (vaultHash == null || vaultHash.vaultSpotCells == null)
            return false;

        var dir2D = moveDir.xz();
        if (dir2D.sqrMagnitude < 0.01f)
            return false;

        var cx = Mathf.Clamp(Mathf.FloorToInt(pos.x / vaultHash.cellSize), 0, vaultHash.width - 1);
        var cy = Mathf.Clamp(Mathf.FloorToInt(pos.z / vaultHash.cellSize), 0, vaultHash.height - 1);

        var tolerance = VaultingTolerances.Player;
        const float maxSqrDist = 1f;
        var closestSqrDist = float.MaxValue;
        var found = false;

        for (var dx = -1; dx <= 1; dx++)
        {
            var ix = cx + dx;
            if (ix < 0 || ix >= vaultHash.width)
                continue;

            for (var dy = -1; dy <= 1; dy++)
            {
                var iy = cy + dy;
                if (iy < 0 || iy >= vaultHash.height)
                    continue;

                var spots = vaultHash.vaultSpotCells[ix, iy];
                if (spots == null)
                    continue;

                foreach (var spot in spots)
                {
                    if (spot.id != VaultSpotType.Window && spot.id != VaultSpotType.Barrier)
                        continue;

                    var midpoint = (spot.p1 + spot.p2) * 0.5f;
                    if (midpoint.y < pos.y - tolerance.lowerBounds ||
                        midpoint.y > pos.y + tolerance.highUpperBounds)
                        continue;

                    var toSpot = midpoint - pos;
                    var sqrDist = new Vector2(toSpot.x, toSpot.z).sqrMagnitude;
                    if (sqrDist > maxSqrDist || sqrDist < 0.01f)
                        continue;

                    var toSpotDir = new Vector2(toSpot.x, toSpot.z).normalized;
                    if (Vector2.Dot(dir2D.normalized, toSpotDir) < 0.5f)
                        continue;

                    if (sqrDist < closestSqrDist)
                    {
                        closestSqrDist = sqrDist;
                        vaultPoint = midpoint;
                        found = true;
                    }
                }
            }
        }

        return found;
    }
}
