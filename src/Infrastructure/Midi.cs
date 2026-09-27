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
  public static class Midi {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct Caps {
      public ushort wMid, wPid; public uint vDriverVersion;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
      public ushort wTechnology, wVoices, wNotes, wChannelMask; public uint dwSupport;
    }
    [DllImport("winmm.dll")] public static extern uint midiOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiOutGetDevCapsW")]
    public static extern uint midiOutGetDevCapsW(uint id, ref Caps caps, uint size);
    [DllImport("winmm.dll")] public static extern uint midiOutOpen(out IntPtr handle, uint id, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] public static extern uint midiOutShortMsg(IntPtr handle, uint message);
    [DllImport("winmm.dll")] public static extern uint midiOutClose(IntPtr handle);
  }
}
