# Cool Glasses – VTOL VR Mod

A fun mod for [VTOL VR](https://store.steampowered.com/app/667970/VTOL_VR/). It replaces the standard
pilot helmet with a **photo-scanned 3D head** wearing **aviator sunglasses**.

- **Visor down:** the glasses flip down onto the eyes.
- **Visor up:** the glasses are pushed up onto the head.
- This applies to every pilot wearing the standard helmet (you, AI and other players), but **only in your
  own game**. The mod sends nothing over the network. The visor state comes from the game itself.
- Your own VR view hides the head and glasses, so you still see the normal visor.
- The F-45 helmet (F-45A, helicopters) is not replaced.

> **This repo is a template.** The head scans are **not** included because they are real people's
> faces. Without a scan the mod still builds and shows only the glasses. To add your own heads, see
> [Prompt/Hand-off.md](Prompt/Hand-off.md), a step-by-step guide written for working with Claude Code.

## Requirements

| What | Why |
|---|---|
| VTOL VR + **VTOL VR Mod Loader** (Steam) | The game and the mod loader. The DLL is built against the game's DLLs |
| [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer) | Building (`dotnet build`, targets net472) |
| [Blender](https://www.blender.org/) 4.x / 5.x | Converting a head scan (.glb) into the mod's format |
| optional: `dotnet tool install -g ilspycmd` | Decompiling the game's code to look up what something is called in the game |

## Building

```bash
dotnet build -c Release
```

The build automatically copies `CoolGlasses.dll` and `item.json` to
`VTOL VR\@Mod Loader\Mods\CoolGlasses\`. If VTOL VR is installed somewhere else:

```bash
dotnet build -c Release -p:VTOLDir="D:/SteamLibrary/steamapps/common/VTOL VR"
```

Then load the mod in the game under **Mods**.

## Adding a head

1. Scan a head, for example with a phone app such as Polycam, KIRI Engine or RealityScan, and export
   it as a **.glb with textures**. Head and neck only works best.
2. Convert it. This writes `Assets/head.bin` and `Assets/head_color/normal/ao.jpg`:

   ```bash
   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P Tools/prepare_head.py -- "C:/path/head.glb" Assets 2048
   ```

3. Rebuild, then fit the head in the game with the keys below.

## In-game keys (for fitting)

All values are saved automatically to `coolglasses.cfg` next to the DLL. Hold **Shift** for fine steps.

| Key | Glasses (without Ctrl) | Head (with **Ctrl**) |
|---|---|---|
| I / K | higher / lower | higher / lower |
| U / O | forward / back | forward / back |
| L / J | larger / smaller | larger / smaller |
| N / M | tilt | turn |
| Y / H | how far they flip up | pitch |
| T / G | how high they sit when flipped up | right / left |
| B | – | quarter turn |
| P | write values to the log | write values to the log |

The log is `%USERPROFILE%\AppData\LocalLow\Boundless Dynamics, LLC\VTOLVR\Player.log` (lines starting
with `[CoolGlasses]`).

## Project layout

| File | Contents |
|---|---|
| `Main.cs` | Mod loader entry point (`[ItemId]`). Creates the host object |
| `GlassesHost.cs` | Finds helmets, loads models and textures, handles keys and `coolglasses.cfg` |
| `HeadGlasses.cs` | Per helmet: hides the helmet, places the head and glasses, reads the visor |
| `Tools/prepare_head.py` | Blender script that converts a .glb into `head.bin` and textures |
| `Assets/glasses.bin` | The glasses model |
| `item.json` | Name and description for the mod loader |

## Privacy

Head scans are biometric data of real people. Only scan people who agree to it, and **don't** upload
the scans to a public repo or the Steam Workshop. That's why the `.gitignore` keeps `Assets/head*`,
`*.glb`, `*.fbx` and `*.blend` out of Git.
