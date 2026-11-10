# Xargon.NET

A port of **Xargon**, the 1993 DOS platform game, to .NET 10 and SDL3. All three episodes run from a single executable.

## Credits and ownership

**Xargon and everything that comes with it (the game, its graphics, levels, text, sound and music) is the intellectual property of Allen Pilgrim.** This project is an unofficial fan port.

The port was written from the original DOS source code, published at <https://github.com/dos-games/vanilla-xargon>.

## Game data is not included

This repository contains only the port's code. The game's data files are proprietary, so you supply your own: copy the original `.XR1` / `.XR2` / `.XR3` files (`GRAPHICS.XR1`, `BOARD_01.XR1`, `MAP.XR1`, `AUDIO.XR1` and the rest) into:

```
XargonNET/Assets/
```

At minimum `GRAPHICS.XR1` must be present.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). SDL3 comes from the `SDL3-CS` NuGet packages.

```bash
git clone https://github.com/becausepurple/Xargon.NET.git
cd Xargon.NET
# put the game data in XargonNET/Assets/ (see above)
dotnet build XargonNET
```

## Running

```bash
dotnet run --project XargonNET
```

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

The tests need the game data files in `XargonNET/Assets/`.

`dotnet run --project XargonNET -- <command>` runs headless inspection commands used during development, for example `--verify` (checks that the data files decode), `--census <episode>` (lists which object kinds are implemented), `--objlist` / `--findobj` (inspect level contents) and `--page`, `--popup`, `--banner` (render screens to PNG files in the current folder).

## Layout

```
XargonNET/          the game
  Assets/           data file loaders
  Engine/           main loop, page viewer, popups, level transitions
  Objects/          object logic, player, collision
  Text/             in-game messages and strings
  Ui/               windows, menus, status bar, fonts
  Video/            software VGA screen and SDL presentation
XargonNET.Tests/    unit and integration tests
```

## License

No license has been chosen yet for the port's code. Xargon itself, including all game data, remains the property of Allen Pilgrim, and this port is derived from the source code he published under his own terms. See [NOTICE.md](NOTICE.md) for the ownership notice and those terms.
