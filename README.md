![ZumbiBots](https://github.com/tbv001/ZumbiBots/blob/main/images/ZumbiBots.png)

Player AI/Bots for **Zumbi Blocks 2** to simulate online gameplay without actual human players.

## Features

- Bots can use all firearms, melee weapons, and explosive throwables
- Bots prioritize loot based on their needs (weapons, meds, food, water)
- Bots can engage bosses required for the wave to progress
- Bots can light up braziers
- Bots will help teammates defeat active bosses
- Bots will try to revive downed teammates if possible
- Bots manage their own inventory; they will scrap or drop useless items, and equip stronger equipment
- Bots can interact with the rescue helicopter to start the rescue sequence and ride to escape
- Randomized bot loadouts and character customization
- Easy-to-use bot menu for managing bots during gameplay

## Installation

1. Install the latest version of [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) in your Zumbi Blocks 2 installation if not already installed. Make sure to run the game at least once after installing for BepInEx to generate the necessary directories
2. Download the latest version of this mod from the [releases page](https://github.com/tbv001/ZumbiBots/releases/latest)
3. Extract the contents of the `ZumbiBots.zip` file to the `BepInEx\plugins\` directory located in your Zumbi Blocks 2 installation directory (e.g., `C:\Program Files (x86)\Steam\steamapps\common\Zumbi Blocks 2 Open Alpha\BepInEx\plugins\`)
4. Launch the game

## Usage

In order to add bots to the game, you need to be the server host. Once you've started your lobby or the game has started (either in singleplayer or multiplayer), you can use the bot menu to add and manage bots. To open the bot menu, press the **P** key on your keyboard.

## Building

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (netstandard2.1)

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
