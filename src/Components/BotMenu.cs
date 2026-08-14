using System;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Components;

public class BotMenu : MonoBehaviour
{
    public static bool DisableThinking;
    public static bool EnableDebug;
    private bool _showGui;
    private Rect _windowRect = new(Screen.width / 2f - 100f, Screen.height / 2f - 105f, 200f, 200f);
    private bool _isDragging;
    private Vector2 _dragOffset;
    private bool _isListeningForKey;

    private void Update()
    {
        if (!_isListeningForKey && Input.GetKeyDown(Configuration.MenuToggleKey.Value) && BotManager.BotIsAvailable)
        {
            _showGui = !_showGui;
        }
    }

    private void OnGUI()
    {
        if (!_showGui)
        {
            _isListeningForKey = false;
            return;
        }

        if (!BotManager.BotIsAvailable)
        {
            _showGui = false;
            _isListeningForKey = false;
            return;
        }

        var curEvent = Event.current;
        if (_isListeningForKey && curEvent.isKey && curEvent.type == EventType.KeyDown)
        {
            if (curEvent.keyCode != KeyCode.None)
            {
                if (curEvent.keyCode != KeyCode.Escape)
                {
                    Configuration.MenuToggleKey.Value = curEvent.keyCode;
                }

                _isListeningForKey = false;
                curEvent.Use();
            }
        }

        switch (curEvent.type)
        {
            case EventType.MouseDown when
                new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 25f).Contains(curEvent.mousePosition):
                _isDragging = true;
                _dragOffset = curEvent.mousePosition - _windowRect.position;
                curEvent.Use();
                break;

            case EventType.MouseUp:
                _isDragging = false;
                break;
        }

        if (_isDragging && curEvent.type == EventType.MouseDrag)
        {
            _windowRect.position = curEvent.mousePosition - _dragOffset;
            curEvent.Use();
        }

        GUILayout.BeginArea(_windowRect, GUI.skin.box);
        GUILayout.Label($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} Beta", GUILayout.Height(20f));
        GUILayout.Label($"Bot Amount: {BotManager.BotQuota}");

        var keybindText = _isListeningForKey ? "Press Any Key..." : $"Menu Key: {Configuration.MenuToggleKey.Value}";
        if (GUILayout.Button(keybindText))
        {
            _isListeningForKey = !_isListeningForKey;
        }

        if (GUILayout.Button("Add a Bot"))
        {
            ModifyBotAmount(1);
        }

        if (GUILayout.Button("Remove a Bot"))
        {
            ModifyBotAmount(-1);
        }

        if (GUILayout.Button("Teleport Bots"))
        {
            TeleportBots();
        }

        DisableThinking = GUILayout.Toggle(DisableThinking, "Disable Thinking");
        EnableDebug = GUILayout.Toggle(EnableDebug, "Enable Debug");

        GUILayout.EndArea();
    }

    private static void ModifyBotAmount(int amount)
    {
        var isSinglePlayer = MultiplayerController.instance?.IsSinglePlayer ?? true;
        var humans = (LobbyController.instance?.players?.Count ?? 1) - BotManager.BotLobbyIDs.Count;
        var maxPlayers = ServerController.instance?.ConfiguredMaxPlayers ?? 6;
        var maxBots = isSinglePlayer ? 5 : Math.Max(0, maxPlayers - humans);
        BotManager.BotQuota = Math.Clamp(BotManager.BotQuota + amount, 0, maxBots);
    }

    private static void TeleportBots()
    {
        Logging.DebugLog("Teleporting bots...");

        var myPlayer = PlayersController.instance?.MyPlayer();
        if (myPlayer == null)
        {
            Logging.DebugLog("MyPlayer not found!", LogLevel.Warning);
            return;
        }

        var targetPos = myPlayer.transform.position;
        var players = PlayersController.instance?.players;
        if (players == null)
            return;

        foreach (var player in players)
        {
            if (player == null)
                continue;

            if (!Helpers.IsBot(player))
                continue;

            player.transform.position = targetPos;
            if (player.movement == null || player.movement.body == null)
                continue;

            player.movement.body.position = targetPos;
            player.movement.body.linearVelocity = Vector3.zero;
        }
    }
}
