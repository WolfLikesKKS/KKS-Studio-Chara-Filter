# Studio Chara Filter

by **WolfLikesKKS**

| Game | Repository | Download |
|---|---|---|
| Koikatsu Sunshine | [KKS-Studio-Chara-Filter](https://github.com/WolfLikesKKS/KKS-Studio-Chara-Filter) | [Releases](https://github.com/WolfLikesKKS/KKS-Studio-Chara-Filter/releases) |
| Koikatsu / Koikatsu Party | [KK-Studio-Chara-Filter](https://github.com/WolfLikesKKS/KK-Studio-Chara-Filter) | [Releases](https://github.com/WolfLikesKKS/KK-Studio-Chara-Filter/releases) |

Both are built from the same source code; install the one that matches your game.

I've been using CharaStudio for a few years now and somehow it never occurred to me that a character filtering mod might be useful.

Studio Chara Filter turns CharaStudio's character list from a long list of names into something you can actually search.
Filter thousands of cards by body sliders, outfits (towel, swimwear, nothing on…), zipmod content and missing mods,
author, folder and name. It works from a background index, so no cards need to be loaded. It also makes CharaStudio
itself faster: a 933-card folder that took 3.8 seconds to open now opens in 13 ms, and hovering over names no longer
stutters. For collections with missing mods, it lists exactly which zipmods are missing, how many cards need each one,
and where to get them.

I hope this is useful to the community - it's my thanks for all the character cards and scenes over the years.

## What it's like to use

1. **Install** (see [Installation](#installation)) and open CharaStudio, then **Add > Female** (or Male).
   The filter window opens next to the character list.
2. **The first time**, the plugin reads your card folders in the background and shows its progress in the window.
   You can keep working meanwhile. A large collection takes a few minutes once; after that, the index is loaded from
   cache in under a second and only new or changed cards are read.
3. **Pick where to look** at the top: the folder the list is showing, a few folders you tick, or all folders.
4. **Open the sections you need and narrow down.** Every filter you set applies together with the others, and the
   result count updates right away. The character list shows only the matching cards; click one to add it as usual.
5. **Clear** resets everything; each section also has its own Clear.

Some things people use it for:

- *"Tall characters with a bath towel outfit"*: Body > Height range, Clothing > Bath towel: has one.
- *"Which of my cards will load with missing hair?"*: Content mods > Hair: Missing.
- *"What do I need to download?"*: Missing mods report - every missing zipmod, how many cards need it, and links.
- *"Cards by one author"* or *"cards I added this week"*: Card authors, Card file > Added.
- *"Find my duplicate saves"*: Card file > Find duplicates.

Tips:

- **Ranges**: type several in one box, e.g. `1-10, 35-40, 90`, to match any of them.
- The little **graph above each slider** shows where your cards are - handy for finding outliers.
- Filters are additive, so a result of 0 usually means one section is too strict; the counts per section help find it.
- **Arrange** lets you reorder the sections; F1 decides which body/face sliders appear (with three saveable presets).
- Nothing on your cards is changed. Card removal is off unless you enable it, and even then cards are moved, not deleted.

There are two builds from the same source: `KKS_StudioCharaFilter.dll` for Koikatsu Sunshine and
`KK_StudioCharaFilter.dll` for Koikatsu / Koikatsu Party.

## Features

### Where to look
- **Current folder**: filters whatever the list is showing (works with Browser Folders).
- **Choose folders**: tick one or more folders in a folder tree (with card counts, optional subfolders, search).
- **All folders**: every card in your collection, regardless of folder.
- **Name search**: character name or file name; several words must all match. Works in every scope.
- Large results are split into pages, with a **Show all** option.
- Counts and distribution graphs in the window follow the current scope (the missing mods report always covers all folders).

### Random filters

- **Random filters** (next to *Reset all*) clears the filters and sets a random few: ranges on 2-5 random sliders and
  sometimes a hair or eye color, picked from your own cards. Press again for another mix.
- **Random** on a slider gives it a random range; **+ random range** adds another one.

Ranges always come from the cards (a random card's value, widened to hold a good share of what still matches), so a random
filter never lands on an empty stretch. How narrow it gets follows the size of the collection: about 3 x the square root of
the cards in scope remain (100 cards -> ~30, 1,000 -> ~95, 5,000 -> ~210, at most 300).

### Random characters (Studio)

A **Random** row under the match count picks characters at random from the cards that match your filter in the current
scope (all pages, for the open female or male list):

- **Add** - adds that many random characters to the scene.
- **Replace selected** - replaces the characters selected in the Workspace with random ones.
- **Replace all** - replaces every character of that sex in the scene (click twice).
- **No repeats** - each card is used once until all matching cards have been used.

Replacing uses Studio's own "Change character", so each character keeps its place, pose and animation. Example: select five
copies of a default character, filter (or pick a folder) down to your own 26 cards, press *Replace selected*. Turn the row
off in F1 > Display > Show random character buttons.

**Undo, one character at a time:** every replacement made in the session - by these buttons or by Studio's own *Change
character* - is listed under the row (*Replaced characters*), newest first, each with an **Undo** button that changes that
character back to exactly what it was (including plugin settings stored on it). **Undo selected** undoes the latest
replacement of each character selected in the Workspace. Undoing an older replacement of a character that was replaced
again since undoes the later ones too. The backups are kept in `UserData\StudioCharaFilter\undo\` and deleted the next time
Studio starts.

### Body
- **Anus size (ABMX)**: the scale of the anus bone from ABMX bone edits (100 = not edited; most cards aren't).
- Range sliders for any of the **47 body values** from the Maker body tab (all 44 shape sliders, plus areola size,
  bust softness and bust weight), plus anus size from ABMX. Choose which ones appear in F1. 11 common ones are shown by default.
- Two-handle sliders with number boxes for exact values. Add several ranges per value (e.g. `1-10, 35-40, 90`).
- A small distribution graph above each slider shows where your cards fall.
- The scale widens automatically for Slider Unlocker values. Handles at the ends include outliers.

### Face
- Range sliders for any of the **52 face sliders** from the Maker face tab (face shape, jaw, chin, cheeks, eyebrows, eyelids,
  eye position/size/angle, nose, mouth, ears), plus **iris width and height**. Choose which appear in F1 > Face sliders.

### Colors
- **Hair**, **eye** and **skin** color, sorted into simple buckets (black, brown, blonde, red, pink, blue, …; pale, fair, tan, dark).
  Tick colors to show only those cards.

### Body details
- Pubic hair, tan lines, body paint / tattoos, fangs (yaeba), futa (penis shown via Uncensor Selector), any ABMX bone edits:
  each *has* / *doesn't*. Plus a folding **nipple type** list (closed by default, with a selected count and Clear).
- **Hand size** and **foot size** sliders (ABMX bone scale) in the Body section, next to anus size.

### Profile (off by default, enable in F1 > Display)
- Personality, blood type, club, voice pitch, aggressive / diligence / kindness, the 23 **traits**, 9 **likes** and 5 **H refusals**.

### Card file
- **Added / modified**: last day, 7 / 30 / 90 days, last year, or older than a year.
- **Find duplicates**: the same character saved more than once (same name and identical face/body sliders), with **Show** to list every copy.
- **Unreadable files**: .png files in the card folders that aren't readable cards, with **Save list**.

### Clothing (outfits)
- Find characters with at least one outfit (in any slot) that is a **bath towel**, **nothing on**,
  **underwear / swimwear**, or **dressed**. Each can be set to *has one* or *none*.
- Filter by extra outfits (More Outfits) and by outfit name (e.g. "maid", "yukata").
- An optional per-slot table (Sunshine: Casual, Swimsuit, Pajamas, Bathing; Koikatsu: its 7 outfits; then More Outfits slots).

### Content mods (zipmods)
- Per body region (Hair, Head, Face, Neck & chest, Body & waist, Arms & hands, Legs, Clothing, Other):
  - **Uses mods**: does the card use zipmod items there?
  - **Missing**: are any of them not installed? For example, hide cards missing hair mods but keep
    cards that are only missing a strap or a beret.
- **Missing mods report**: every zipmod your cards use that isn't installed, sorted by how many cards need it,
  with name, author and website (from the cards themselves). Copy or open links one at a time,
  **open the next N sites** in batches (each site once), or save the full list to a text file.

### Installed zipmods not used by any card
- Information only: lists installed zipmods that none of your character cards (female or male) use, grouped by folder with sizes,
  so you can consider trimming a large mods folder (fewer zipmods = faster game startup).
- Only character cards are checked; Studio items, maps, scenes and Maker-only content can still need those mods.
  Studio / map packs are hidden by default. **Save list** writes everything to `UserData\StudioCharaFilter_UnusedZipmods.txt`.

### Genital type (Uncensor Selector)
- Lists which uncensor body each card asks Uncensor Selector for (with readable names where available), plus "Default" for cards without a choice.
  Tick one or more to show only those cards. Can be hidden in F1 > Display.

### Birthday (off by default, enable in F1 > Display > Show birthday filter)
- Cards store a birthday (month and day), not an age. Filter by birth month (tick any months), or by whether a birthday was set at all.
- May 17 is the Maker's default birthday, so it counts as "not set" by default (changeable in F1).

### Card authors
- Searchable list of card authors (from the Maker's author info) with counts. Tick authors to show their cards.

### Safe card removal (off by default, enable in F1 > Removed cards)
- When enabled, a collapsed **Card removal** section appears at the bottom of the filter window.
  **Remove highlighted card...** only works while a card is highlighted in the list, and asks once to confirm.
- The card is moved to `-UserRequest2Delete-` next to CharaStudio.exe, keeping its folder structure. Nothing is deleted.
- The same section lists removed cards with **Restore** to put a card back where it was.
- F1 > Removed cards: **Open folder**, or **Empty to Recycle Bin** (asks once). Even then, the cards can be recovered
  from the Windows Recycle Bin until it's emptied.
- Saved scenes aren't affected, because scenes store their own copy of each character.

### Speedups for CharaStudio itself (on by default, can be turned off in F1 > Performance)
- **Fast folder loading**: the character list reads names from the index instead of fully loading every card.
  Example: a 933-card folder went from ~3.8 s to ~13 ms. New or changed cards are still read normally.
- **Reuse list rows**: rows in the character list are kept and reused instead of destroyed and re-created every time the list changes,
  which speeds up switching folders and pages.
- **Fast thumbnails**: removes the stutter when moving the mouse over names, by caching thumbnails instead of
  running a full memory cleanup on every hover.

### Window and settings (F1)
- Draggable, resizable window with a scale setting for 1440p / 4K, an optional toggle hotkey,
  and a saved startup position (**Save current position** in F1).
- **Arrange** button: reorder the sections with ▲ ▼ (also in F1 > Window > Section order). Open/closed sections are remembered.
- F1 slider lists stay compact: Show all / Default set / Hide all, three saveable custom presets, and a fold-out checkbox list.
- Hover text on the main buttons.
- Optional legacy filter for which plugins saved data on a card (any / HAS / NOT).

## Requirements
- Koikatsu Sunshine with **BepInEx 5** and **KKSAPI**, or Koikatsu / Koikatsu Party with **BepInEx 5** and **KKAPI**
  (both included in HF Patch / BetterRepack).
- Optional: **Sideloader** (for missing-mod checks; included in HF Patch), **Browser Folders** (subfolder browsing).

## Installation
Download the zip for your game from its Releases page (see the table at the top) and extract it
into your game folder (the one with `CharaStudio.exe`), so that the DLL ends up in `BepInEx\plugins`
(`KKS_StudioCharaFilter.dll` for Sunshine, `KK_StudioCharaFilter.dll` for Koikatsu). Open CharaStudio, then Add > Female.
The first time, the plugin indexes your cards in the background (progress is shown in the window).
After that, startup reads the cached index in well under a second and only re-reads new or changed cards.
Cards are read on several threads at once (F1 > Performance > Indexing threads).

The index cache is stored in `BepInEx\cache\StudioCharaFilter_*.idx`, and can be deleted at any time to rebuild it.

## Building from source
The build uses the C# compiler that ships with Windows and references the game's own DLLs.
No Visual Studio is needed.

1. Put this repository in a folder inside the game folder (e.g. `<game>\_ModDev`).
2. Run `build.bat`. It compiles `src\*.cs` and copies `KKS_StudioCharaFilter.dll` to `BepInEx\plugins`.
   Close CharaStudio first, because Windows locks the DLL while the game is running.
3. For Koikatsu, run `build_kk.bat`. It builds `KK_StudioCharaFilter.dll` from the same source with `/define:KK`
   against Koikatsu's DLLs and copies it into Koikatsu's `BepInEx\plugins`. By default it looks for Koikatsu next to this
   game's folder; set the `KKGAME` environment variable to point it elsewhere.

Everything that differs between the games is in `src\GameDefs.cs` (and a few `#if KK` blocks).
Koikatsu runs Unity 5.6 on .NET 3.5, so the shared code sticks to APIs that exist there.

The code is limited to C# 5 (no `$""` strings, `?.` or `nameof`) because of the compiler that ships with Windows.
`decompile.bat` / `decompile_kk.bat` (optional) use dnSpy.Console to decompile each game for reference into
`decompiled\` / `decompiled_kk\`. Those folders are git-ignored and must not be published.

## Settings
All settings are in the F1 menu under **Studio Chara Filter** (saved in `BepInEx\config\WolfLikesKKS.StudioCharaFilter.cfg`).

## Notes
- Body values are shown as Maker slider values (0-100).
- Missing-mod checks ask Sideloader whether each zipmod is installed. A mod that was renamed
  and is redirected by Sideloader may still show as missing.
- Not tested in VR. The English body slider names are translations of the game's Japanese names.
- Koikatsu differences: 7 base outfits (School uniform, Going home, Gym clothes, Swimsuit, Club, Casual, Pajamas),
  18 personality traits, and only Koikatsu / Party cards are indexed (Sunshine cards can't be loaded by Koikatsu).
- Towels are recognized by item name ("towel" / タオル), or by the zipmod name for modded tops.

## FAQ

**Does it change my cards?** No. It only reads them. The index lives in `BepInEx\cache\StudioCharaFilter_*.idx`
and can be deleted at any time to rebuild it.

**The list shows fewer cards than the folder has.** A filter is active - press Clear, or check the per-section counts.
Unreadable files (not real cards) are listed under Card file > Unreadable files.

**Does it slow the game down?** No; indexing runs on background threads (F1 > Performance > Indexing threads), and the
speedups make the character list itself faster. Each speedup can be turned off in F1 > Performance.

**A mod shows as missing but I have it.** Missing-mod checks ask Sideloader. A zipmod that was renamed and is
redirected by Sideloader can still show as missing.

**Reporting problems**: open an issue on the repository with your `BepInEx\LogOutput.log`.
