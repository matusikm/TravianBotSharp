# TravianBotSharp (TBS)

[![alt text][image]][hyperlink]

[hyperlink]: https://discord.gg/mBa4f2K
[image]:https://discordapp.com/api/guilds/740829801446637601/widget.png?style=banner4

**TravianBotSharp** is a **free** Travian bot supports [Travian Official](https://www.travian.com).

## Quick start

1. Install the **latest Google Chrome**.
2. Download TBS for your system from **GitHub Releases**: Windows (zip), macOS (Apple Silicon or Intel zip) or Ubuntu (.deb or tarball).
3. Windows: extract the zip and run `TravianBotSharp.exe`. macOS and Ubuntu: see below.
4. Add your account and start setup.

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

## Telegram notifications

Each account can send you a Telegram message when a village gets attacked, when the bot pauses on its own (an error, or no internet for 30 minutes), or when a warehouse or granary reaches 90%. It's off until you turn it on.

1. In Telegram, open **@BotFather**, send `/newbot` and copy the token it gives you.
2. Open your new bot and press **Start** (for a group, add the bot and send a message there).
3. In **Account settings > Telegram notifications**, paste the token, click **Find chat**, then **Send test**.
4. Tick **Send Telegram notifications for this account**, choose the events, and **Save**.

The token is stored encrypted, like the account passwords, and isn't included in settings exports. Messages go straight to Telegram, not through the account's proxy, and start with `[username @ server]` so one chat can serve several accounts. The bot reports each attack once while the village's attack flag stays on, and each full storage once until it drops to 80%. It can only notify while it's running.

## Tech specification

- `MainCore`: the bot itself (Patchright-driven Chrome, database, tasks, view models).
- `AvaloniaUI`: the desktop app, one UI for Windows, macOS and Linux.

The original Windows-only WPF app (`WPFUI`) was removed; the Avalonia app has the same screens. To move from it, copy `TBS.db` and `Data` from the old folder into the new one.

## Running on macOS

Download `TravianBotSharp-<version>-osx-arm64.zip` (Apple Silicon) or `-osx-x64.zip` (Intel), open it and move **TravianBotSharp** to Applications. Install Google Chrome into Applications too.
The app is signed ad hoc but not notarized by Apple, so macOS blocks the first launch: open **System Settings > Privacy & Security** and click **Open Anyway**, or run `xattr -dr com.apple.quarantine /Applications/TravianBotSharp.app`.
The database, logs and Chrome profiles are kept in `~/Library/Application Support/TravianBotSharp`.

## Running on Linux (Ubuntu)

Every release has a `.deb` and a portable tarball for Ubuntu 26.04 (24.04 works too). The bot drives real Google Chrome, so install Google's package; Chromium from snap will not work.

```bash
wget https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
sudo apt install ./google-chrome-stable_current_amd64.deb ./travianbotsharp_<version>_amd64.deb
travianbotsharp
```

The `.deb` installs to `/opt/travianbotsharp` and keeps the database (`TBS.db`), `logs/` and Chrome profiles (`Data/`) in `~/.local/share/TravianBotSharp`. The tarball (`tar -xzf TravianBotSharp-<version>-linux-x64.tar.gz -C ~`) keeps them next to the executable, the same as on Windows.

To build it yourself instead, install the .NET 10 SDK (`sudo apt install dotnet-sdk-10.0`) and Chrome, then:

```bash
dotnet publish AvaloniaUI -c Release -r linux-x64 --self-contained -o ~/tbs
~/tbs/TravianBotSharp
```

Account and proxy passwords are encrypted in `TBS.db`. On Windows the key is your Windows user (DPAPI); on Linux and macOS it is `~/.config/TravianBotSharp/secret.key`, readable only by you. If you move `TBS.db` to another computer or user, or delete that key, re-enter the passwords.
For development, `dotnet run --project AvaloniaUI` works too.
Run the tests with `dotnet test --project MainCore.Test` (they need Google Chrome installed).
To publish a release, run the **Release** workflow from the Actions tab and pick **patch**, **minor** or **major**; the version is the latest tag with that part raised (it never runs on its own). It builds the Linux tarball and `.deb`, the Windows zip and the macOS zips, checks that they start (Ubuntu 26.04, Windows, Apple Silicon), attaches them to a GitHub Release and deletes releases older than the newest 5.
After changing an entity, add a migration with `dotnet tool restore` and `dotnet ef migrations add <Name> --project MainCore --output-dir Infrasturecture/Migrations`.

## FAQs

User guides are available in [FAQs/README.md](FAQs/README.md).

- Getting started
- Add account / proxy
- Account settings
- Village build queue
- NPC and troop training
- Farming setup

