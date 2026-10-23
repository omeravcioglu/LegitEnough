# LEGIT ENOUGH

Single-player underground document-shop simulator (Unity 6000.0.85f1). The full core loop and
business simulation, presented as lit pixel art following the visual-direction canvas: a dim,
grimy shop on a 480x270 grid, pixel customers, a late-90s shop computer and a stamp logo.

## Run it

1. Open the project in Unity 6000.0.85f1.
2. Open any scene (the default `SampleScene` is fine) and press **Play**.
   `GameBootstrap` creates everything at runtime, so nothing needs to be placed in the scene.
3. The title menu is the empty shop at night. **PLAY DEMO** starts Day 1 (there are no saves yet).
   To play the full game in the Editor, tick **Legit Enough > Play Full Game in Editor** (see below).

Set the Game view to 16:9 (1920x1080 or *Full HD*) to see it as most players will.

**Screen sizes.** The game is one 1920x1080 stage, always shown whole: the panel scales it by the
smaller of width/1920 and height/1080 (`PanelScreenMatchMode.Expand`, set in
`GameRoot.CreateDocument`) and `GameRoot.LayoutStage` centres it on a whole screen pixel. Any
other aspect ratio gets plain near-black bars, rgb(6, 5, 4) like the day card and the splash
(the `.le-root` background and the panel's clear colour); the stage clips its own content, so
nothing draws into them and clicks there hit nothing. 21:9 (3440x1440: the stage at 2560x1440,
1.333x) has bars at the sides; 16:10 (1920x1200, 2560x1600), 4:3 and the Steam Deck (1280x800:
the stage at 1280x720, 0.667x, 40 px bars) have them above and below. 1440p and 21:9 run at a
non-whole scale (art pixels 1 or 2 screen pixels wide); 1080p and 4K are exact. Settings'
FULLSCREEN fills the monitor the window is on (FullScreenWindow, bars as needed); windowed
picks a 16:9 window (a whole multiple of 1920x1080 when it fits), and the window can't be
resized by dragging.

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
  jam (hit the printer), bind, check, hand over, take the cash, close early. R.'s closing note
  says tomorrow's tools are in the computer's SHOP. SKIP TUTORIAL or hints off (F1, Settings)
  hands over to the hint line (skipping on Day 1 skips Day 2's notes too).
- **Day 2, scripted** (`Sim/Content/DemoCast.cs`, `Sim/GameSession.Demo.cs`): Turkey is a gift,
  R.'s note, fixed news (France changes its photo rules, so the handbook is OUTDATED; an
  equipment sale), Samir's outcome, then four visitors, timed from when the sign flips (these
  times assume 09:00): Elif (Turkey, 09:05), Kenji (Japan, 09:45: the FULL GAME teaser), Viktor
  (an urgent France job, 10:20, due four hours later) and Anja (Germany, 11:30: only if you own
  the Germany section; buying it while she waits gets you the job). R. says close up when they're done.
- **Day 2, guided to the SHOP.** A short, skippable tour on the same sticky note (THE MAIL / THE
  SHOP / OPEN UP): computer, MESSAGES, R.'s "day two" note, SHOP, the France UPDATE ($80), then a
  quiet ring on Germany's BUY ($250) and the half-off gear ("Your call"), then flip the sign. It
  never blocks play and ends once the shop is open and up to date (or a visitor walks in). A
  visitor you can't serve yet (no section, or an outdated one) gets a one-off note: "Click the
  COMPUTER, then SHOP", following through to the right button. SHOP buttons are named
  `shop-update-<ID>`, `shop-buy-<ID>`, `shop-equip-<slot>`.
