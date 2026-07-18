using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ZumbiBots.Components;

namespace ZumbiBots;

[BepInPlugin(PluginGuid, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class ZumbiBots : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;
    public const string PluginGuid = "com.theblackvoid.zumbibots";
    private readonly Harmony _harmony = new(PluginGuid);

    private void Awake()
    {
        Logger = base.Logger;
        try
        {
            gameObject.AddComponent<BotMenu>();
            gameObject.AddComponent<BotManager>();
            gameObject.AddComponent<BotGameManager>();
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
}
