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
    public static readonly Dictionary<PlayerMain, ConsumableSlots> BotSlots = new();

    public struct DroppedItemEntry
    {
        public Vector3 Position;
        public float Timestamp;
        public InventoryItem.ID ItemId;
    }

    public struct ConsumableSlots
    {
        public int DrinkIdx;
        public int FoodIdx;
        public int HealIdx;
        public int ThrowableIdx;
    }

    public static void ManageInventory(PlayerMain player)
    {
        if (player?.inventory == null || ItemsBase.instance == null)
            return;

        var inventory = player.inventory;
        ManagePrimaryWeaponSlots(inventory);
        ManageOtherWeaponSlots(inventory);
        ManageConsumableSlots(player);
        DiscardNonExplosiveThrowables(player);
    }

    private static bool IsPrimaryGun(InventoryItem item)
    {
        if (item == null || item.IsNone)
            return false;

        var dbItem = item.GetDataBaseItem();
        return dbItem?.GetSubType() == DatabaseItem.SubType.PrimaryGun;
    }

    private static void ManagePrimaryWeaponSlots(PlayerInventory inventory)
    {
        var primarySlot0 = EquipmentIndex.Weapon(0);
        var primarySlot1 = EquipmentIndex.Weapon(1);
        var candidateGuns = new List<InventoryItem>();

        var itemInSlot0 = inventory.GetEquipment(primarySlot0);
        if (IsPrimaryGun(itemInSlot0))
            candidateGuns.Add(itemInSlot0);

        var itemInSlot1 = inventory.GetEquipment(primarySlot1);
        if (IsPrimaryGun(itemInSlot1))
            candidateGuns.Add(itemInSlot1);

        foreach (var item in inventory.storage.items.ToArray())
        {
            if (!IsPrimaryGun(item))
                continue;

            candidateGuns.Add(item);
            inventory.storage.items.Remove(item);
        }

        if (candidateGuns.Count == 0)
            return;

        candidateGuns.Sort((a, b) => GetItemScoreForSlot(b).CompareTo(GetItemScoreForSlot(a)));

        var bestGun = candidateGuns[0];
        var secondBestGun = candidateGuns.Count > 1 ? candidateGuns[1] : null;
        inventory.equippedItems.Set(primarySlot0, InventoryItem.None);
        inventory.equippedItems.Set(primarySlot1, InventoryItem.None);
        inventory.SetEquipment(bestGun, primarySlot0);

        if (secondBestGun != null)
            inventory.SetEquipment(secondBestGun, primarySlot1);

        for (var i = 2; i < candidateGuns.Count; i++)
            ScrapOrStore(inventory, candidateGuns[i]);
    }

    private static void ManageOtherWeaponSlots(PlayerInventory inventory)
    {
        for (var w = 2; w <= 3; w++)
        {
            var eqIndex = EquipmentIndex.Weapon(w);
            var equipped = inventory.GetEquipment(eqIndex);
            var equippedScore = GetItemScoreForSlot(equipped);
            InventoryItem bestItem = null;
            var bestScore = equippedScore;

            foreach (var item in inventory.storage.items.ToArray())
            {
                if (item.id == InventoryItem.ID.None)
                    continue;

                var dbItem = item.GetDataBaseItem();
                if (dbItem == null || !PlayerInventory.ItemTypeMatchesSlotIndex(item, eqIndex))
                    continue;

                var score = GetItemScoreForSlot(item);
                if (!(score > bestScore))
                    continue;

                bestScore = score;
                bestItem = item;
            }

            if (bestItem != null)
            {
                if (equipped != null && !equipped.IsNone)
                    ScrapOrStore(inventory, equipped);

                inventory.storage.items.Remove(bestItem);
                inventory.SetEquipment(bestItem, eqIndex);
            }
            else
            {
                ScrapInferiorStorage(inventory, eqIndex, equippedScore);
            }
        }
    }

    private static void ManageConsumableSlots(PlayerMain player)
    {
        var inventory = player.inventory;
        if (inventory == null)
            return;

        var drinkIdx = ManageSpecificConsumableSlot(inventory, 0, IsDrink);
        var foodIdx = ManageSpecificConsumableSlot(inventory, 1, IsFood);
        var healIdx = ManageSpecificConsumableSlot(inventory, 2, IsHeal);
        var throwableIdx = ManageSpecificConsumableSlot(inventory, 3, IsExplosiveThrowable);
        BotSlots[player] = new ConsumableSlots
        {
            DrinkIdx = drinkIdx,
            FoodIdx = foodIdx,
            HealIdx = healIdx,
            ThrowableIdx = throwableIdx
        };
    }

    private static int ManageSpecificConsumableSlot(PlayerInventory inventory, int slotIndex,
        System.Func<InventoryItem, bool> filter)
    {
        var targetEqIndex = EquipmentIndex.Misc(slotIndex);
        var equippedInTarget = inventory.GetEquipment(targetEqIndex);
        var equippedValid = equippedInTarget != null && !equippedInTarget.IsNone && !equippedInTarget.IsEmpty() &&
                            filter(equippedInTarget);
        var bestScore = equippedValid ? GetItemScoreForSlot(equippedInTarget) : -1f;

        InventoryItem bestItem = equippedValid ? equippedInTarget : null;
        var bestSourceStorageIndex = -1;
        var bestSourceMiscSlot = equippedValid ? slotIndex : -1;

        for (var i = 0; i < inventory.storage.items.Count; i++)
        {
            var item = inventory.storage.items[i];
            if (item == null || item.id == InventoryItem.ID.None || item.IsEmpty() || !filter(item))
                continue;

            var score = GetItemScoreForSlot(item);
            if (!(score > bestScore))
                continue;

            bestScore = score;
            bestItem = item;
            bestSourceStorageIndex = i;
            bestSourceMiscSlot = -1;
        }

        var unlockedMiscCount = inventory.equippedItems.UnlockedMiscSlotsCount;
        for (var m = 0; m < unlockedMiscCount; m++)
        {
            if (m == slotIndex)
                continue;

            var otherIndex = EquipmentIndex.Misc(m);
            var item = inventory.GetEquipment(otherIndex);
            if (item == null || item.IsNone || item.IsEmpty() || !filter(item))
                continue;

            var score = GetItemScoreForSlot(item);
            if (!(score > bestScore))
                continue;

            bestScore = score;
            bestItem = item;
            bestSourceStorageIndex = -1;
            bestSourceMiscSlot = m;
        }

        if (bestItem != null)
        {
            if (bestSourceMiscSlot == slotIndex)
                return slotIndex;

            if (bestSourceStorageIndex >= 0)
            {
                inventory.storage.items.RemoveAt(bestSourceStorageIndex);

                if (equippedInTarget != null && !equippedInTarget.IsNone && !equippedInTarget.IsEmpty())
                {
                    var oldDbItem = equippedInTarget.GetDataBaseItem();
                    if (oldDbItem != null && (oldDbItem.GetSubType() == DatabaseItem.SubType.Food ||
                                              oldDbItem.GetSubType() == DatabaseItem.SubType.Healing))
                    {
                        if (!inventory.storage.items.Contains(equippedInTarget))
                            inventory.storage.items.Add(equippedInTarget);
                    }
                    else
                    {
                        ScrapOrStore(inventory, equippedInTarget);
                    }
                }

                inventory.SetEquipment(bestItem, targetEqIndex);
                return slotIndex;
            }

            if (bestSourceMiscSlot >= 0)
            {
                var sourceEqIndex = EquipmentIndex.Misc(bestSourceMiscSlot);
                inventory.equippedItems.Set(sourceEqIndex, equippedInTarget ?? InventoryItem.None);
                inventory.SetEquipment(bestItem, targetEqIndex);
                return slotIndex;
            }
        }

        if (equippedInTarget != null && !equippedInTarget.IsNone && !equippedInTarget.IsEmpty() && !equippedValid)
        {
            var moved = false;
            for (var m = 0; m < unlockedMiscCount; m++)
            {
                if (m == slotIndex)
                    continue;

                var otherEqIndex = EquipmentIndex.Misc(m);
                var otherItem = inventory.GetEquipment(otherEqIndex);
                if (otherItem == null || otherItem.IsNone || otherItem.IsEmpty())
                {
                    inventory.equippedItems.Set(targetEqIndex, InventoryItem.None);
                    inventory.SetEquipment(equippedInTarget, otherEqIndex);
                    moved = true;
                    break;
                }
            }

            if (!moved)
            {
                ScrapOrStore(inventory, equippedInTarget);
                inventory.equippedItems.Set(targetEqIndex, InventoryItem.None);
            }

            return -1;
        }

        return equippedValid ? slotIndex : -1;
    }

    private static bool IsDrink(InventoryItem item)
    {
        if (item == null || item.IsNone)
            return false;

        var dbItem = item.GetDataBaseItem() as DatabaseConsumable;
        return dbItem != null && dbItem.GetSubType() == DatabaseItem.SubType.Food &&
               dbItem.statusID == StatusEffect.ID.Drink;
    }

    private static bool IsFood(InventoryItem item)
    {
        if (item == null || item.IsNone)
            return false;

        var dbItem = item.GetDataBaseItem() as DatabaseConsumable;
        return dbItem != null && dbItem.GetSubType() == DatabaseItem.SubType.Food &&
               dbItem.statusID == StatusEffect.ID.Food;
    }

    private static bool IsHeal(InventoryItem item)
    {
        if (item == null || item.IsNone)
            return false;

        var dbItem = item.GetDataBaseItem();
        return dbItem != null && dbItem.GetSubType() == DatabaseItem.SubType.Healing;
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

        if (dbItem.GetSubType() == DatabaseItem.SubType.Food ||
            dbItem.GetSubType() == DatabaseItem.SubType.Healing) return;

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

    private static bool IsExplosiveThrowable(InventoryItem item)
    {
        var throwable = item.GetDataBaseItem() as DatabaseThrowable;
        return throwable != null && throwable.explosionID != Explosion.ID.None;
    }

    private static void DiscardNonExplosiveThrowables(PlayerMain player)
    {
        var inventory = player.inventory;

        foreach (var (equipped, eqIndex) in inventory.equippedItems.AllItemsIndexed())
        {
            if (equipped == null || equipped.IsNone ||
                equipped.GetDataBaseItem()?.GetSubType() != DatabaseItem.SubType.Throwable)
            {
                continue;
            }

            if (IsExplosiveThrowable(equipped))
                continue;

            if (equipped.GetDataBaseItem()?.CanScrap == true)
            {
                ScrappingUtils.ScrapItem(equipped, inventory);
            }
            else
            {
                RecordDrop(player, equipped);
                inventory.RemoveEquippedItem(eqIndex);
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

    private static void ScrapInferiorStorage(PlayerInventory inventory, EquipmentIndex eqIndex, float equippedScore)
    {
        foreach (var item in inventory.storage.items.ToArray())
        {
            if (item.id == InventoryItem.ID.None)
                continue;

            var dbItem = item.GetDataBaseItem();
            if (dbItem == null || !PlayerInventory.ItemTypeMatchesSlotIndex(item, eqIndex) ||
                dbItem.GetSubType() == DatabaseItem.SubType.Food
                || dbItem.GetSubType() == DatabaseItem.SubType.Healing)
            {
                continue;
            }

            var score = GetItemScoreForSlot(item);
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
        var score = (int)melee.tier * 100f;
        if (melee.meleePrefab == null ||
            !melee.meleePrefab.TryGetComponent<PhysicalMelee>(out var physicalMelee))
        {
            return score;
        }

        if (physicalMelee.MoveSet != null)
        {
            score += physicalMelee.MoveSet.GetDamageEstimate();
        }

        return score;
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

    private static float GetItemScoreForSlot(InventoryItem item)
    {
        if (item == null || item.IsNone)
            return 0f;

        var dbItem = item.GetDataBaseItem();
        if (dbItem == null)
            return 0f;

        var subType = dbItem.GetSubType();
        switch (subType)
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
                return consumable != null ? (int)dbItem.tier * 100f + consumable.effectAmount : (int)dbItem.tier * 100f;

            case DatabaseItem.SubType.Tool:
            case DatabaseItem.SubType.Misc:
            default:
                return 0f;
        }
    }

    public static int GetCurAmmoCount(PlayerMain player)
    {
        if (player?.inventory == null || player.arms == null)
            return 0;

        var equipment = player.inventory.GetEquipment(player.arms.selectedItem);
        return equipment?.ammo ?? 0;
    }

    public static int GetMaxAmmoCount(PlayerMain player)
    {
        if (player?.inventory == null || player.arms == null)
            return 0;

        var equipment = player.inventory.GetEquipment(player.arms.selectedItem);
        var database = equipment?.GetDataBaseItem() as DatabaseGun;
        return database != null ? database.maxAmmo : 0;
    }

    public static bool IsHoldingGun(PlayerMain player)
    {
        if (player?.arms == null || player.inventory == null)
            return false;

        var equipment = player.inventory.GetEquipment(player.arms.selectedItem);
        return equipment != null && !equipment.IsNone && equipment.GetDataBaseItem() is DatabaseGun;
    }

    public static bool IsHoldingMelee(PlayerMain player)
    {
        if (player?.arms == null || player.inventory == null)
            return false;

        var equipment = player.inventory.GetEquipment(player.arms.selectedItem);
        return equipment != null && !equipment.IsNone && equipment.GetDataBaseItem() is DatabaseMelee;
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

        foreach (var item in inventory.equippedItems.AllItems())
        {
            ClassifyItem(item, ref hasGun, ref hasMelee, ref hasFood, ref hasDrink, ref hasHeal);
        }

        foreach (var item in inventory.storage.items)
        {
            ClassifyItem(item, ref hasGun, ref hasMelee, ref hasFood, ref hasDrink, ref hasHeal);
        }
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

    public static int GetLootPriority(InventoryItem item, bool hasGun, bool hasFood, bool hasDrink, bool hasHeal,
        bool needPyreFuel)
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
                return 5;

            case DatabaseItem.SubType.Food
                when dbItem is DatabaseConsumable { statusID: StatusEffect.ID.Drink } && !hasDrink:
                return 4;

            case DatabaseItem.SubType.Food
                when dbItem is DatabaseConsumable { statusID: StatusEffect.ID.Food } && !hasFood:
                return 3;

            case DatabaseItem.SubType.Healing when !hasHeal:
                return 2;

            default:
                if (needPyreFuel && (item.id == InventoryItem.ID.Wood || item.id == InventoryItem.ID.DarkBlocks))
                    return 1;

                return 0;
        }
    }

    public static bool IsEquipSlotAvailable(PlayerMain playerMain, EquipmentIndex index)
    {
        var eq = playerMain?.inventory?.GetEquipment(index);
        return eq != null && !eq.IsNone && !eq.IsEmpty();
    }

    public static bool IsEquipSlotAvailable(PlayerMain playerMain, int slot)
    {
        var index = slot < 4 ? EquipmentIndex.Weapon(slot) : EquipmentIndex.Misc(slot - 4);
        return IsEquipSlotAvailable(playerMain, index);
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
