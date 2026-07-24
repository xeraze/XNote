# XNote

Desktop notes app, C# + Avalonia. Black/grey theme, splash screen with fade animation, then a two-pane note list + editor. Saves to a local JSON file.

## Setup

Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0 (the SDK installer, not just runtime).

Check it worked:
```powershell
dotnet --version
```

## Run it (dev mode)

```powershell
cd XNote
dotnet run
```

First run downloads Avalonia packages, takes a bit longer. This does NOT create an exe you can double-click — see below for that.

## Get an actual .exe

```powershell
cd XNote
dotnet publish -c Release -r win-x64 --self-contained true
```

The exe lands here:
```
XNote\bin\Release\net8.0\win-x64\publish\XNote.exe
```

That's the file to put on your desktop / make a shortcut to. It's ~60-80MB since the .NET runtime is bundled inside — runs on any Windows machine with no install needed.

## Using it

- `+` — new note
- click a note in the list — opens it in the editor
- `Task` toggle — makes it a task instead of a note
- `Done` toggle — shows up on tasks, marks complete
- tags field — comma separated
- search box — filters as you type
- `✕` — delete current note
- saves automatically, no save button

## Where data is stored

`%APPDATA%\XNote\notes.json` — plain JSON, readable in any text editor.

## Structure

```
XNote/
├── XNote.csproj
├── Program.cs
├── App.axaml(.cs)
├── Models/Note.cs
├── Services/NoteStore.cs
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── RelayCommand.cs
│   ├── NoteViewModel.cs
│   ├── MainViewModel.cs
│   └── Converters.cs
└── Views/
    ├── SplashWindow.axaml(.cs)
    └── MainWindow.axaml(.cs)
```

MVVM: Views are XAML bound to ViewModels, ViewModels don't touch UI types (except Converters.cs), Models are plain data. No MVVM framework dependency — ViewModelBase/RelayCommand are hand-written instead of pulling in CommunityToolkit.Mvvm.

## Testing

Core logic (models, storage, viewmodels) was tested separately during development — 35 checks, all passing, covering save/load, atomic writes, corrupted-file handling, tags, add/search/delete. The Avalonia UI itself couldn't be build-tested in the environment this was written in, so if `dotnet run` throws an error, send it over and it'll get fixed.