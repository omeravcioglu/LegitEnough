# LEGIT ENOUGH

Single-player underground document-shop simulator (Unity 6000.0.32f1). The full core loop and
business simulation, presented as lit pixel art following the visual-direction canvas: a dim,
grimy shop on a 480x270 grid, pixel customers, a late-90s shop computer and a stamp logo.

## Run it

1. Open the project in Unity 6000.0.32f1.
2. Open any scene (the default `SampleScene` is fine) and press **Play**.
   `GameBootstrap` creates everything at runtime, so nothing needs to be placed in the scene.
3. The title menu is the empty shop at night. **PLAY DEMO** starts Day 1 (there are no saves yet).
   To play the full game in the Editor, tick **Legit Enough > Play Full Game in Editor** (see below).

Set the Game view to 16:9 (1920x1080 or *Full HD*). The UI scales to fit any size.

## Demo and full game

The same code is both games. `GameRoot.DemoBuild` (a static, `true` by default) decides which
session `GameRoot.NewGame` makes: `new GameSession(seed, GameRules.Demo())` or the plain
`new GameSession(seed)`, which is the full game exactly as before. Everything demo-only checks
`GameSession.IsDemo` (`Sim/Core/GameRules.cs`), so the full game's rules, random draws and
messages don't change. The trailer rig and the store-shot harness set `DemoBuild = false`
before their session is made; the smoke test and the player build use the demo.

**Running the full game.** Never edit `DemoBuild` in source. In the Editor, tick
**Legit Enough > Play Full Game in Editor** (`Assets/LegitEnough/Editor/FullGameToggle.cs`): it sets
the EditorPrefs switch `LegitEnough.PlayFullGameInEditor`, which `GameRoot.Awake` reads in the Editor
only (not in batch mode, never in a player), so the next Play starts the full game. Untick it to get
the demo back. The batch harnesses (trailer, store shots, smoke test) set `DemoBuild` themselves.

**Building the full game.** There is no full-game player build yet, on purpose: `build-demo.ps1`
and `BuildDemo.cs` refuse to build unless `public static bool DemoBuild = true;` is in
`Game/Boot/GameRoot.cs`, so a forgotten edit can never ship the full game as the demo. When the
full game ships, give it its own build entry (same scene list, its own output folder) rather than
flipping the demo's.

The demo (about 15 minutes, McHunter Studio ident, then the title with a DEMO stamp):
- **Day 1, coached.** R.'s sticky notes and a pointer (`Game/Tutorial/TutorialCoach.cs`) walk
  through Samir's France passport: read the mail, open the sign, haggle once, the photo (handbook
  photo page, backdrop, glasses, zoom, shutter, crop, keep), the editor, print, the scripted
  jam (hit the printer), bind, check, hand over, take the cash, close early. SKIP TUTORIAL or
  hints off (F1, Settings) hands over to the hint line.
- **Day 2, scripted** (`Sim/Content/DemoCast.cs`, `Sim/GameSession.Demo.cs`): Turkey is a gift,
  R.'s note, fixed news (France changes its photo rules, so the handbook is OUTDATED; an
  equipment sale), Samir's outcome, then four visitors, timed from when the sign flips (these
  times assume 09:00): Elif (Turkey, 09:05), Kenji (Japan, 09:45: the FULL GAME teaser), Viktor
  (an urgent France job, 10:20, due four hours later) and Anja (Germany, 11:30: only if you own
  the Germany section; buying it while she waits gets you the job). R. says close up when they're done.
- After the Day-2 report, **FINISH DEMO** opens the thank-you screen (stats, what the full
  game adds, WISHLIST ON STEAM, PLAY AGAIN, QUIT). There is no Day 3.
- Five countries play (France, Turkey, Germany, Italy, United States). United Kingdom, Japan,
  South Korea, UAE, Iran, visas and tier-3 equipment are shown locked with a FULL GAME stamp;
  clicking one says so.
- Dev keys (F5, F6, F9) only exist in the Editor and development builds.

## Build the demo

```
powershell -ExecutionPolicy Bypass -File Tools/Build/build-demo.ps1
```

Copies the project to a throwaway folder (default `%TEMP%\LegitEnoughBuild`, reused so later
builds skip the full import), runs batch-mode Unity there with
`Assets/LegitEnough/Editor/BuildDemo.cs` (`BuildPipeline.BuildPlayer`, Windows x64, Mono,
scene list given explicitly: `Assets/Scenes/SampleScene.unity`) and writes the player to
`Builds/LegitEnoughDemo/Legit Enough.exe` under the project root (outside Assets). The same
build is on the Editor menu: **Legit Enough > Build Demo (Windows x64)**. Player settings:
company McHunter Studio, product Legit Enough, version 0.9.0, no Unity splash (the game shows
its own studio ident). Options: `-Out <folder>`, `-Work <folder>`, `-TimeoutMinutes <n>`.
The exe's icon is the store icon: BuildDemo pulls every hand-drawn size (16-256 px) out of
`Steam/store/client_icon.ico`, imports them into the build copy as point-filtered, uncompressed
textures and sets them as the Standalone icons (each slot gets the matching or next larger size),
then puts the project's icon settings back. The script exits 4 without building when
`GameRoot.DemoBuild` isn't `true` in source or the icon file is missing.

