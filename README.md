# XNote

XNote is a desktop note-taking application built with C# and Avalonia. The current project targets .NET 10 and focuses on a lightweight local-first workflow with notes, tasks, reminders, search/filtering, tray integration, and a custom window shell.

## Current project scope

The app already includes the following workflow pieces:

- note creation, editing, deletion and local persistence
- task/done state for notes
- reminder scheduling and notification popups
- tray icon support with quick new-note actions
- search and filter helpers for note discovery
- per-note metadata such as creation and last-modified timestamps
- an unsaved-changes indicator in the UI
- a custom window frame and settings/shortcuts surface

## Tech stack

- .NET 10
- Avalonia UI
- C# project with MVVM-style separation between Views, ViewModels, and Models

## Setup

Install .NET 10 SDK from the official Microsoft site, then verify the toolchain:

```powershell
dotnet --version
```

## Run in development mode

```powershell
cd XNote
dotnet run
```

## Build a Windows executable

```powershell
cd XNote
dotnet publish -c Release -r win-x64 --self-contained true
```

The publish output lands in a folder similar to:

```text
XNote\bin\Release\net10.0\win-x64\publish\
```

## Notes storage

The app keeps data locally in the Windows user profile, which is the usual place for a desktop notes app using a JSON file store.

## Project structure

```text
XNote/
├── App.axaml(.cs)
├── Program.cs
├── XNote.csproj
├── Assets/
├── Models/
├── Services/
├── ViewModels/
├── Views/
└── README.md
```

## Notes on the current status

This README is intentionally written to match the current codebase rather than the older minimal feature list. The project has since expanded with reminder-driven notifications, tray interaction, and richer note metadata.

## Build note

The obsolete `Window.SystemDecorations` usage in the notification window has been replaced with the current Avalonia property so the XAML stays compatible with new framework versions.