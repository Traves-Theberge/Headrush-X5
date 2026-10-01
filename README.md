# X5 Control

An unofficial native Windows controller and local API for the HeadRush VX5 and MX5. It reads and edits live VX5 settings over USB, sends documented MIDI performance commands, and stores custom patch snapshots on the PC. MX5 live rig and parameter control is implemented for compatible MX5 Bridge firmware.

| Device | Status | Connection |
| --- | --- | --- |
| VX5 | Tested with firmware 1.3.1 | USB for live settings; Windows MIDI output for preset and performance commands |
| MX5 | API and UI implemented; live hardware unverified | MX5 Bridge USB MIDI for live control; external MIDI interface to the 3.5 mm input for stock performance commands |

This project is independent of HeadRush, Antares, and inMusic Brands.

## Get started

**Portable build:** Extract the complete `X5Control-*-windows-portable.zip` to a writable folder, then run `X5Control.exe`. Keep `MainWindow.xaml` beside the EXE. Windows with .NET Framework 4.8 is required. Build a ZIP from source with `powershell.exe -ExecutionPolicy Bypass -File .\package.ps1`; the ZIP and SHA-256 checksum appear in `dist`.

**Source build:** Connect and power on the VX5 by USB, then run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\build.ps1
.\bin\X5Control.exe
```

The build uses the Windows .NET Framework C# compiler and needs no package download. The VX5 device API is reached at `http://vx5.local`; Windows must resolve that address through the USB connection. Choose the VX5 MIDI output in **Advanced** before requesting a preset, A/B mode, or Talk mode.

The release ZIP contains no personal preset library. Close the old app and copy `bin\presets.user.json` beside the extracted EXE to move your presets. Keep that JSON file private when sharing the app.

`X5Control.csproj` provides a conventional WPF project for Visual Studio. While the app is running, build into another folder with `powershell.exe -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory bin\verify`; Windows locks a running executable.

## VX5 workflow

The VX5 landing view mirrors the pedal. Click **Pedal View** to return to it from any submenu. Twist the Volume, Speed, or Humanize knobs by dragging around their rims, or drag vertically from the center. The indicator rotates with the value. Their sliders stay in sync; release to send the value and read it back. The **Set** buttons also send the selected value. The five submenu buttons follow the pedal: **Preset**, **Key**, **Auto-Tune**, **Harmony**, and **FX**. **Advanced** exposes every live parameter reported by the unit.

**Preset** lists the 99 factory names from the user guide and labels the other slots locally. Search, rename, load, capture, edit, and apply from that menu. **Create custom patch** copies the current live settings into a named PC snapshot, then opens its editor. The edited patch can be applied to the selected slot after that slot has been loaded through the app. Names and snapshots are saved in `bin\presets.user.json` on this PC. Applying parameters changes the live device state; saving them permanently as a device preset still uses the pedal's Preset/Edit control. The VX5 USB API has not exposed device preset names or a save command.

**Key** selects key, scale, and vocal range together and reads back the VX5 state. The same key and scale are available on the pedal view for quick changes.

## MX5

The MX5 panel now includes a live rig browser, rig recall, and a property editor backed by a local API. Start it with `powershell.exe -ExecutionPolicy Bypass -File .\run-service.ps1`, then select **MX5 > Connect and scan** in the app. Node.js is required for the API. The bridge client uses Windows MIDI directly and needs compatible [MX5 Bridge firmware](https://github.com/TicT4x/MX-Edit); ordinary stock USB audio mode does not expose live parameter control. See [MX5 USB API](docs/mx5-usb-api.md) for routes, examples, and the current hardware result.

The MX5 panel also sends documented MIDI program changes, footswitch commands, effect block commands, and looper commands through an external USB MIDI interface connected to the MX5's 3.5 mm MIDI input. Enable MIDI Program Change receive on the pedal. **USB Transfer** can expose rig files as a drive for read-only scanning. The MX5 has not yet been detected by this PC over USB, so live bridge commands remain unverified.

## Current limits

- The VX5 USB API is undocumented. Device firmware updates may change it.
- VX5 preset names and snapshots live in `presets.user.json` on the PC. The app cannot write a permanent preset to the pedal; save it with the VX5 Preset/Edit control.
- VX5 MIDI preset recall, A/B, and Talk commands are sent using the documented map, but the pedal has not provided a readback confirming each MIDI command.
- Live MX5 control requires third-party bridge firmware and an enumerated USB MIDI port. Neither was present during this development pass.

## Project files

| Path | Purpose |
| --- | --- |
| `src/App/Program.cs` | Startup, window navigation, and shared controller state |
| `src/Ui/StageWindow.xaml` | Pedal face, menus, and visual styles |
| `src/Features/Vx5/Vx5.cs` | VX5 API, live parameters, knob and key controls |
| `src/Features/Presets/Presets.cs` | Preset names, snapshots, custom patches |
| `src/Features/Mx5/Mx5.cs` | MX5 MIDI control panel |
| `service/BridgeCli.cs` | Windows MIDI SysEx bridge client |
| `service/server.js` | Local HTTP API for VX5 and MX5 |
| `build-service.ps1`, `run-service.ps1` | Build and start the API |
| `src/Infrastructure/` | Windows MIDI access and port selection |
| `src/Models/Models.cs` | Preset and MIDI port data models |

See [architecture](docs/architecture.md), [control behavior](docs/controls.md), [MX5 USB API](docs/mx5-usb-api.md), [UI direction](docs/ui.md), [release process](docs/release.md), and [changelog](CHANGELOG.md).

## Device references

- [VX5 user guide](https://cdn.inmusicbrands.com/HeadRush/vx5/AutoTune%20VX5%20-%20User%20Guide%20-%20v1.3.pdf)
- [MX5 user guide](https://cdn.inmusicbrands.com/HeadRush/mx5/MX5_User_Guide_v1.5.pdf)
