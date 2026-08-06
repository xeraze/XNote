# XNote

A lightweight desktop notes app for Windows. Everything stays on your machine — no accounts, no cloud.

---

## What you get

- **Notes** with rich text, tags, and search
- **Tasks** — mark notes as open or done
- **Reminders** — scheduled notifications with sound
- **Timed notes** — auto-delete when the timer runs out
- **Drafts** — unsaved notes are clearly marked
- **Tray** — minimize to the system tray; app keeps running in the background
- **Import / export** — bring in `.txt` files or save note text out
- **English & Russian** UI (switch in Settings)

Data is stored locally in your Windows profile (`%AppData%\XNote`).

---

## Download & run

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

**Run from source**

```powershell
dotnet run
```

**Build a release folder**

```powershell
dotnet publish -c Release
```

The ready-to-use app lands in `publish/XNote/` — `XNote.exe` plus an `Assets` folder. Dependencies are bundled into the executable, so you won't get a pile of DLL files next to it.

---

## Tech

Open source · LICENSED

| | |
|---|---|
| **Runtime** | .NET 10 |
| **UI** | [Avalonia](https://avaloniaui.net) |
| **Editor** | AvaloniaRichEditor |

---

*v0.7 · developed by xeraze*