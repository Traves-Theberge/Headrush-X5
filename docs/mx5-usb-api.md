# MX5 live USB API

The local service listens on `http://127.0.0.1:8765`. It combines the VX5's observed HTTP API with MX5 Bridge MIDI SysEx operations. The MX5 side requires [MX-Edit's MX5 Bridge firmware](https://github.com/TicT4x/MX-Edit), version 0.6 or later, on a compatible MX5 firmware version. It cannot control a stock MX5 through its ordinary USB audio connection. The stock alternative is the documented 3.5 mm MIDI input, available on the app's MX5 page.

To build and start the service:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\build-service.ps1
powershell.exe -ExecutionPolicy Bypass -File .\run-service.ps1
```

Start the native app separately and select **MX5 > Connect and scan**. The service binds only to loopback. It never exposes the bridge's root shell, QML evaluation, or SQL write commands.

## Routes

| Method | Route | Function |
| --- | --- | --- |
| GET | `/health` | API process health |
| GET | `/api/v1/devices` | VX5 reachability, MX5 MIDI port and transfer drive discovery |
| GET | `/api/v1/mx5/status` | Bridge handshake and version, MIDI ports, transfer drives |
| GET | `/api/v1/mx5/rigs` | Rig names, IDs, program numbers, colors |
| GET | `/api/v1/mx5/transfer/rigs` | Read rig names from the stock USB Transfer drive |
| POST | `/api/v1/mx5/rigs/load` | Load `{ "program": 1 }` (1 through 128) |
| GET | `/api/v1/mx5/properties?path=/Engine/Patch/Amp/Bass` | Read live property info |
| PUT | `/api/v1/mx5/properties` | Set `{ "path": "/Engine/Patch/Amp/Bass", "field": "unnormalized", "value": 6.5 }` |
| POST | `/api/v1/mx5/footswitches/1/press` | Press and release footswitch 1, 2, or 3 |
| GET | `/api/v1/vx5/state` | Read VX5 live parameters |
| GET | `/api/v1/vx5/metadata` | Read VX5 property tree |
| PUT | `/api/v1/vx5/state` | Set a validated scalar parameter object and read it back |

Example:

```powershell
Invoke-RestMethod http://127.0.0.1:8765/api/v1/mx5/status
Invoke-RestMethod 'http://127.0.0.1:8765/api/v1/mx5/properties?path=/Engine/PresetCtrl/Rigs/LoadedName'
```

The API serializes MX5 bridge operations to avoid overlapping WinMM port access. It reports connection failures as JSON. A healthy API process does not mean the MX5 is connected; check `connected` in `/api/v1/mx5/status`.

## Hardware state on 2026-10-01

After changing the USB connection and entering Global Settings > USB Transfer, Windows detected the `HeadRush` drive as `D:\` with 278 rig files. The visible user folders and logo were copied to the local, Git-ignored `backups/mx5-usb-transfer-2026-10-01` folder. All 1,275 copied files matched their source SHA-256 hashes. The native app can list rig names from USB Transfer, but cannot load or edit them live in that mode. The bridge MIDI port is absent in stock transfer mode, and live MX5 commands remain unverified. Compatible bridge firmware has not been installed in this project. Windows Application Control blocked the upstream patcher executable with the reason `Malicious binary reputation`; the application policy has not been bypassed.

## Protocol sources

- [MX-Edit bridge protocol](https://github.com/TicT4x/MX-Edit/blob/main/docs/PROTOCOL.md)
- [HeadRush MX5 user guide](https://cdn.inmusicbrands.com/HeadRush/mx5/MX5_User_Guide_v1.5.pdf)
