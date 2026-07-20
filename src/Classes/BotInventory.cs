using System.Collections.Generic;
using UnityEngine;

namespace ZumbiBots.Classes;

public static class BotInventory
{
    private static readonly CraftingRecipe.Material[] PyreFuelMaterials =
        [new(InventoryItem.ID.Wood, 50), new(InventoryItem.ID.DarkBlocks, 25)];

    private const float DroppedItemExpirySeconds = 30f;
    private const float DroppedItemMatchRadiusSqr = 2.25f;

    public static readonly Dictionary<PlayerMain, List<DroppedItemEntry>> DroppedItemsByBot = new();

    public struct DroppedItemEntry
    {
        public Vector3 Position;
        public float Timestamp;
        public InventoryItem.ID ItemId;
    }

    public static void ManageInventory(PlayerMain player, bool needEat, bool needDrink)
    {
        if (player?.inventory == null || ItemsBase.instance == null)
            return;

        var inventory = player.inventory;
        for (var slot = 0; slot < inventory.equippedItem.Length; slot++)
        {
            if (slot == (int)DatabaseItem.SubType.Tool)
                continue;

            var equipped = inventory.GetEquipment(slot);
            var equippedScore = GetItemScoreForSlot(equipped, slot, needEat, needDrink);
            InventoryItem bestItem = null;
            var bestScore = equippedScore;
            foreach (var item in inventory.storage.items.ToArray())
            {
                if (item.id == InventoryItem.ID.None)
                    continue;

                var dbItem = item.GetDataBaseItem();
                if (dbItem == null || (int)dbItem.GetSubType() != slot)
                    continue;

                var score = GetItemScoreForSlot(item, slot, needEat, needDrink);
                if (!(score > bestScore))
                    continue;

                bestScore = score;
                bestItem = item;
            }

            if (bestItem != null)
            {
                if (!equipped.IsNone)
                    ScrapOrStore(inventory, equipped);

                inventory.storage.items.Remove(bestItem);
                inventory.SetEquipment(bestItem, slot);
            }
            else
            {
                ScrapInferiorStorage(inventory, slot, equippedScore);
            }
        }

        DiscardNonExplosiveThrowables(player);
    }

    private static void ScrapOrStore(PlayerInventory inventory, InventoryItem item)
    {
        var dbItem = item.GetDataBaseItem();
        if (dbItem == null)
            return;

        if (dbItem.GetSubType() == DatabaseItem.SubType.Throwable && !IsExplosiveThrowable(item))
        {
            if (dbItem.CanScrap)
            {
                ScrappingUtils.ScrapItem(item, inventory);
            }
            else
            {
                RecordDrop(inventory.playerMain, item);
                inventory.RemoveItem(item);
                inventory.DropLoot(item);
            }

            return;
        }

        if (dbItem.CanScrap && dbItem.GetSubType() != DatabaseItem.SubType.Food &&
            dbItem.GetSubType() != DatabaseItem.SubType.Healing)
        {
            ScrappingUtils.ScrapItem(item, inventory);
        }
        else
        {
            RecordDrop(inventory.playerMain, item);
            inventory.RemoveItem(item);
            inventory.DropLoot(item);
        }
    }

    private static bool IsExplosiveThrowable(InventoryItem item)
    {
        var throwable = item.GetDataBaseItem() as DatabaseThrowable;
        return throwable != null && throwable.explosionID != Explosion.ID.None;
    }

    private static void DiscardNonExplosiveThrowables(PlayerMain player)
    {
        var inventory = player.inventory;

        const int throwableSlot = (int)DatabaseItem.SubType.Throwable;
        var equipped = inventory.GetEquipment(throwableSlot);
        if (!equipped.IsNone && !IsExplosiveThrowable(equipped))
        {
            if (equipped.GetDataBaseItem()?.CanScrap == true)
            {
                ScrappingUtils.ScrapItem(equipped, inventory);
            }
            else
            {
                inventory.RemoveItem(equipped);
                inventory.DropLoot(equipped);
            }
        }

        foreach (var item in inventory.storage.items.ToArray())
        {
            if (item.id == InventoryItem.ID.None)
                continue;

            var dbItem = item.GetDataBaseItem();
            if (dbItem == null || dbItem.GetSubType() != DatabaseItem.SubType.Throwable)
                continue;

            if (IsExplosiveThrowable(item))
                continue;

            if (dbItem.CanScrap)
            {
                ScrappingUtils.ScrapItem(item, inventory);
            }
            else
            {
                RecordDrop(player, item);
                inventory.RemoveItem(item);
                inventory.DropLoot(item);
            }
        }
    }