### Controls

| Action | How |
|---|---|
| Open / close the shop | Click the sign on the door |
| Reply to a customer | Click a reply card (click a speech bubble to skip a line) |
| Photograph | Click the camera / photo corner while the customer sits on the stool |
| Pose the customer | Drag their **head** (9 directions + in-between frames); drag their **shoulders** to slide them on the stool |
| Frame the shot | Drag the background (pan) · mouse wheel (zoom) · buttons |
| Remove glasses / hat | Click them on the face, or use the SAY buttons |
| Shutter | SHUTTER button or **SPACE** (the cheap camera has shutter lag) |
| Make the document | Computer, then **OFFICIAL-ISH 98** |
| Bind | Click the printed page in the printer tray |
| Hand over | Drag the booklet from the desk onto the customer |
| Get paid | Click the cash on the counter |
| Handbook | **H** |
| Hints | **F1** |
| Back / pause | **ESC** |
| Dev (Editor / development builds only) | **F5** spawn a walk-in, **F6** +30 game minutes, **F9** customer lineup |

## Architecture

```
Assets/LegitEnough/
  Scripts/Sim/    LegitEnough.Sim   pure C#, noEngineReferences: ALL the rules
  Scripts/Game/   LegitEnough.Game  Unity UI Toolkit presentation
    Pixel/        software pixel renderer: room, props, lights, fonts, logo, flags, icons
  Resources/LegitEnough/UI/      Main.uss (every UI style) + theme; DemoBoot/DemoLocks/DemoTutorial.uss (demo screens)
  Resources/LegitEnough/Fonts/   LegitPixel.ttf (body) + LegitStamp.ttf (headings)
  Editor/         LegitEnough.Editor  the player build script (editor only)
Tools/SimTests/   offline test runner + compile checks + batch-mode Unity smoke test
Tools/Build/      build-demo.ps1: the Windows player build on a throwaway copy
Tools/ArtGen/     Node: the pixel fonts (fonts.js) and the visual-direction concept art (build.js)
```

**The simulation never touches Unity.** `GameSession` is the whole game state. The
presentation calls `Tick(dt)` each frame, reads state to draw it, issues player commands
(`Choose`, `BeginPhotoSession`, `PrintDraft`, `BindPage`, `GiveDocument`, ...) and drains
`SimEvent`s for sounds and toasts. A redesign or a full art pass can replace `Scripts/Game`
without changing any rule.

**The art is drawn in code, as pixel art.** `PixelBuffer` is a software canvas and
`PixelPainter` a hard-edged stand-in for Painter2D; results are shown through point-filtered
textures (`PixelImage`) at 4x on the 1920x1080 stage.
- `RoomArt` draws the shop at 480x270, laid out on the same spots as `RoomLayout`. Equipment
  tiers swap the physical objects (CRT → monitors, taped inkjet → laser, bedsheet → studio) and
  the overall kit level dresses the room (stains and plastic chairs on Day 1, a plant and a
  leather bench later).
- `ShopRenderer` bakes the room behind the customers and the counter/desk in front of them, and
  animates small layers on top (rain in the window, the door and bell, the clock, the screen,
  the printer, the cash LCD). One banded `LightMap` lights everything, people included; it
  follows the clock (cool morning, warm evening, dark night), the day's weather (`ShopMood`,
  presentation only) and power cuts.
- `FigurePainter` keeps the old vector geometry (so photo scoring still matches what you see)
  but rasterizes it as pixels; heads under 24 art pixels use hand-placed pixel features.
  `PortraitView`'s resolution follows the camera: the cheap camera gives chunkier pixels.
- UI is UI Toolkit styled by `Main.uss` with the two pixel fonts. Font sizes in multiples of 12
  keep the pixels even. `Tools/ArtGen/fonts.js` regenerates the TTFs and `PixelGlyphs.cs`.

## What's in (systems)

