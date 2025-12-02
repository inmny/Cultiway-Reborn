# Repository Guidelines

## Project Structure & Module Organization
Core gameplay logic lives in `Source/`, with major subsystems split
across `Core/` (extensions, systems), `Patch/` (Harmony patches), `UI/`,
`Utils/`, and `LocaleKeys/`. Assets and data tables ship from `Content/
  ` and `GameResources/`, while localization strings reside in `Locales/`.
`Scripts/` hosts Python helpers for data conversion and balancing, and the
compiled assemblies land in `bin/<Configuration>/net48/`.
Game source code lives in `.GameSource/`.

## Build, Test, and Development Commands
- `dotnet build Cultiway.csproj -c Debug`: restores references to the local
  WorldBox installation and outputs a debug DLL to `bin/Debug/net48/`.
- `dotnet build Cultiway.csproj -c Release /p:Optimize=true`: produces
  the release DLL used for packaging; copy it alongside `mod.json` into
  `worldbox_Data/StreamingAssets/mods/Cultiway`.
- `python Scripts/csv2json.py Tables/<file>.csv`: converts authoring
  spreadsheets to the JSON format consumed under `Content/`.
- `python Scripts/count_source_lines.py Source`: quick sanity check on code
  size when reviewing PR scope.

## Coding Style & Naming Conventions
The project targets `net48` with C# 12 syntax and unsafe code enabled. Use
4-space indentation, `PascalCase` for public types/methods, and `camelCase`
for locals and private fields (prefix with `_` only when required for
clarity). Group related extensions in `<Concept>Extend.cs` files under
`Source/Core`, and keep Harmony patches mirrored under `Source/Patch`.
Nullable annotations are disabled, so favor explicit guards and utility
methods in `Source/Utils`. Run ReSharper/IDE auto-formatting with the
included `.DotSettings` profiles before committing. Comments should be writen in Chinese (UTF-8).

## Testing Guidelines
There is no automated test suite; validation happens in WorldBox. After
building, load the mod through NeoModLoader, enable the debug tools
in `Source/Debug`, and verify the affected systems (e.g., spells, sect
mechanics) via in-game scenarios. Regression-test any data changes by
regenerating derived JSON with the scripts above and confirming that UI
assets under `GameResources/cultiway` still render as expected.

## Commit & Pull Request Guidelines
Follow the existing Conventional Commit style (`feat:`, `bugfix:`,
`feat(scope): description`) seen in `git log -5`. Keep summaries short,
present tense, and scoped to one change. Pull requests should describe
gameplay impact, include reproduction or verification steps, and attach
screenshots/gifs for UI-affecting changes. Reference related roadmap items
or issues, call out any new resource files added under `GameResources/`,
and mention if manual migration steps are required for mod users. 
Commit message should be writen in Chinese.