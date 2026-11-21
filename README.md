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
For development, `dotnet run --project AvaloniaUI` works too.
Run the tests with `dotnet test --project MainCore.Test` (they need Google Chrome installed).

## FAQs

User guides are available in [FAQs/README.md](FAQs/README.md).

- Getting started
- Add account / proxy
- Account settings
- Village build queue
- NPC and troop training
- Farming setup

