# DepressoWorker (CatTimer) 🐱

> I work at 8-17 sometime no work but need to keep computuer stay on standby mode so When the time reaches 17:00 i want to shutdown so can easy to run back home

A tiny Windows system-tray app that keeps your PC awake during the workday and shuts it down automatically when it's time to go home.

## Features
- **Keep awake** – taps Caps Lock twice every 5 minutes (state unchanged) and tells Windows not to sleep or turn off the display.
- **Auto shutdown at 17:00** – a popup warns you 5 minutes before, with a countdown.
  - **Snooze +5 min** (as many times as you like)
  - **Shut down now**
- **Tray menu** (right-click the cat icon)
  - Shutdown target time
  - Snooze info (count, minutes added, original time)
  - Change target time (also on double-click)
  - Exit

## Usage
1. Run `CatTimer.exe` – the cat appears in the system tray.
2. Optional: run `add-to-startup.bat` once to start CatTimer automatically when you log in.

## Build
No install needed. Run `build.bat` – it uses the C# compiler that ships with Windows (.NET Framework 4.x) to build `CatTimer.exe` from `CatTimer.cs`.

## Notes
- Changing the target time applies to the current day; on next launch it's 17:00 again. If started after 17:00, the target is 17:00 tomorrow.
- Uses a normal (non-forced) shutdown, so apps with unsaved work may prompt you.
