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

## Tech specification

TBS has two desktop front ends that share the same bot core (`MainCore`):

- `WPFUI`: the original Windows app.
- `AvaloniaUI`: a cross-platform app for Linux (tested on Ubuntu) and Windows.

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

Account and proxy passwords are encrypted in `TBS.db`. On Windows the key is your Windows user (DPAPI); on Linux it is `~/.config/TravianBotSharp/secret.key`, readable only by you. If you move `TBS.db` to another computer or user, or delete that key, re-enter the passwords.
For development, `dotnet run --project AvaloniaUI` works too.
Run the tests with `dotnet test --project MainCore.Test` (they need Google Chrome installed).
To publish a Linux release, push a tag like `1.2.0` or run the **Release (Linux)** workflow from the Actions tab with a version; it builds the tarball and `.deb`, checks that they install and start on Ubuntu 26.04, and attaches them to a GitHub Release.
After changing an entity, add a migration with `dotnet tool restore` and `dotnet ef migrations add <Name> --project MainCore --output-dir Infrasturecture/Migrations`.

## FAQs

User guides are available in [FAQs/README.md](FAQs/README.md).

- Getting started
- Add account / proxy
- Account settings
- Village build queue
- NPC and troop training
- Farming setup

