# TravianBotSharp (TBS)

[![alt text][image]][hyperlink]

[hyperlink]: https://discord.gg/mBa4f2K
[image]:https://discordapp.com/api/guilds/740829801446637601/widget.png?style=banner4

**TravianBotSharp** is a **free** Travian bot supports [Travian Official](https://www.travian.com).

## Quick start

1. Download TBS from **GitHub Releases**.
2. Extract files to a local folder.
3. Make sure you are on **Windows** and using the **latest Google Chrome**.
4. Run `TravianBotSharp.exe`.
5. Add your account and start setup.

## Main features

- Multi-account support
- Auto builder (support use resource from hero inventory, instant upgrade)
- Auto start adventures
- Auto start farm lists
- Auto claim daily quest/ village quest
- Auto NPC
- Auto train troop

## Activity and timing

These settings change when and how the bot acts, so its activity looks less mechanical. They lower the risk of being noticed; they can't make a bot undetectable.

- **Work time** (hours, in Account settings): after it, the bot either sleeps and continues, or, with *log out and stop*, logs out of Travian, closes Chrome and waits until you press Login.
- **Night sleep**: the bot always sleeps in the night window (default 23:30 to 07:00, shifted by up to 45 minutes each day), in the account's time zone.
- **Time zone and language** (Chrome settings): leave them empty and the bot looks up the current IP's location at each login (ipinfo.io, falling back to ipapi.co) and sets Chrome's time zone and language to match. Or set them, for example `Asia/Bangkok` and `en-US`.
- **Farming** goes out in bursts of 2 to 5 sends at the farm interval, then pauses for the idle gap (default 9 to 63 minutes), and skips about one cycle in ten.
- Click and task delays are usually near the low end of their range, sometimes higher, and now and then much longer. New accounts get their own randomised delay, farm and sleep ranges.
- Clicks move the mouse to a random point inside the element; text is typed one key at a time.

## Tech specification

TBS has two desktop front ends that share the same bot core (`MainCore`):

- `WPFUI`: the original Windows app.
- `AvaloniaUI`: a cross-platform app for Linux (tested on Ubuntu) and Windows.

## Running on Linux (Ubuntu)

1. Install the .NET 10 SDK and Google Chrome (the bot drives real Chrome, so Chromium from snap will not work):

   ```bash
   sudo apt install dotnet-sdk-10.0
   wget https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
   sudo apt install ./google-chrome-stable_current_amd64.deb
   ```

2. Build a self-contained copy:

   ```bash
   dotnet publish AvaloniaUI -c Release -r linux-x64 --self-contained -o ~/tbs
   ```

3. Run it:

   ```bash
   ~/tbs/TravianBotSharp
   ```

The database (`TBS.db`), `logs/` and Chrome profiles (`Data/`) are stored next to the executable, the same as on Windows.

Account and proxy passwords are encrypted in `TBS.db`. On Windows the key is your Windows user (DPAPI); on Linux it is `~/.config/TravianBotSharp/secret.key`, readable only by you. If you move `TBS.db` to another computer or user, or delete that key, re-enter the passwords.
For development, `dotnet run --project AvaloniaUI` works too.
Run the tests with `dotnet test --project MainCore.Test` (they need Google Chrome installed).
After changing an entity, add a migration with `dotnet tool restore` and `dotnet ef migrations add <Name> --project MainCore --output-dir Infrasturecture/Migrations`.

## FAQs

User guides are available in [FAQs/README.md](FAQs/README.md).

- Getting started
- Add account / proxy
- Account settings
- Village build queue
- NPC and troop training
- Farming setup

