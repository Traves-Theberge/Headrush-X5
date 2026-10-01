# Changelog

## 0.2.1 - 2026-10-01

- Confirmed USB Transfer detection and made a locally verified backup of the MX5 user files.
- Added read-only MX5 rig browsing from the stock USB Transfer drive.

## 0.2.0 - 2026-10-01

- Added a loopback X5 Control API for VX5 state and MX5 Bridge discovery, rig lists, rig recall, property read/write, and footswitch presses.
- Added a Windows MIDI SysEx bridge client with chunked reply and Unicode handling.
- Added live MX5 rig and property controls to the native app, plus USB Transfer drive detection.
- Documented the bridge firmware prerequisite and MX5 USB discovery process.

## 0.1.0 - 2026-09-27

- Added a versioned Windows portable package and checksum build script.
- Replaced three VX5 top tabs with the five pedal menus and an Advanced view.
- Added key, scale, and vocal range controls with device readback.
- Added named preset browsing, local custom patches, snapshot editing, and live apply.
- Made the Volume, Speed, and Humanize knobs draggable and their sliders send on release.
- Added rotating knob indicators and circular rim dragging, synchronized with sliders and device readback.
- Split the controller into VX5, MX5, preset, MIDI, model, and UI modules.
- Added project and device control documentation.

## Initial prototype

- Added VX5 USB API discovery and live parameter writes.
- Added Windows MIDI output for VX5 and a preliminary MX5 control panel.
