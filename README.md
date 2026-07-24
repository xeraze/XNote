# XNote

A minimal, monochrome desktop notes & tasks app for Windows, built with
**C# and Avalonia UI**. Splash screen with a fade in/out animation, then a
dark, black/grey/white two-pane note editor. Notes persist locally as JSON.

## Requirements (one-time setup)

You need the **.NET 8 SDK** installed to build it. You do **not** need
Visual Studio — this all works from PowerShell.

1. Download and install the .NET 8 SDK from:
   https://dotnet.microsoft.com/download/dotnet/8.0
   (pick the "SDK" installer for Windows x64, not just the runtime)
2. Verify it installed, in PowerShell:
   ```powershell
   dotnet --version
   ```
   should print something like `8.0.xxx`.

That's it — Avalonia itself is a NuGet package and gets downloaded
automatically the first time you build (this requires an internet
connection once; after that, builds work offline).

## Running it while you develop (fastest way to try it)

Unzip the project, then in PowerShell:

```powershell
cd XNote
dotnet run
```

The first run will take a bit longer (downloading Avalonia packages).
A window should appear: splash screen first, then the main app after
about 2 seconds.

## Building a real .exe you can double-click

This produces a single, self-contained `XNote.exe` — no .NET install
required on whatever machine runs it, no console window, just double-click
and go.

```powershell
cd XNote
dotnet publish -c Release -r win-x64 --self-contained true
```

Your `.exe` will show up at:

```
XNote\bin\Release\net8.0\win-x64\publish\XNote.exe
```

Copy that one file (it's ~60-80MB because the whole .NET runtime is bundled
inside it) anywhere you like — Desktop, a Programs folder, wherever — and
make a shortcut to it. Double-click runs the app directly.

> If you built this on Linux/WSL/Mac and want a Windows exe, the `-r win-x64`
> flag above already handles that — .NET publish is cross-targeting, so
> running that same command from any OS produces a Windows binary. You just
> need to actually run/test it on Windows.

## Using the app

- **`+`** in the sidebar — new note
- Click a note in the list to open it in the editor on the right
- **Task** toggle — marks the note as a task instead of a plain note
- **Done** toggle — appears once a note is a task; marks it complete
  (dims it in the list)
- Tags field — comma-separated, saved automatically
- Search box — filters by title, body, and tags as you type
- **✕** — deletes the currently open note
- Everything saves automatically as you type — there's no separate save
  button/shortcut needed

## Where your data lives

`%APPDATA%\XNote\notes.json` — plain JSON, human-readable, easy to back up
or inspect. Saves are atomic (write to a temp file, then swap), so an
interrupted save can't corrupt your existing notes.

## Project structure

```
XNote/
├── XNote.csproj          # project file, Avalonia package references
├── app.manifest           # Windows DPI-awareness manifest
├── Program.cs              # entry point
├── App.axaml(.cs)          # app startup, global theme/colors
├── Models/
│   └── Note.cs              # plain data model (no UI/framework code)
├── Services/
│   └── NoteStore.cs          # JSON load/save, atomic writes
├── ViewModels/
│   ├── ViewModelBase.cs       # small hand-rolled INotifyPropertyChanged base
│   ├── RelayCommand.cs         # ICommand for button bindings
│   ├── NoteViewModel.cs         # wraps Note for UI-bindable properties
│   ├── MainViewModel.cs          # note list, search, add/delete, persistence
│   └── Converters.cs              # XAML value converters
└── Views/
    ├── SplashWindow.axaml(.cs)     # intro screen with fade in/out
    └── MainWindow.axaml(.cs)        # sidebar + editor layout
```

## Design notes

- **MVVM architecture**: Views are dumb XAML bound to ViewModels;
  ViewModels hold no UI framework types (except `Converters.cs`, which by
  nature has to talk to `IBrush`); Models are plain data. This is the
  standard shape of a real Avalonia/WPF app, not a toy structure.
- **No external MVVM framework** — `ViewModelBase`/`RelayCommand` are
  hand-written (a dozen lines each) rather than pulling in
  CommunityToolkit.Mvvm, to keep the dependency list to just Avalonia
  itself.
- **Storage is plain JSON**, not a database — appropriate for a
  single-user local notes app, and it means you can open `notes.json` in
  any text editor if you're curious what's in it.
- **The splash screen** is a second, borderless `Window` (not a control
  inside the main window) so it can appear instantly and independently
  fade before the (heavier) main window's controls are constructed.

## Testing

The core logic (models, storage, view models) was tested via a standalone
console harness during development — 35 checks covering save/load
roundtrips, atomic writes, corrupted-file recovery, tag parsing/dedup, and
full add/search/delete flows through `MainViewModel`, all passing. That
harness isn't included in this package (it lived outside the app project),
but the logic it covered is unchanged in `Models/`, `Services/`, and
`ViewModels/`.

The Avalonia UI layer itself (XAML, animations, rendering) needs an actual
.NET+Avalonia environment to run, which isn't available in the environment
this was written in — so please treat the visual side as reviewed
carefully by hand rather than verified by an automated build. If
`dotnet run` throws anything on your machine, send me the error and I'll
fix it immediately.