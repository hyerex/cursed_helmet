# Cool Glasses – VTOL VR Mod

Ein Spaß-Mod für [VTOL VR](https://store.steampowered.com/app/667970/VTOL_VR/): Der Standard-Pilotenhelm
wird durch einen **fotogescannten 3D-Kopf** mit einer **Aviator-Sonnenbrille** ersetzt.

- **Visier runter:** Die Brille klappt auf die Augen.
- **Visier hoch:** Die Brille wird auf den Kopf geschoben.
- Das gilt für alle Piloten mit dem Standardhelm (du, KI, andere Spieler), aber **nur in deinem eigenen Spiel**.
  Der Mod sendet nichts übers Netzwerk. Der Visierzustand kommt aus dem Spiel selbst.
- In deiner eigenen VR-Sicht sind Kopf und Brille ausgeblendet. Du siehst weiter das normale Visier.
- Der F-45-Helm (F-45A, Helikopter) wird nicht ersetzt.

> **Dieses Repo ist eine Vorlage.** Die Kopf-Scans sind **nicht** enthalten, weil es echte Gesichter
> sind. Ohne Scan baut der Mod trotzdem und zeigt nur die Brille. Wie du deine eigenen Köpfe einbaust,
> steht in [Prompt/Hand-off.md](Prompt/Hand-off.md). Die Anleitung ist für die Arbeit mit Claude Code geschrieben.

## Voraussetzungen

| Was | Wofür |
|---|---|
| VTOL VR + **VTOL VR Mod Loader** (Steam) | Spiel und Mod-Loader, die DLLs werden gegen die Spiel-DLLs gebaut |
| [.NET SDK](https://dotnet.microsoft.com/download) (8 oder neuer) | Bauen (`dotnet build`, Ziel ist net472) |
| [Blender](https://www.blender.org/) 4.x / 5.x | Kopf-Scan (.glb) in das Mod-Format umwandeln |
| optional: `dotnet tool install -g ilspycmd` | Spielcode dekompilieren, um nachzusehen, wie etwas im Spiel heißt |

## Bauen

```bash
dotnet build -c Release
```

Der Build kopiert `CoolGlasses.dll` und `item.json` automatisch nach
`VTOL VR\@Mod Loader\Mods\CoolGlasses\`. Ist VTOL VR woanders installiert:

```bash
dotnet build -c Release -p:VTOLDir="D:/SteamLibrary/steamapps/common/VTOL VR"
```

Danach im Spiel unter **Mods** den Mod laden.

## Einen Kopf einbauen

1. Einen Kopf scannen, z. B. mit einer Handy-App wie Polycam, KIRI Engine oder RealityScan, und als
   **.glb mit Texturen** exportieren. Am besten nur Kopf und Hals.
2. Umwandeln (schreibt `Assets/head.bin` und `Assets/head_color/normal/ao.jpg`):

   ```bash
   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P Tools/prepare_head.py -- "C:/pfad/kopf.glb" Assets 2048
   ```

3. Neu bauen und im Spiel mit den Tasten unten einpassen.

## Tasten im Spiel (zum Einpassen)

Alle Werte werden automatisch in `coolglasses.cfg` neben der DLL gespeichert. **Shift** = feine Schritte.

| Taste | Brille (ohne Strg) | Kopf (mit **Strg**) |
|---|---|---|
| I / K | höher / tiefer | höher / tiefer |
| U / O | vor / zurück | vor / zurück |
| L / J | größer / kleiner | größer / kleiner |
| N / M | neigen | drehen |
| Y / H | wie weit sie hochklappt | kippen |
| T / G | wie hoch sie hochgeklappt sitzt | rechts / links |
| B | – | Vierteldrehung |
| P | Werte ins Log | Werte ins Log |

Log: `%USERPROFILE%\AppData\LocalLow\Boundless Dynamics, LLC\VTOLVR\Player.log` (Einträge mit `[CoolGlasses]`).

## Projektaufbau

| Datei | Inhalt |
|---|---|
| `Main.cs` | Einstieg für den Mod-Loader (`[ItemId]`), legt das Host-Objekt an |
| `GlassesHost.cs` | Sucht Helme, lädt Modelle und Texturen, Tasten, `coolglasses.cfg` |
| `HeadGlasses.cs` | Pro Helm: Helm ausblenden, Kopf und Brille platzieren, Visier auslesen |
| `Tools/prepare_head.py` | Blender-Skript: .glb → `head.bin` und Texturen |
| `Assets/glasses.bin` | Das Brillenmodell |
| `item.json` | Name und Beschreibung für den Mod-Loader |

## Datenschutz

Kopf-Scans sind biometrische Daten echter Menschen. Scanne nur Leute, die einverstanden sind, und
lade die Scans **nicht** in ein öffentliches Repo oder in den Steam Workshop. Die `.gitignore` hält
`Assets/head*`, `*.glb`, `*.fbx` und `*.blend` deshalb aus Git heraus.
