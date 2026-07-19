using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Components;

public class BotManager : MonoBehaviour
{
    private static readonly Dictionary<int, LoadoutDescriptor[]> BotItems = [];
    private static readonly HashSet<int> AppliedLoadoutThisSession = [];
    private static readonly HashSet<string> UsedBotNames = [];
    private static string[] _botNames;
    public static List<int> BotLobbyIDs = [];
    public static bool BotIsAvailable = false;
    public static int BotQuota;

    private static void LoadBotNames()
    {
        if (_botNames != null)
            return;

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyPath);
        if (assemblyDir == null)
            return;

        var namesPath = Path.Combine(assemblyDir, "names.txt");
        _botNames = File.Exists(namesPath) ? File.ReadAllLines(namesPath) : ["Bot"];
    }

    public static void AddBot()
    {
        LoadBotNames();

        var availableNames = new List<string>();
        foreach (var name in _botNames)
        {
            if (!UsedBotNames.Contains(name))
                availableNames.Add(name);
        }

        var botName = availableNames.Count > 0
            ? availableNames[Random.Range(0, availableNames.Count)]
            : _botNames[Random.Range(0, _botNames.Length)];

        UsedBotNames.Add(botName);
        var lobby = LobbyController.instance;
        var speaker = ServerController.instance.GetSpeaker;
        var skinId = Randomization.GetRandomSkin();
        var gender = Randomization.GetRandomGender();
        var id = lobby.AddPlayer(null, botName, skinId, gender,
            AppearanceColorsHandler.AllocateColorsWithDefaults(skinId),
            0, true);
        var bot = lobby.GetPlayerByLobbyID(id);
        bot.type = LobbyPlayer.Type.Client;
        bot.SetReady(true);
        BotLobbyIDs.Add(id);
        speaker.AddLobbyPlayer(id);
        AssignRandomLoadout(id);
        Logging.DebugLog($"Bot {botName} joined with LobbyID {id}");

        if (MatchController.instance?.state != MatchController.MatchState.InGame)
            return;

        var playerObj = MatchController.instance.SpawnPlayerWithLobbyReference(bot);
        if (BotItems.TryGetValue(id, out var midDescriptors))
        {
            ApplyBotLoadout(playerObj.inventory, midDescriptors);
            AppliedLoadoutThisSession.Add(id);
        }

        var spawnPoint = MatchController.instance.respawn.GetBaseSpawnPoint();
        playerObj.RespawnAt(spawnPoint);
        bot.readyState = PlayerReadyState.InGame;
        speaker.SyncPlayerInstantiate(playerObj, id);
        InterestPointController.instance.AddRespawnPoint(spawnPoint);
    }

    public static void RemoveBot()
    {
        var lobby = LobbyController.instance;
        if (lobby == null) return;

        var lobbyId = BotLobbyIDs[^1];
        var botToRemove = lobby.GetPlayerByLobbyID(lobbyId);
        if (botToRemove == null)
            return;

        var botName = botToRemove.playerName;
        PlayersController.instance?.DeletePlayer(lobbyId);
        ServerController.instance?.GetSpeaker?.RemoveLobbyPlayer(lobbyId, true);
        lobby.RemovePlayerByLobbyID(lobbyId);
        BotLobbyIDs.Remove(lobbyId);
        BotItems.Remove(lobbyId);
        UsedBotNames.Remove(botName);
        Logging.DebugLog($"Bot {botName} with LobbyID {lobbyId} removed.");
    }

    private void Update()
    {
        if (!BotIsAvailable)
        {
            if (BotLobbyIDs.Count <= 0)
                return;

            Logging.DebugLog("Bots are now unavailable. Cleaning up...");
            for (var i = BotLobbyIDs.Count - 1; i >= 0; i--)
            {
                RemoveBot();
            }

            BotLobbyIDs.Clear();
            BotItems.Clear();
            AppliedLoadoutThisSession.Clear();
            UsedBotNames.Clear();
            BotQuota = 0;

            return;
        }

        switch (MatchController.instance?.state)
        {
            case MatchController.MatchState.Lobby:
            {
                AppliedLoadoutThisSession.Clear();

                var lobby = LobbyController.instance;
                if (lobby != null)
                {
                    foreach (var lobbyId in BotLobbyIDs)
                    {
                        var player = lobby.GetPlayerByLobbyID(lobbyId);
                        if (player is { IsReady: false })
                            player.SetReady(true);
                    }
                }

                break;
            }
            case MatchController.MatchState.InGame:
            {
                var lobby = LobbyController.instance;
                if (lobby != null)
                {
                    foreach (var lobbyId in BotLobbyIDs)
                    {
                        if (AppliedLoadoutThisSession.Contains(lobbyId))
                            continue;

                        if (!BotItems.TryGetValue(lobbyId, out var inGameDescriptors))
                            continue;

                        var player = lobby.GetPlayerByLobbyID(lobbyId);
                        if (player?.playerObj == null)
                            continue;

                        ApplyBotLoadout(player.playerObj.inventory, inGameDescriptors);
                        AppliedLoadoutThisSession.Add(lobbyId);
                    }
                }

                break;
            }
        }

        if (BotLobbyIDs.Count < BotQuota)
        {
            AddBot();
        }
        else if (BotLobbyIDs.Count > BotQuota && BotLobbyIDs.Count > 0)
        {
            RemoveBot();
        }
    }

    private static readonly Dictionary<LoadoutSelector.SlotID, LoadoutID[]> FixedLoadouts = new()
    {
        {
            LoadoutSelector.SlotID.Utility,
            [
                LoadoutID.Dynamite,
                LoadoutID.Frag
            ]
        },
        {
            LoadoutSelector.SlotID.Resources,
            [
                LoadoutID.ConsumablesTier3,
                LoadoutID.ConsumablesTier2,
                LoadoutID.ConsumablesTier1
            ]
        }
    };

    private static void AssignRandomLoadout(int lobbyId)
    {
        var allDescriptor = LoadoutDatabase.Instance.AllDescriptors;
        var slotIds = new[]
        {
            LoadoutSelector.SlotID.Primary,
            LoadoutSelector.SlotID.Secondary,
            LoadoutSelector.SlotID.Melee,
            LoadoutSelector.SlotID.Utility,
            LoadoutSelector.SlotID.Resources
        };

        var descriptors = new LoadoutDescriptor[5];
        var items = new InventoryItem.ID[5];
        var totalTier = 0;
        for (var i = 0; i < 5; i++)
        {
            LoadoutDescriptor chosen;

            if (FixedLoadouts.TryGetValue(slotIds[i], out var fixedIds))
            {
                chosen = LoadoutDatabase.Instance.GetDescriptor(fixedIds[Random.Range(0, fixedIds.Length)]);
            }
            else
            {
                var slotOptions = new List<LoadoutDescriptor>();
                foreach (var descriptor in allDescriptor)
                {
                    if (!Pricing.IsBlocked(descriptor) && LoadoutSelector.instance.GetSlotID(descriptor) == slotIds[i])
                        slotOptions.Add(descriptor);
                }

                chosen = slotOptions.Count > 0 ? slotOptions[Random.Range(0, slotOptions.Count)] : null;
            }

            descriptors[i] = chosen;
            items[i] = chosen?.GetPropID() ?? InventoryItem.ID.None;
            totalTier += chosen?.TierNumber ?? 0;
        }

        var availablePerks = new List<PerkID>();
        foreach (PerkID perk in System.Enum.GetValues(typeof(PerkID)))
        {
            var perkLoadout = LoadoutDatabase.Instance.FindPerkLoadout(perk);
            if (perkLoadout != null && !Pricing.IsBlocked(perkLoadout))
                availablePerks.Add(perk);
        }

        var selectedPerks = new List<PerkID>();
        for (var i = 0; i < Perks.BaseMaxPerks; i++)
        {
            var perkIdx = Random.Range(0, availablePerks.Count);
            var perk = availablePerks[perkIdx];
            availablePerks.RemoveAt(perkIdx);
            selectedPerks.Add(perk);
        }

        foreach (var perk in selectedPerks)
        {
            var perkLoadout = LoadoutDatabase.Instance.FindPerkLoadout(perk);
            if (perkLoadout != null)
                totalTier += perkLoadout.TierNumber;
        }

        var lobby = LobbyController.instance;
        var player = lobby?.GetPlayerByLobbyID(lobbyId);
        if (player != null)
        {
            player.loadoutLevel = totalTier;
            player.updatedLobbyLoadout = true;
            player.perks = selectedPerks;
        }

        BotItems[lobbyId] = descriptors;

        var itemNames = new string[5];
        for (var i = 0; i < 5; i++)
        {
            itemNames[i] = descriptors[i] != null
                ? $"{descriptors[i].GetName()} (Tier {descriptors[i].TierNumber})"
                : items[i].ToString();
        }

        Logging.DebugLog(
            $"Bot {lobbyId} loadout: [{string.Join(", ", itemNames)}] Perks: [{string.Join(", ", selectedPerks)}] Total Tier: {totalTier}");

        var index = lobby?.GetPlayerIndex(lobbyId) ?? -1;
        if (index >= 0)
        {
            lobby?.lobbyMenu.slots[index].UpdateWithLoadout(player, totalTier, items);
        }

        ServerController.instance?.GetSpeaker?.BroadcastLobbyLoadout(-1, lobbyId, totalTier, items, selectedPerks);
    }

    private static void ApplyBotLoadout(PlayerInventory inventory, LoadoutDescriptor[] descriptors)
    {
        foreach (var desc in descriptors)
        {
            if (desc == null)
                continue;

            if (desc is ResourcesLoadoutDescriptor resourcesDesc)
            {
                foreach (var pack in resourcesDesc.Resources)
                {
                    var item = new InventoryItem(pack.itemID)
                    {
                        stackCount = pack.itemQuantity
                    };
                    inventory.AddItem(item, true, true, true);
                }
            }
            else
            {
                var propId = desc.GetPropID();
                if (propId == InventoryItem.ID.None)
                    continue;

                var dbItem = ItemsBase.instance.GetItem(propId);
                var equipIndex = (int)dbItem.GetSubType();
                if (equipIndex < 0 || equipIndex >= inventory.equippedItem.Length)
                    continue;

                var invItem = new InventoryItem(propId)
                {
                    stackCount = 1
                };

                if (dbItem is DatabaseThrowable)
                    invItem.stackCount = dbItem.stackMax / 2;

                if (dbItem is DatabaseGun dbGun)
                    invItem.ammo = dbGun.maxAmmo;

                inventory.equippedItem[equipIndex] = invItem;
            }
        }

        inventory.OnEquipmentChanged();
    }
}
