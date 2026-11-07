# CLAUDE.md – notes for Claude Code

A VTOL VR mod (Unity 2020.3, C#, net472) for the VTOL VR Mod Loader. It replaces the standard pilot
helmet with a scanned 3D head and aviator glasses that follow the visor. See README.md for the
features and Prompt/Hand-off.md for the planned work.

## Build and test

- `dotnet build -c Release`. It references the game's DLLs from `$(VTOLDir)` and deploys to
  `$(VTOLDir)/@Mod Loader/Mods/<AssemblyName>/`. Override with `-p:VTOLDir=...`.
- Testing only works in the game (VR or desktop), so ask the user to try it. Read the log afterwards:
  `%USERPROFILE%\AppData\LocalLow\Boundless Dynamics, LLC\VTOLVR\Player.log`, lines with `[CoolGlasses]`.
- To look up game classes, decompile them:
  `ilspycmd -t <ClassName> "<VTOLDir>/VTOLVR_Data/Managed/Assembly-CSharp.dll"`.
- Blender runs headless: `blender.exe -b --factory-startup -P Tools/prepare_head.py -- <in.glb> <outDir> [size]`.

## Mod loader facts (learned the hard way)

- The entry class derives from `VtolMod` with `[ItemId("...")]`. It gets **no Update()**, so the mod
  creates a `DontDestroyOnLoad` GameObject (`GlassesHost`) for Update and key input.
- The loader applies Harmony patches by itself. **Don't call `PatchAll()`**, or every patch runs twice.
  Harmony **Finalizers fail** in VTOL VR (IL compile error), so use Prefix/Postfix only.
- `UnLoad()` must undo everything (destroy objects and restore hidden renderers).

## Game facts used by the mod

- Standard helmet: a MeshFilter named `helmet` with an `hqh_dif` texture under the root `hqh`.
  Visor materials start with `mat_hqHelmetVisor`. Helmet frame in hqh space: front = -z, up = +y.
- The game's head is a sphere `Helmet/headSphere`, which the mod hides.
- Visor: your own helmet uses `HelmetController.visorTime` (private, 0 = down, 0.999 = up). Remote
  players use `PlayerModelSync.remoteHelmetAnimator.GetT()` (1 = up). AI has neither.
- Players: `PlayerModelSync` is a `VTNetSync`, and `pms.netEntity.ownerID` is the player's SteamID
  (ulong). Your own SteamID is `BDSteamClient.mySteamID`. `VTOLMPLobbyManager.GetPlayer(steamId)`
  returns the `PlayerInfo` (check its fields with ilspycmd before you use them).
- Per-camera visibility goes through `Camera.onPreCull`: a camera inside the head doesn't see the
  head or the glasses.

## Conventions

- Assets are embedded resources (see the csproj). Head files are optional (`Condition="Exists(...)"`).
- Never commit personal scans (`Assets/head*`, `*.glb`, `*.fbx`). They are real people's faces.
- head.bin format: int vertexCount; per vertex 8 floats (pos xyz, normal xyz, uv), already in Unity
  axes and centred; int indexCount; int indices.