- **Another photo (demo and full game).** Not happy with a photo you kept? While the client is
  on the bench (or at the counter waiting for the booklet) and nothing's handed over, click the
  photo corner: "Another photo?" calls them back to the stool (`GameSession.CallBackForPhoto`,
  costs 10% patience; below 25% they refuse, except the tutorial's Samir; draws no game dice).
  Kept photos stay; the editor lists them newest first (NEWEST / IN USE) and you pick one. A
  booklet already bound stays on the desk: print again, hand over the new one (desk names
  `booklet-<order>` and `booklet-<order>-<doc>` for a reprint), shred the old copy.
- **Music** (`Game/Audio/Music.cs`, files in `Resources/LegitEnough/Music`): four original
  pieces in the trailer's instruments, made in code by `Tools/Music/compose.py` (see
  `Tools/Music/README.md`). Title/splash: `title_night` (80 s loop); the shop: `shop_day` (4 min
  loop); a rush job open (or someone waiting after closing): `shop_busy`, the same performance
  with more drive, picked up at the same point in the song; the demo end: `demo_end` (once).
  1.5 s crossfades, a 4 dB duck under speech, half volume under pause/settings. Settings has a
  MUSIC row (default 60%, PlayerPrefs `le.music`) under VOLUME: up/down picks the row, left/right
  sets it. `Editor/MusicImport.cs` imports the files as streamed Vorbis (q 0.6, not preloaded).
  Batch runs (smoke test, store shots, trailer) track the cue but play nothing.
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
| Another photo | Click the photo corner while they wait on the bench (before handing over), confirm "Another photo?" |
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

## Achievements

36 achievements (15 progress, 10 skill, 4 story, 7 secret; 8 of them hidden), 12 of them in the
demo. One catalog drives everything: `Sim/Achievements/AchievementCatalog.cs` (API names, names,
descriptions, in-game teasers for hidden ones, the 6 progress stats). The 12 demo ones come first.