    private static void ScrapInferiorStorage(PlayerInventory inventory, int slot, float equippedScore)
    {
        foreach (var item in inventory.storage.items.ToArray())
        {
            if (item.id == InventoryItem.ID.None)
                continue;

            var dbItem = item.GetDataBaseItem();
            if (dbItem == null || (int)dbItem.GetSubType() != slot || dbItem.GetSubType() == DatabaseItem.SubType.Food)
                continue;

            var score = GetItemScoreForSlot(item, slot, false, false);
            if (!(score <= equippedScore))
                continue;

            if (dbItem.CanScrap)
            {
                ScrappingUtils.ScrapItem(item, inventory);
            }
            else
            {
                RecordDrop(inventory.playerMain, item);
                inventory.RemoveItem(item);
                inventory.DropLoot(item);
            }
        }
    }

    private static float GetGunScore(DatabaseGun gun)
    {
        return (int)gun.tier * 100f + gun.otherStats.sustDPS;
    }

    private static float GetMeleeScore(DatabaseMelee melee)
    {
        return (int)melee.tier * 100f + melee.baseDmg;
    }

    private static float GetThrowableScore(DatabaseThrowable throwable)
    {
        if (throwable.explosionID == Explosion.ID.None)
            return 0f;

        var explosionDmg = 0f;
        if (ExplosionController.instance != null)
        {
            var explosion = ExplosionController.instance.GetExplosion(throwable.explosionID);
            if (explosion != null)
                explosionDmg = explosion.maxTotalDamage;
        }

        return (int)throwable.tier * 100f + explosionDmg;
    }

    private static float GetItemScoreForSlot(InventoryItem item, int slot, bool needEat, bool needDrink)
    {
        if (item == null || item.IsNone)
            return 0f;

        var dbItem = item.GetDataBaseItem();
        if (dbItem == null)
            return 0f;

        switch ((DatabaseItem.SubType)slot)
        {
            case DatabaseItem.SubType.PrimaryGun:
            case DatabaseItem.SubType.SecondaryGun:
                var gun = dbItem as DatabaseGun;
                return gun != null ? GetGunScore(gun) : 0f;

            case DatabaseItem.SubType.Melee:
                var melee = dbItem as DatabaseMelee;
                return melee != null ? GetMeleeScore(melee) : 0f;

            case DatabaseItem.SubType.Throwable:
                var throwable = dbItem as DatabaseThrowable;
                return throwable != null ? GetThrowableScore(throwable) : 0f;

            case DatabaseItem.SubType.Food:
            case DatabaseItem.SubType.Healing:
                var consumable = dbItem as DatabaseConsumable;
                if (consumable == null)
                    return (int)dbItem.tier * 100f;

                var consumableScore = (int)dbItem.tier * 100f + consumable.effectAmount;
                if (slot != (int)DatabaseItem.SubType.Food)
                    return consumableScore;

                if (needDrink && consumable.statusID == StatusEffect.ID.Drink)
                    consumableScore += 10000f;
                else if (needEat && consumable.statusID == StatusEffect.ID.Food)
                    consumableScore += 5000f;

                return consumableScore;

            case DatabaseItem.SubType.Tool:
            case DatabaseItem.SubType.Misc:
            default:
                return 0f;
        }
    }

    public static int GetCurAmmoCount(PlayerMain player)
    {
        var equipment = player.inventory.GetEquipment(player.arms.selectedWeapon);
        return equipment.ammo;
    }

    public static int GetMaxAmmoCount(PlayerMain player)
    {
        var equipment = player.inventory.GetEquipment(player.arms.selectedWeapon);
        var database = equipment.GetDataBaseItem() as DatabaseGun;
        return database != null ? database.maxAmmo : 0;
    }

    public static bool IsHoldingGun(PlayerMain player)
    {
        return player.arms != null && player.arms.selectedWeapon is 0 or 1;
    }

    public static bool IsHoldingMelee(PlayerMain player)
    {
        return player.arms != null && player.arms.selectedWeapon == 2;
    }

    public static bool HasMatchingConsumable(PlayerMain player, bool needEat, bool needDrink)
    {
        return IsConsumableMatching(player.inventory.GetEquipment(4), needEat, needDrink) ||
               IsConsumableMatching(player.inventory.GetEquipment(player.arms.selectedWeapon), needEat, needDrink);
    }

    private static bool IsConsumableMatching(InventoryItem item, bool needEat, bool needDrink)
    {
        if (item == null || item.IsNone)
            return false;

        var dbItem = item.GetDataBaseItem() as DatabaseConsumable;
        if (dbItem == null)
            return false;

        return (needDrink && dbItem.statusID == StatusEffect.ID.Drink) ||
               (needEat && dbItem.statusID == StatusEffect.ID.Food);
    }

