# G-Helper Auto Profile Switcher

A lightweight Windows application that automatically switches [G-Helper](https://github.com/seerge/g-helper) profiles based on the currently running processes.

## Features
- **Automatic Profile Switching:** Seamlessly switch to a specific G-Helper profile (e.g., Turbo, Balanced, Eco) when a designated application (like a game) is running.
- **Process Detection:** Periodically scans running processes to apply the correct profile.
- **System Tray Integration:** Runs quietly in the background, accessible via the system tray.
- **Start with Windows:** Easily configure the app to launch automatically when you boot your PC.
- **Simple UI:** Add, remove, and manage your app profiles with an intuitive graphical interface.

## How it Works
The application monitors the list of running processes every few seconds. If it detects a process name that matches one of your configured profiles, it sends the appropriate command (via hotkeys or IPC) to G-Helper to switch the profile. 

## Requirements
- Windows OS
- .NET 8.0 Desktop Runtime
- [G-Helper](https://github.com/seerge/g-helper) installed and running.

## Installation
1. Go to the [Releases](../../releases) page.
2. Download the latest version.
3. Extract the files and run `GHelperAutoProfileSwitcher.exe`.

## Building from Source
To build the project yourself:

1. Clone the repository:
   ```bash
   git clone https://github.com/Lunarstar/GHelper-Auto-Profile-Switcher.git
   ```
2. Open the project in Visual Studio or use the .NET CLI:
   ```bash
   dotnet build --configuration Release
   ```
3. The executable will be located in the `bin/Release/net8.0-windows/` folder.

## Usage
1. Launch the application.
2. Click **Add Current App** to select a currently running application and assign a target mode (e.g., Turbo, Silent, Balanced).
3. Check the **Start with Windows** option to ensure it runs continuously in the background.
4. Minimize the application to the system tray to keep it out of the way.

## Thx Gemini :D

## License
MIT License
