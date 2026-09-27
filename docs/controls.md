# Controls and verification

## VX5

| Control | Transport | Current behavior |
| --- | --- | --- |
| Volume, Speed, Humanize knobs and sliders | VX5 USB API | Send on release or Set; GET confirms the value |
| Key, Scale, Range | VX5 USB API | Send together from Key menu; GET confirms state |
| Auto-Tune, Harmony, FX parameters | VX5 USB API | Metadata driven edit and readback |
| Preset load, A/B, Talk | Windows MIDI | Send the VX5 guide's bank, program, and CC commands |
| Custom patch | Local JSON plus VX5 USB API | Copy current state, edit locally, apply to loaded slot |

The connected VX5 has firmware 1.3.1 and reported 70 state properties during initial inspection. A Chorus Depth edit from 0.50 to 0.51 was read back and restored. The USB API is undocumented, so behavior may change with firmware. MIDI commands are sent but preset recall and permanent save have not been verified through device state.

The Humanize knob was dragged in the running window. The device readback changed from 0 to 20.625; the original 0 value was then restored and read back. This verifies the knob's drag path and HTTP write on the connected VX5.

## MX5

MIDI controls are implemented from the user guide. Connect the MX5 before treating their operation as verified. The unit's external MIDI input is 3.5 mm; USB-B is not used by this app for MX5 MIDI control.