    public static void CheckNeeds(PlayerMain player, out bool hasGun, out bool hasMelee, out bool hasFood,
        out bool hasDrink, out bool hasHeal)
    {
        hasGun = false;
        hasMelee = false;
        hasFood = false;
        hasDrink = false;
        hasHeal = false;

        if (player?.inventory == null || ItemsBase.instance == null)
            return;

        var inventory = player.inventory;

        for (var i = 0; i < inventory.equippedItem.Length; i++)
            ClassifyItem(inventory.GetEquipment(i), ref hasGun, ref hasMelee, ref hasFood, ref hasDrink, ref hasHeal);

        foreach (var item in inventory.storage.items)
            ClassifyItem(item, ref hasGun, ref hasMelee, ref hasFood, ref hasDrink, ref hasHeal);
    }

    private static void ClassifyItem(InventoryItem item, ref bool hasGun, ref bool hasMelee, ref bool hasFood,
        ref bool hasDrink, ref bool hasHeal)
    {
        if (item == null || item.IsNone)
            return;

        var dbItem = item.GetDataBaseItem();
        if (dbItem == null)
            return;

        var subType = dbItem.GetSubType();
        switch (subType)
        {
            case DatabaseItem.SubType.PrimaryGun:
            case DatabaseItem.SubType.SecondaryGun:
                hasGun = true;
                break;

            case DatabaseItem.SubType.Melee:
                hasMelee = true;
                break;

            case DatabaseItem.SubType.Food when dbItem is DatabaseConsumable consumable:
            {
                if (consumable.statusID == StatusEffect.ID.Drink)
                    hasDrink = true;
                else
                    hasFood = true;
                break;
            }

            case DatabaseItem.SubType.Healing:
                hasHeal = true;
                break;
        }
    }

    public static int GetLootPriority(InventoryItem item, bool hasGun, bool hasFood, bool hasDrink,
        bool hasHeal)
    {
        if (item == null || item.IsNone)
            return -1;

        var dbItem = item.GetDataBaseItem();
        if (dbItem == null)
            return -1;

        var subType = dbItem.GetSubType();

        switch (subType)
        {
            case DatabaseItem.SubType.PrimaryGun or DatabaseItem.SubType.SecondaryGun when !hasGun:
                return 4;

            case DatabaseItem.SubType.Food
                when dbItem is DatabaseConsumable { statusID: StatusEffect.ID.Drink } && !hasDrink:
                return 3;

            case DatabaseItem.SubType.Food
                when dbItem is DatabaseConsumable { statusID: StatusEffect.ID.Food } && !hasFood:
                return 2;

            case DatabaseItem.SubType.Healing when !hasHeal:
                return 1;

            default:
                return 0;
        }
    }

    public static bool IsEquipSlotAvailable(PlayerMain playerMain, int slot)
    {
        return slot >= 0 && playerMain.inventory.spawnedEquipment[slot] != null;
    }

    public static bool HasPyreFuel(PlayerMain player)
    {
        return player?.inventory != null && CraftingRecipe.CanCraft(player.inventory, PyreFuelMaterials);
    }

    public static void ConsumePyreFuel(PlayerMain player)
    {
        if (player?.inventory != null)
            CraftingRecipe.RemoveMaterialsFrom(player.inventory, PyreFuelMaterials);
    }

    public static void RecordDrop(PlayerMain player, InventoryItem item)
    {
        if (player == null || item == null || item.IsNone)
            return;

        var position = player.transform.position;
        if (!DroppedItemsByBot.TryGetValue(player, out var list))
        {
            list = [];
            DroppedItemsByBot[player] = list;
        }

        list.Add(new DroppedItemEntry
        {
            Position = position,
            Timestamp = Time.time,
            ItemId = item.id
        });
    }

    public static bool IsRecentlyDroppedByBot(PlayerMain player, DroppedLoot loot)
    {
        if (player == null || loot == null || loot.item == null || loot.item.IsNone)
            return false;

        if (!DroppedItemsByBot.TryGetValue(player, out var list))
            return false;

        var lootPos = loot.transform.position;
        var now = Time.time;
        foreach (var entry in list)
        {
            if (now - entry.Timestamp > DroppedItemExpirySeconds)
                continue;

            if (entry.ItemId != loot.item.id)
                continue;

            if ((lootPos - entry.Position).sqrMagnitude > DroppedItemMatchRadiusSqr)
                continue;

            return true;
        }

        return false;
    }

    public static void PruneExpiredDroppedItems()
    {
        var now = Time.time;
        var keys = new List<PlayerMain>(DroppedItemsByBot.Keys);
        foreach (var bot in keys)
        {
            if (!DroppedItemsByBot.TryGetValue(bot, out var list))
                continue;

            list.RemoveAll(e => now - e.Timestamp > DroppedItemExpirySeconds);

            if (list.Count == 0)
                DroppedItemsByBot.Remove(bot);
        }
    }
}
