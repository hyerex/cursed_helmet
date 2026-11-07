# Hand-off: Your own head mod with Claude Code

**Goal:** your own VTOL VR mod based on *Cool Glasses*. Each of your friends gets their own 3D head.
When you fly together in multiplayer, every pilot wears the head of the friend flying it.

This guide is split into steps. Each step has a **prompt** you can paste into Claude Code. Give Claude
one step at a time, and test in the game before you move on.

---

## 0. Setup (once, without Claude)

Install:

- **VTOL VR** and the **VTOL VR Mod Loader** (via Steam). Start the game once with the mod loader.
- **.NET SDK** 8 or newer: <https://dotnet.microsoft.com/download>
- **Blender** 4.x or 5.x: <https://www.blender.org/>
- **Git** and **Claude Code**: <https://claude.com/claude-code>
- Optional but helpful: `dotnet tool install -g ilspycmd`. It lets Claude look up what classes and
  fields are called in the game's code.

Then:

1. On GitHub, click **"Use this template"** (or fork the repo) to create your own repo.
2. Clone it and start Claude Code in the folder:

   ```bash
   git clone https://github.com/<your-name>/<your-repo>.git
   cd <your-repo>
   claude
   ```

Claude reads `CLAUDE.md` automatically. It explains how to build and test, and the mod loader's quirks.

---

## 1. Make the mod your own

> **Prompt:**
> ```
> Read README.md and CLAUDE.md. Turn this project into my own mod:
> - New name: "<MyModName>" (assembly, namespace, csproj file name, log prefix, cfg file name)
> - New ItemId: "<yourname>.<mymodname>" in Main.cs
> - item.json: a matching title and description
> - Version 0.1.0
> Check whether VTOL VR is installed at the default path on my PC; if not, ask me for the path.
> Then build the project with dotnet build and tell me whether it worked.
> ```

**Test:** start the game and load your mod under *Mods*. The helmet should disappear and the glasses
should appear (with no head yet, because there is no scan).

---

## 2. Scan the heads

No program does this for you. It's manual work:

1. **Ask your friends for their permission.** It's their faces.
2. Scan each head with a phone app such as **Polycam**, **KIRI Engine** or **RealityScan**.
   Tips: even lighting, the person sits still, and you walk slowly once all the way around them.
3. Export as **.glb** (glTF binary) **with textures**. FBX often loses the textures.
4. Optional, in Blender: cut away everything below the neck. The head should be upright.
5. Put the files somewhere like `Scans/<name>.glb`. The `.gitignore` keeps `*.glb` out of Git.

---

## 3. Add the first head

> **Prompt:**
> ```
> I have a head scan at Scans/<name>.glb. Convert it with Tools/prepare_head.py (Blender,
> headless) into Assets/head.bin and the textures, rebuild the mod, and tell me how big the
> model is and which way the face points. If you're not sure about the direction, render a
> few images from the front and side with Blender and look at them. Then adjust the default
> HeadYaw/HeadPitch values in GlassesHost.cs.
> ```

**Test in the game:** fit the head and glasses with the keys (see the README: **Ctrl** + keys for
the head, without Ctrl for the glasses). Then press **Ctrl+P** and **P**. Then:

> **Prompt:**
> ```
> I fitted the head in the game. Read the latest [MyModName] lines in Player.log and make
> those values the new defaults in the code.
> ```

---

## 4. One head per friend (the main part)

Right now every pilot has the same head. This step gives each player their own.

**The idea:**
- The heads are **no longer** built into the DLL. They load at runtime from a folder next to the
  DLL: `heads/<name>/head.bin`, `head_color.jpg` and so on. Adding a friend then needs no rebuild,
  and the DLL contains no faces.
- A `heads.cfg` file maps **SteamIDs** to heads, for example `76561198000000000 = max`.
- Each head has its own fitting values (height, size, rotation), because every scan is different.

> **Prompt:**
> ```
> Read CLAUDE.md. I want a separate head per player. Plan first, then build:
>
> 1. Load heads at runtime instead of embedding them: a "heads" folder next to the DLL, with one
>    subfolder per head containing head.bin, head_color.jpg, head_normal.jpg, head_ao.jpg and a
>    head.cfg with the fitting values (forward, up, right, scale, yaw, pitch). Each head is
>    loaded only once (cache the mesh and material).
> 2. heads.cfg in the mod folder: lines "SteamID = foldername" plus a line "default = foldername"
>    (or "default = none" for the plain glasses without a head), which also applies to AI pilots.
> 3. For each helmet, find out who it belongs to: your own helmet (HelmetController) belongs to
>    BDSteamClient.mySteamID; for other players use PlayerModelSync.netEntity.ownerID
>    (HeadGlasses.FindVisor already finds the matching PlayerModelSync).
>    AI pilots get the default head.
> 4. The Ctrl keys adjust the head closest to the camera (or my own head in the external view)
>    and save to that head's head.cfg. Ctrl+P also logs which head it is and its SteamID.
> 5. When a player spawns, log "SteamID <id> (<pilot name>) -> head <name>", so I can read my
>    friends' SteamIDs from the log.
> 6. Extend Tools/prepare_head.py, or write a small script, that converts a scan straight into
>    heads/<name>/, and explain in the README how to add a friend.
>
> First check with ilspycmd that those fields really have those names.
> Build and deploy at the end. The heads folders must be in .gitignore.
> ```

**Test:** join a multiplayer lobby with a friend. Your friend doesn't need the mod, because only you
see the heads. The log shows the SteamIDs, which you add to `heads.cfg`. If your friend should see
the heads too, they need the mod **and** the `heads` folders.

---

## 5. Ideas for later (optional)

- **Your own glasses or a hat:** a different model instead of `glasses.bin`, such as a cap. Prompt:
  *"I have a model at Scans/hat.glb. Use it instead of the glasses, with its own Blender script like
  prepare_head.py."*
- **A different accessory per friend**, set in the same `head.cfg`.
- **Replace the F-45 helmet** too (today only the standard helmet is replaced).

---

## Troubleshooting

- Give Claude the error, or let it look for itself: *"Read the last 200 lines of Player.log and tell
  me what's going wrong."*
- The head faces the wrong way or sticks into the seat: fit it in the game with **Ctrl** + keys.
  **Ctrl+B** turns it 90°.
- The head is invisible: check that the files are in the right folder and what the log says.
- You deliberately **can't** see your own head in your VR view. Test with the external camera or
  the spectator camera.

## Rules

- **Never upload scans to GitHub or the Steam Workshop.** The `.gitignore` guards against it, but
  check what's included with `git status` before every `git push`.
- Share the `heads` folders only privately, with friends who have agreed to it.
