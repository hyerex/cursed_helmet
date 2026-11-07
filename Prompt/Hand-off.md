# Hand-off: Dein eigener Kopf-Mod mit Claude Code

**Ziel:** Ein eigener VTOL-VR-Mod auf Basis von *Cool Glasses*. Jeder deiner Freunde bekommt seinen
eigenen 3D-Kopf. Fliegt ihr zusammen im Multiplayer, sitzt auf jedem Piloten der Kopf des Freundes,
der ihn steuert.

Diese Anleitung ist in Schritte geteilt. Zu jedem Schritt gibt es einen **Prompt**, den du in Claude
Code einfügen kannst. Gib Claude immer einen Schritt nach dem anderen und teste im Spiel, bevor du
weitermachst.

---

## 0. Vorbereitung (einmalig, ohne Claude)

Installiere:

- **VTOL VR** und den **VTOL VR Mod Loader** (über Steam). Starte das Spiel einmal mit dem Mod Loader.
- **.NET SDK** 8 oder neuer: <https://dotnet.microsoft.com/download>
- **Blender** 4.x oder 5.x: <https://www.blender.org/>
- **Git** und **Claude Code**: <https://claude.com/claude-code>
- Optional, aber hilfreich: `dotnet tool install -g ilspycmd`. Damit kann Claude im Spielcode
  nachsehen, wie Klassen und Felder heißen.

Dann:

1. Auf GitHub **„Use this template“** klicken (oder das Repo forken) und dein eigenes Repo anlegen.
2. Klonen und Claude Code im Ordner starten:

   ```bash
   git clone https://github.com/<dein-name>/<dein-repo>.git
   cd <dein-repo>
   claude
   ```

Claude liest `CLAUDE.md` automatisch. Darin steht, wie man baut, testet und welche Eigenheiten der
Mod Loader hat.

---

## 1. Den Mod zu deinem eigenen machen

> **Prompt:**
> ```
> Lies README.md und CLAUDE.md. Mach dieses Projekt zu meinem eigenen Mod:
> - Neuer Name: "<MeinModName>" (Assembly, Namespace, csproj-Dateiname, Log-Präfix, cfg-Dateiname)
> - Neue ItemId: "<deinname>.<meinmodname>" in Main.cs
> - item.json: Titel und Beschreibung passend
> - Version auf 0.1.0
> Prüf, ob VTOL VR bei mir unter dem Standardpfad installiert ist, sonst frag mich nach dem Pfad.
> Bau das Projekt danach mit dotnet build und sag mir, ob es geklappt hat.
> ```

**Test:** Spiel starten, unter *Mods* deinen Mod laden. Der Helm sollte verschwinden und die Brille
erscheinen (noch ohne Kopf, weil noch kein Scan da ist).

---

## 2. Köpfe scannen

Das macht kein Programm für dich. Das ist Handarbeit:

1. **Frag deine Freunde, ob sie einverstanden sind.** Es sind ihre Gesichter.
2. Scanne jeden Kopf mit einer Handy-App, z. B. **Polycam**, **KIRI Engine** oder **RealityScan**.
   Tipps: gleichmäßiges Licht, die Person sitzt still, du gehst einmal langsam rundherum.
3. Exportiere als **.glb** (glTF Binary) **mit Texturen**. FBX verliert die Texturen oft.
4. Optional in Blender: alles unterhalb des Halses wegschneiden. Der Kopf sollte aufrecht stehen.
5. Leg die Dateien z. B. unter `Scans/<name>.glb` ab. Die `.gitignore` hält `*.glb` aus Git heraus.

---

## 3. Den ersten Kopf einbauen

> **Prompt:**
> ```
> Ich habe einen Kopf-Scan unter Scans/<name>.glb. Wandle ihn mit Tools/prepare_head.py
> (Blender headless) in Assets/head.bin und die Texturen um, bau den Mod neu und sag mir,
> wie groß das Modell ist und in welche Richtung das Gesicht schaut. Wenn du dir bei der
> Richtung nicht sicher bist, rendere mit Blender ein paar Bilder von vorne/seitlich und
> schau sie dir an. Passe dann die Standardwerte HeadYaw/HeadPitch in GlassesHost.cs an.
> ```

**Test im Spiel:** Kopf und Brille mit den Tasten einpassen (siehe README: **Strg** + Tasten für den
Kopf, ohne Strg für die Brille). Danach **Strg+P** und **P** drücken. Dann:

> **Prompt:**
> ```
> Ich habe den Kopf im Spiel eingepasst. Lies die letzten [MeinModName]-Zeilen aus Player.log
> und übernimm die Werte als neue Standardwerte im Code.
> ```

---

