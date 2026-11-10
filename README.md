# Xargon.NET

A faithful port of **Xargon**, the 1993 DOS platform game, to modern .NET 10 and SDL3.
All three episodes run from a single executable: pick the episode on the start screen, then play.

The goal is fidelity: the original's game logic, collision, timing (the 18.2 Hz BIOS clock the DOS game ran on), graphics and text are reproduced from the original source code rather than approximated.

## Credits and ownership

**Xargon and everything that comes with it (the game, its graphics, levels, text, sound and music) is the intellectual property of Allen Pilgrim.** This project is an unofficial fan port. It would not exist without his game, and credit for the game belongs to him.

The port was written by reading the original DOS source code, which is published here:

**<https://github.com/dos-games/vanilla-xargon>**

Please go there for the original source, and treat it as the authority on how the game behaves.

## Game data is not included

This repository contains **only the port's code**. It does not contain, and must never contain, any of the game's data files, because they are proprietary. You supply them yourself: copy the original game's data files (the ones named like `GRAPHICS.XR1`, `BOARD_01.XR1`, `MAP.XR1`, `AUDIO.XR1`, and their `.XR2` / `.XR3` equivalents for the other episodes) into:

```
XargonNET/Assets/
```

The game finds that folder next to the built program, in the current directory, or in a parent folder, and needs at least `GRAPHICS.XR1` to be present. That folder, and any images or other files extracted from the game data, are excluded by `.gitignore` so they can't be committed by accident.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The SDL3 libraries come from the `SDL3-CS` NuGet packages, so there is nothing else to install.

```bash
git clone https://github.com/becausepurple/Xargon.NET.git
cd Xargon.NET
# put the game data in XargonNET/Assets/ (see above)
dotnet build XargonNET
```

The build should finish with 0 warnings and 0 errors. It has only been tried on Windows so far.

## Running

```bash
dotnet run --project XargonNET
```

The window is 3x the original 320x200 size and scales to fit. Choose an episode, then use the main menu.

### Controls

| Key | Action |
|---|---|
| Arrow keys | Move; Up/Down climb vines and ride lifts; Up uses doors and reads signs |
| Space or Shift | Fire |
| Ctrl or Alt | Jump |
| G | Toggle "granny mode" (half speed) while playing |
| Esc | Back to the menu (no in-game menu yet) |
| Enter or Space | Choose in menus, dismiss message boxes |

### Status

Working: the three episodes' levels and map, enemies, pickups, platforms and doors, the bird and fish forms, death and checkpoints, text messages (signs, hints, dialog windows, level banners), the story and credits pages, and the status bar.

Not yet done: sound and music, saving and loading, high scores, the demo, the in-game menu and game-over screens, and the final boss and episode endings.

## Tests and tools

```bash
dotnet test XargonNET.Tests
```

The tests load the real game data through the same code the game uses, so they need the data files in `XargonNET/Assets/` too.

`dotnet run --project XargonNET -- <command>` also runs a set of headless inspection commands used during development, for example `--verify` (checks that the data files decode), `--census <episode>` (lists which object kinds are implemented), `--objlist` / `--findobj` (inspect level contents) and `--page`, `--popup`, `--banner` (render screens to PNG files in the current folder). Rendered images are ignored by git.

## Layout

```
XargonNET/          the game
  Assets/           loaders for the data files (your data files go here too)
  Engine/           the main loop, page viewer, popups, level transitions
  Objects/          object logic, player, collision, one file per kind of behaviour
  Text/             in-game messages and strings
  Ui/               windows, menus, status bar, fonts
  Video/            the VGA-style software screen and SDL presentation
XargonNET.Tests/    unit and integration tests
```

## License

No license has been chosen yet for the port's code. Xargon itself, including all game data, remains the property of Allen Pilgrim, and this port is derived from the source code he published under his own terms. See [NOTICE.md](NOTICE.md) for the ownership notice and those terms.