- **Sim** (`Scripts/Sim/Achievements`): `AchievementTracker` watches a session read-only (three
  hooks in `GameSession.cs`: the `Achievements` property, `Emit`, the end of `Tick`) and queues
  unlock / stat signals. It never draws a random number or adds an event, so a session plays
  exactly the same with or without it (`Ach_FullModeUnchanged`, `Ach_DemoUnchanged`).
  `AchievementBook` turns signals into unlocks on an `IAchievementStore`: stats unlock progress
  achievements, the demo **banks** full-game event achievements it earns early ("earned for the
  full game"), and `Import` carries the demo's unlocked and banked ones into the full game.
- **Game** (`Scripts/Game/Achievements`): `AchievementService` (owned by `GameRoot`) picks the
  store per edition: demo → PlayerPrefs `le.ach.demo.*` (never Steam), full →
  `AchievementPlatform.SteamFactory` when a Steam backend is installed, else PlayerPrefs
  `le.ach.full.*`; batch mode → memory only, so the smoke test, trailer and store shots never
  read or write a player's achievements. The full game imports the demo's achievements once on
  first boot; that needs the demo and full builds to keep the same company and product name.
  `AchievementToasts` is the unlock banner (top centre over the shop, compact at the bottom over
  the computer, held under any other screen, quiet on the coached Day 1, hidden in batch mode
  unless `AchievementToasts.ShowInBatch`). `AchievementsView` is the ACHIEVEMENTS screen (title
  and pause menu); the demo-end screen says how many of the 12 you unlocked.
- **Art**: `Game/Pixel/AchievementArt.cs` draws the 32x32 badges (a perforated stamp per
  category, a 20x20 glyph each) and the 256x256 Steam icons.
- **Dev**: in the Editor and development builds **F8** earns the next achievement this edition
  can still earn and **Shift+F8** resets this edition; **Legit Enough > Achievements > Reset
  Local (Demo + Full)** clears the Editor's PlayerPrefs for both.
- **Steam**: not wired yet. The Steam backend (Steamworks.NET, `Scripts/Platform/Steam`) comes
  later; until then the full game keeps achievements locally. The demo app gets no Steam
  achievements (Valve's recommendation for demos).

## Steam achievements export

```
powershell -ExecutionPolicy Bypass -File Steam/achievements/tool/compile-check.ps1             # seconds: compiles the exporter against Sim+Game
powershell -ExecutionPolicy Bypass -File Steam/achievements/tool/export.ps1 -Out <folder>      # batch Unity on a copy, a few minutes
```

`export.ps1` copies the project to `<folder>/project` (`-Copy <folder>` reuses an imported copy;
`-Unity <Unity.exe>` picks another editor), runs `Steam/achievements/tool/AchievementExport.cs`
in batch mode and checks what it wrote (72 PNGs at 256x256, 36 definition rows, 72 localization
rows, a 36-row paste table, LF and no BOM) before copying it into `Steam/achievements/`:

- `icons/<API>_ACHIEVED.png` and `_UNACHIEVED.png`: the badge at 8x on the panel colour,
  colour and grayscale, named the way Steam's bulk upload assigns them.
- `5394180-achievements-definitions.csv` and `5394180-achievements-localization.csv`: the Bulk
  Import/Export columns (spoiler flags, progress stats and ranges; English names and
  descriptions, real text for spoilers).
- `contact_sheet.png`: every badge achieved / locked / full game / in-game secret at 4x.
- `STEAM_ACHIEVEMENTS.md`: the paste table, the 6 stats to create by hand, the upload steps,
  what was checked in the Steamworks docs and what is still uncertain, and the long-run
  achievements to confirm before publishing.

Never edit the generated files: change the catalog or the art and export again.

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
Tools/Music/      Python: composes, renders and checks the background music (.ogg into Resources)
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
more story arcs.

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
Day-2 card (Samir's verdict follows his passport's quality), R.'s Day-2 tour (the note's ring
and arrow checked on the computer, MESSAGES, his note, SHOP, the France UPDATE, the arrow-less
ring on Germany's BUY, the sign) and mail, Border Watch (FULL GAME
flags; Turkey switched off) and SHOP (50% sale, tier 3, locked handbooks, visa license), the
France update, the handbook's FULL GAME pages, VISAS tab and unowned sections, pause and
settings, then the Day-2 visitors with their times checked against the opening: Elif turned
away until Turkey is taken again, then her job with a voluntary retake (booklet bound, photo
corner, "Another photo?", the new photo picked in the editor, reprint, the new booklet handed
over, the old one shredded; pictures of that pass end in `_again`), Kenji's Japan teaser, Viktor's rush job (due four hours
after he walks in; shot on the old light-gray background first, so FIX IT sends him back to the
stool for a white one), the focus-loss pause, Anja turned away until Germany is bought while
she waits (R.'s note points at the computer, SHOP, then Germany's BUY), then the Day-2 report,
FINISH DEMO and the demo-end epilogue. Music: the four files are checked to import as streamed
Vorbis and load at the right length, the settings MUSIC row is pressed, and the cue MusicPlayer
picks is logged (`music:`) and checked: title, shop, busy during Viktor's job, shop again, the
demo end, title after PLAY AGAIN. While one job runs,
the next visitor due is moved back a little so the script can do one job at a time (logged as
`hold:`). At six points (the title, the Day-1 shop with R.'s note, a toast over Border Watch,
the computer SHOP with R.'s note, the open Day-2 shop with its hint line and a toast, the demo end) the game is held still and the same moment is
rendered again at 3440x1440 and 1280x800 (the panel takes the render texture's size as the
screen size), then 1920x1080 comes back: the whole stage must sit inside the panel, centred,
with bars of rgb(6, 5, 4) on one pair of sides only; R.'s note, the toasts and the hint line
must be inside the stage; a known button is clicked at its on-screen spot and a bar must hit
nothing (`aspect` lines in the log, pictures `NNN_name_3440x1440.png` / `_1280x800.png`). On the
Day-2 SHOP screens R.'s note must cover no visible, enabled button (`coach vs buttons` lines).
Achievements (memory store, banners switched on): ACHIEVEMENTS from the title ("DEMO 0 / 12",
12 demo cells and 24 FULL GAME ones, the secrets "???" with their teaser, WISHLIST on a FULL GAME
cell, BACK), then every banner is logged when it first shows (`achievement banner:`) and must
never show over any screen but the shop and the computer. FIRST_PHOTO waits for the camera to
close, then shows quiet and clear of R.'s note (`ach_toast_tutorial`); PERCUSSIVE_MAINTENANCE
after the scripted jam; FIRST_JOB (and QUALITY_90 when Samir's passport scores 90+) after his
cash; SAMIR_MADE_IT waits under the Day-2 card (`ach_toast_samir`); READ_THE_PAPER and RUSH_JOB
from Viktor's redo; then the photo station upgrade is bought on sale ($350) and FIRST_UPGRADE
shows compact over the computer, clear of the bezel (`ach_toast_computer`); JUST_IN_TIME from
Anja; the pause menu's ACHIEVEMENTS count matches the book (`achievements_pause`); and the demo
end says "You unlocked N of 12 demo achievements." with the same N.
Screenshots are numbered in
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