| Doc § | System | Where |
|---|---|---|
| 3, 4 | Customer visit loop: door, counter, stool, bench, door, with patience and queueing | `GameSession.Visits.cs`, `Visit.cs` |
| 7, 9 | Modular, seeded, persistent customers; 14 traits; 6 hidden stats | `CustomerGenerator.cs`, `Personality.cs` |
| 8 | Recurring story: Samir (Day 1, France), and if it works, his brother ~20 days later | `StoryCast.cs` |
| 10 | 12 motivations, personality-driven dialogue | `Lines.cs` |
| 11-14 | 10 real countries with fictional rules, tier unlocks, handbook purchases | `Countries.cs`, `Handbook.cs` |
| 15-23 | Photography: 9 poses + transition frames, blinking, expressions + relapse, personality drift, accessories (incl. the backup-glasses joke), backdrops, zoom/pan, shutter lag, crop, review | `Photo/*` |
| 24-27 | Document editor: template, category, cover, pages, fields, dates, number, photo; print (jams, slow printer), bind, inspect, shred | `Documents/*`, `ComputerView.cs` |
| 28 | Hidden quality: photo / information / formatting with soft penalties and hard multipliers | `PhotoEvaluator.cs`, `DocumentEvaluator.cs`, `Quality.cs` |
| 29 | Negotiation with hidden reservation price and "final offer" | `Negotiation.cs`, `Pricing` |
| 30-32 | Orders, deadlines, urgent jobs, pickups / come-back-tomorrow, multiple open orders | `Order.cs`, `CustomerDirector.cs` |
| 33-37 | Money ledger, rent, supplies; reputation tiers; heat levels; per-country risk; "stop taking X jobs" toggle | `Ledger.cs`, `Standing.cs` |
| 38-40 | Inbox, delayed outcomes (days later, sometimes never), client database with statuses | `Inbox.cs`, `Outcomes.cs`, `ClientDatabase.cs` |
| 41, 43 | News: risk spikes, demand surges, traffic, rule changes that leave your handbook out of date, power cuts, internet outages, broken printer, sales | `News.cs`, `GameSession.World.cs` |
| 42 | Equipment tiers (camera, printer, computer, photo station) with real mechanical effects | `Equipment.cs` |
| 44 | Day structure: morning card, open/close sign, closing time, end-of-day ledger | `GameSession.cs`, `DayViews.cs` |

Not yet: visas, campaign milestones / retirement ending / endless mode, save/load,
more story arcs, music.

## Tuning

Every economy, timing and scoring number is in `Sim/Core/Balance.cs`. Country rules live in
`Sim/Content/Countries.cs`, equipment in `Equipment.cs`, dialogue in `Lines.cs` and
`MessageTemplates.cs`.

## Tests

```
powershell -ExecutionPolicy Bypass -File Tools/SimTests/run.ps1            # sim tests: the full game (incl. a 20-day bot) and the demo (Demo_*)
powershell -ExecutionPolicy Bypass -File Tools/SimTests/compile-game.ps1   # compile-check Sim+Game offline, then SmokeTest, StoreShots and the Editor scripts
powershell -ExecutionPolicy Bypass -File Tools/SimTests/unity-smoke.ps1 -Out <folder>   # batch Unity on a copy: plays the whole demo, saves screenshots
```

All three use Unity's bundled compiler and runtime. Nothing else needs installing.

The smoke test (`Tools/SimTests/UnitySmoke/SmokeTest.cs`, about 10 minutes) plays the DEMO
through public APIs and the named UI elements (buttons get a submit event, shop hit areas a
click): title, credits, settings, the studio splash (opened by hand; batch mode skips it), the
coached Day 1 with Samir (the sign refuses to close before he comes, haggle, photo, editor with
the locked templates and VISA, print, the scripted jam, bind, hand over, cash, close early), the
Day-2 card (Samir's verdict follows his passport's quality) and mail, Border Watch (FULL GAME
flags; Turkey switched off) and SHOP (50% sale, tier 3, locked handbooks, visa license), the
France update, the handbook's FULL GAME pages, VISAS tab and unowned sections, pause and
settings, then the Day-2 visitors with their times checked against the opening: Elif turned
away until Turkey is taken again, Kenji's Japan teaser, Viktor's rush job (due four hours
after he walks in; shot on the old light-gray background first, so FIX IT sends him back to the
stool for a white one), the focus-loss pause, Anja turned away until Germany is bought while
she waits, then the Day-2 report, FINISH DEMO and the demo-end epilogue. While one job runs,
the next visitor due is moved back a little so the script can do one job at a time (logged as
`hold:`). Screenshots are numbered in
script order in `<folder>/shots`, with `log.txt` and `errors.txt`. Any error-level log, failed
check or timeout fails the run (exit 1, or 3 for a Unity timeout).

## Fonts and concept art

```
node Tools/ArtGen/fonts.js   # rebuilds LegitPixel.ttf, LegitStamp.ttf and Scripts/Game/Pixel/PixelGlyphs.cs
node Tools/ArtGen/build.js   # renders the visual-direction concept images into Tools/ArtGen/out
```

Plain Node, no packages. `fonts.js` holds the glyph tables for both pixel fonts (5x7 body with
lowercase, descenders and Latin-1 accents; bold 8px capitals) and writes them as TrueType
outlines, one square per pixel, plus the same tables as C# for text painted into the room art.

`build.js` draws the concept art from code at native size (shop 400x225, customers 36x72, photo
portraits 80x80) and saves it pre-scaled with nearest-neighbour; the scale is in the file name
(`shop-night-x4.png`). `scene.js` draws the shop (tier 1 = Day 1, tier 3 = Day 100) and its
lights, `people.js` the customers and portraits, `cast.js` names the cast, `assets.js` draws
product art, icons, flags and the logo, and `lib.js` is the pixel buffer, 3x5 font, dithered
lighting and PNG writer. `out/` sits outside `Assets/`, so Unity imports nothing from it.