## 4. Ein Kopf pro Freund (der Hauptteil)

Im Moment hat jeder Pilot denselben Kopf. Jetzt soll jeder Spieler seinen eigenen bekommen.

**Idee:**
- Die Köpfe werden **nicht** mehr in die DLL eingebaut, sondern zur Laufzeit aus einem Ordner neben
  der DLL geladen: `heads/<name>/head.bin`, `head_color.jpg` usw. So musst du für einen neuen Freund
  nicht neu bauen, und die DLL enthält keine Gesichter.
- Eine Datei `heads.cfg` ordnet **SteamIDs** den Köpfen zu, z. B. `76561198000000000 = max`.
- Jeder Kopf hat seine eigenen Einpasswerte (Höhe, Größe, Drehung), weil jeder Scan anders ist.

> **Prompt:**
> ```
> Lies CLAUDE.md. Ich will pro Spieler einen eigenen Kopf. Plan zuerst, dann bau:
>
> 1. Köpfe zur Laufzeit laden statt eingebettet: Ordner "heads" neben der DLL, pro Kopf ein
>    Unterordner mit head.bin, head_color.jpg, head_normal.jpg, head_ao.jpg und einer head.cfg
>    mit den Einpasswerten (forward, up, right, scale, yaw, pitch). Jeder Kopf wird nur einmal
>    geladen (Mesh + Material cachen).
> 2. heads.cfg im Mod-Ordner: Zeilen "SteamID = ordnername" und eine Zeile "default = ordnername"
>    (oder "default = none" für die normale Brille ohne Kopf), auch für KI-Piloten.
> 3. Pro Helm herausfinden, wem er gehört: der eigene Helm (HelmetController) gehört
>    BDSteamClient.mySteamID; bei anderen Spielern über PlayerModelSync.netEntity.ownerID
>    (in HeadGlasses.FindVisor wird der passende PlayerModelSync schon gefunden).
>    KI-Piloten bekommen den default-Kopf.
> 4. Die Strg-Tasten verstellen den Kopf, den ich gerade am nächsten zur Kamera habe (oder
>    meinen eigenen, wenn ich in der Außenansicht bin), und speichern in dessen head.cfg.
>    Strg+P loggt auch, welcher Kopf das ist und welche SteamID dazugehört.
> 5. Logge beim Spawnen jedes Spielers "SteamID <id> (<Pilotname>) -> Kopf <name>", damit ich
>    die SteamIDs meiner Freunde aus dem Log ablesen kann.
> 6. Erweitere Tools/prepare_head.py oder schreib ein kleines Skript, das einen Scan direkt nach
>    heads/<name>/ umwandelt, und erklär mir in der README, wie man einen Freund hinzufügt.
>
> Prüf vorher mit ilspycmd, ob die genannten Felder wirklich so heißen.
> Bau und deploye am Ende. Die heads-Ordner sollen in .gitignore stehen.
> ```

**Test:** Mit einem Freund in eine Multiplayer-Lobby gehen (er braucht den Mod nicht, nur du siehst
die Köpfe). Im Log stehen die SteamIDs, die trägst du in `heads.cfg` ein. Wenn dein Freund auch die
Köpfe sehen soll, braucht er den Mod **und** die `heads`-Ordner.

---

## 5. Ideen für danach (optional)

- **Eigene Brille oder Hut:** ein anderes Modell statt `glasses.bin`, z. B. eine Kappe. Prompt:
  *„Ich habe ein Modell Scans/hut.glb. Bau es statt der Brille ein, mit einem eigenen Blender-Skript
  wie prepare_head.py.“*
- **Pro Freund ein eigenes Accessoire** in derselben `head.cfg`.
- **F-45-Helm** auch ersetzen (heute nur der Standardhelm).

---

## Wenn etwas nicht klappt

- Gib Claude den Fehler oder lass es selbst nachsehen: *„Lies die letzten 200 Zeilen aus Player.log
  und sag mir, was schiefgeht.“*
- Der Kopf schaut in die falsche Richtung oder steckt im Sitz: im Spiel mit **Strg** + Tasten
  einpassen. **Strg+B** dreht um 90°.
- Der Kopf ist unsichtbar: Prüfe, ob die Dateien im richtigen Ordner liegen und was das Log sagt.
- In deiner eigenen VR-Sicht siehst du deinen Kopf absichtlich **nicht**. Teste mit der externen
  Kamera oder der Zuschauerkamera.

## Regeln

- **Keine Scans auf GitHub oder in den Steam Workshop hochladen.** Die `.gitignore` schützt davor,
  aber prüf vor jedem `git push` mit `git status`, was mitgeht.
- Teile die `heads`-Ordner nur privat mit Freunden, die einverstanden sind.
