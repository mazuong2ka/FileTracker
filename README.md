<div align="center">

# 📁 FileTracker

**See what changed on your Windows PC: recent files, change history and large files in one place.**

**English** | [Tiếng Việt](README.vi.md)

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D6?logo=windows&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet&logoColor=white)
![Admin](https://img.shields.io/badge/admin%20rights-not%20required-2ea44f)
![Data](https://img.shields.io/badge/data-stays%20on%20your%20PC-2ea44f)
[![Release](https://img.shields.io/github/v/release/mazuong2ka/FileTracker?label=release)](https://github.com/mazuong2ka/FileTracker/releases/latest)

[⬇️ **Download the latest release**](https://github.com/mazuong2ka/FileTracker/releases/latest)

</div>

<!-- Optional app icon: add docs/icon.png, then uncomment the image below.
<p align="center"><img src="docs/icon.png" width="96" alt="FileTracker blue folder app icon"></p>
-->

---

## ✨ Features

| | |
|---|---|
| 🕘 **Recent files** | Grouped by day; sort by created date, modified date or size. |
| 🔄 **Change history** | Added, modified, deleted and renamed/moved files inside the watched scope. |
| 🔎 **Search & filters** | Search by name or path; filter by folder, file type and time range. |
| ⭐ **Starred files** | Star files to reopen them quickly. |
| 📦 **Large files** | Files of 100 MB or more, largest first. |
| 📂 **Quick actions** | Open a file, open its folder, copy its path. Executable files ask for confirmation. |
| 📤 **CSV export** | Export by the current filter (up to 50,000 rows per export, with a notice if the limit is hit). |
| ⏯️ **Pause / resume** | Manual rescan, plus automatic sync every 30 minutes. |
| 🔔 **System tray** | Runs in the tray. Closing the window does not quit the app by default. |
| 🚀 **Start with Windows** | Optional, **off** by default. |
| 🌐 **English / Vietnamese** | Switch the app interface between English and Vietnamese. |
| 🎨 **Custom app icon** | A custom blue folder icon for FileTracker. |

FileTracker **never edits, moves or deletes your original files**.

## 🆕 What's New in v1.1.0

- Switch the app interface between **English and Vietnamese**.
- Added a **custom blue folder app icon**.

See [Releases](https://github.com/mazuong2ka/FileTracker/releases) for release notes and downloads.

## 📸 Screenshots

To show both interface languages, add `docs/screenshot-en.png` and `docs/screenshot-vi.png` to the repository. Uncomment each image below only after its corresponding file is available.

<!-- English interface — uncomment after adding docs/screenshot-en.png:
![FileTracker — English interface](docs/screenshot-en.png)
-->

<!-- Vietnamese interface — uncomment after adding docs/screenshot-vi.png:
![FileTracker — Vietnamese interface](docs/screenshot-vi.png)
-->

## 🚀 Quick start

1. Extract the whole ZIP into a permanent folder.
2. Run `FileTracker.exe`. Keep `FileTracker.exe.config` in the same folder.
3. Wait for the first scan. Results update while it runs.

**Requirements:** Windows 10/11 x64 and .NET Framework 4.8. No Python, Node.js, internet connection or Administrator rights needed. The app is not signed with a commercial code-signing certificate, so Windows may show a security warning the first time you run it.

## 🎯 Default scope

- Program Files (64-bit)
- Program Files (x86)
- Downloads of the current Windows account, including relocated folders
- `D:\` and its subfolders

You can add or remove locations in **Folders & Settings** (*Thư mục & Cài đặt*). After saving, choose **Exit** from the tray icon and reopen the app to apply the new scope.

## ⌨️ Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl` + `F` | Search |
| `Ctrl` + `C` | Copy the path of the selected row |
| `F5` | Rescan |

## 🕒 How to read the timestamps

- **Created** and **Modified** come from Windows metadata. They are not necessarily when you downloaded or saved the file, because copying, extracting and other apps can change them.
- The first scan builds a list of existing files. It does **not** reconstruct past activity.
- Events **without** the word "Detected" (*Phát hiện*) were captured by the watcher while the app was running.
- Events labelled **Detected added / modified / missing** come from a rescan. The time shown is when the change was detected, not the exact moment it happened while the app was off.

<details>
<summary><b>⚠️ Limitations (please read)</b></summary>

<br>

- FileTracker relies on filesystem notifications and metadata comparison. **It is not a full audit log.** Very fast or duplicated events, events during pause/restart, and buffer overflows may not be recorded completely.
- A file created and deleted while the app was off cannot be recovered from history.
- The app does not store file contents or old versions, and it cannot restore deleted files.
- Renaming a folder is reconciled as files added/missing. Moving files between two watched scopes may appear as *Deleted* + *Added* rather than one *Moved* event.
- Each page shows 200 items. History is trimmed to the most recent 100,000 events after each sync. The file index is stored permanently.
- "Hide technical files" only filters the list. Untick it to see DLLs, logs, etc.
- By default these are skipped: `.git`, `node_modules`, `__pycache__`, `.venv`, `venv` (configurable in Settings). Recycle Bin, System Volume Information, directory junctions/symlinks and folders you do not have permission to read are also skipped. FileTracker never goes beyond your access rights.
- A disconnected drive or unreadable folder is **not** treated as "all files deleted".
- Scanning a large `D:` drive can take several minutes and increases disk reads at first.

</details>

## 🔒 Privacy & data

FileTracker has no feature that sends data over the network. It only stores file names, paths, metadata, stars and events in:

```text
%LOCALAPPDATA%\FileTracker
```

The database contains personal paths, so share a CSV or the database only when you mean to.
The release package contains no index, personal settings or test data.

### Portable mode (optional)

Create an empty file named `FileTracker.portable` next to `FileTracker.exe` **before** launching the app. Data will then live in a `Data` folder next to the app (write permission required; do not put `Data` inside Program Files).

You can also choose the folder explicitly:

```powershell
FileTracker.exe --data-dir "C:\path\to\Data"
```

Use `--tray` to start the app hidden in the system tray.

### Backup

Fully **Exit** the app, then copy the whole data folder.

### Uninstall

1. Turn off **Start with Windows** inside the app.
2. Choose **Exit** from the tray icon.
3. Delete the extracted folder.
4. *(Optional)* Delete the data folder. This erases your history.

> If you enabled auto-start, do not move the EXE without updating the setting first.

## 🧱 Source code

| File | Purpose |
|---|---|
| `Core.cs` | SQLite index (Windows built-in `winsqlite3`), watcher, reconciliation scan, settings storage |
| `App.cs` | Bilingual English/Vietnamese UI, search/filters, stars, CSV export, tray and settings |
| `build.ps1` | Builds with the C# compiler that ships with .NET Framework |
| `tests/IntegrationTests.cs` | Functional tests that run in their own temporary folder |

## ✅ What has been tested

The initial index creates no fake history; live add/modify; Unicode file names; rename keeps the old path and star; delete; skipping developer folders; reconciliation after pause/reopen; unavailable drive; SQLite integrity; saving and reloading settings. Tests never modify your personal files.
