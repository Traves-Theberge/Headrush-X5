using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace X5Control {
  public sealed partial class Controller {
    void ScanMidi() {
      var previousVx = vxMidi.SelectedItem as MidiPort;
      var previousMx = mxMidi == null ? null : mxMidi.SelectedItem as MidiPort;
      vxMidi.Items.Clear(); if (mxMidi != null) mxMidi.Items.Clear();
      for (uint i = 0; i < Midi.midiOutGetNumDevs(); i++) {
        var caps = new Midi.Caps();
        if (Midi.midiOutGetDevCapsW(i, ref caps, (uint)Marshal.SizeOf(typeof(Midi.Caps))) != 0) continue;
        var port = new MidiPort { Id = i, Name = caps.szPname };
        vxMidi.Items.Add(port); if (mxMidi != null) mxMidi.Items.Add(port);
        if (previousVx != null && previousVx.Name == port.Name) vxMidi.SelectedItem = port;
        if (previousMx != null && previousMx.Name == port.Name && mxMidi != null) mxMidi.SelectedItem = port;
      }
      if (vxMidi.SelectedItem == null) foreach (MidiPort port in vxMidi.Items) if (port.Name.IndexOf("VX5", StringComparison.OrdinalIgnoreCase) >= 0) { vxMidi.SelectedItem = port; break; }
      window.Dispatcher.BeginInvoke(new Action(delegate { Find<ScrollViewer>("VxActionsScroll").ScrollToTop(); }), DispatcherPriority.Loaded);
      Status(vxMidi.Items.Count + " MIDI output ports found");
    }
    bool SendVx(int status, int data1, int data2) { return Send(vxMidi, status, data1, data2, "VX5"); }
    bool SendMx(int status, int data1, int data2) { return Send(mxMidi, status, data1, data2, "MX5"); }
    bool Send(ComboBox picker, int status, int data1, int data2, string device) {
      var port = picker.SelectedItem as MidiPort;
      if (port == null) { Status("Choose a " + device + " MIDI output first"); return false; }
      IntPtr handle;
      var code = Midi.midiOutOpen(out handle, port.Id, IntPtr.Zero, IntPtr.Zero, 0);
      if (code != 0) { Status("Cannot open MIDI output " + port.Name + " (error " + code + ")"); return false; }
      try {
        uint message = (uint)(status | (data1 << 8) | (data2 << 16));
        code = Midi.midiOutShortMsg(handle, message);
        Status(code == 0 ? device + " MIDI command sent to " + port.Name : "MIDI send failed (error " + code + ")");
        return code == 0;
      } finally { Midi.midiOutClose(handle); }
    }
  }
}
