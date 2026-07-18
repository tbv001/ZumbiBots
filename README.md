![ZumbiBots](https://github.com/tbv001/ZumbiBots/blob/main/images/ZumbiBots.png)

Player AI/Bots for **Zumbi Blocks 2** to simulate online gameplay without actual human players.

## Features

- They can finish the game by themselves
- Capable enough to survive and wipe hordes on their own
- They prioritize helping teammates defeat active bosses
- Randomization for the bots' loadout and character customization
- Easy to use bot menu to manage bots during gameplay

## Installation

1. Make sure you have the latest version of [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) installed in your game
2. Download the latest release from the [releases page](https://github.com/tbv001/ZumbiBots/releases/latest)
3. Extract the contents of the `ZumbiBots.zip` file to the game directory
4. Launch the game

## Usage

In order to add bots to the game, you need to be the server host. Once you've started your lobby or the game has started (either in singleplayer or multiplayer), you can use the bot menu to add and manage bots. To open the bot menu, press the **P** key on your keyboard.

## Building

### Prerequisites

Requires [.NET SDK](https://dotnet.microsoft.com/download) (netstandard2.1).

### Project References

The project references `Assembly-CSharp.dll` from your Steam installation. Make sure Zumbi Blocks 2 is installed at the default Steam path, or update the path in `src/ZumbiBots.csproj`:

```xml
<AssemblyCSharpPath>$(SteamInstallPath)\steamapps\common\Zumbi Blocks 2 Open Alpha\ZumbiBlocks2_Data\Managed\Assembly-CSharp.dll</AssemblyCSharpPath>
```

### Compiling

Build from the repository root:

```
dotnet build -c Release ./src
```

The output DLL will be in `src/bin/Release/netstandard2.1/`.

## Showcases

![Showcase1](https://github.com/tbv001/ZumbiBots/blob/main/images/Showcase1.png)

![Showcase2](https://github.com/tbv001/ZumbiBots/blob/main/images/Showcase2.png)

![Showcase3](https://github.com/tbv001/ZumbiBots/blob/main/images/Showcase3.png)

## License

This project is licensed under the **MIT License** - see the [LICENSE](https://github.com/tbv001/ZumbiBots/blob/main/LICENSE) file for details.
