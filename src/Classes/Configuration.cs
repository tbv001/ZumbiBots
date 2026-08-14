using BepInEx.Configuration;
using UnityEngine;

namespace ZumbiBots.Classes;

public static class Configuration
{
    public static ConfigEntry<KeyCode> MenuToggleKey;

    public static void RegisterConfig(ConfigFile config)
    {
        MenuToggleKey = config.Bind("Settings", "MenuToggleKey", KeyCode.P, "Toggle the bot menu");
    }
}
