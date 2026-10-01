using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace X5Control.Mx5Bridge {
  internal static class Program {
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };

    static int Main(string[] args) {
      try {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length == 0) throw new ArgumentException("Expected a command.");
        object result;
        if (args[0] == "selftest") result = SelfTest();
        else if (args[0] == "ports") result = MidiSession.Ports();
        else if (args[0] == "drives") result = Drives();
        else {
          using (var bridge = new MidiSession()) {
            string version = bridge.Ping();
            if (!version.StartsWith("MX5Bridge-", StringComparison.Ordinal)) throw new IOException("The MIDI port did not answer as MX5 Bridge.");
            if (args[0] == "ping") result = new { bridge = version };
            else if (args[0] == "get" && args.Length == 2) result = bridge.Get(CheckedPath(args[1]));
            else if (args[0] == "set" && args.Length == 4) result = bridge.Set(CheckedPath(args[1]), CheckedField(args[2]), args[3]);
            else if (args[0] == "rigs") result = bridge.Rigs();
            else if (args[0] == "load" && args.Length == 2) {
              int number = int.Parse(args[1], CultureInfo.InvariantCulture);
              if (number < 1 || number > 128) throw new ArgumentException("Program number must be 1 to 128.");
              result = bridge.Set("/Engine/PresetCtrl/Rigs/ReceivePresetIndex", "unnormalized", (number - 1).ToString(CultureInfo.InvariantCulture));
            } else if (args[0] == "footswitch" && args.Length == 2) {
              int number = int.Parse(args[1], CultureInfo.InvariantCulture);
              if (number < 1 || number > 3) throw new ArgumentException("Footswitch must be 1 to 3.");
              string path = "/Engine/RedirCtrl/RawFootswitches/FS" + number;
              bridge.Set(path, "state", "1");
              Thread.Sleep(40);
              result = bridge.Set(path, "state", "0");
            } else throw new ArgumentException("Unknown command or incorrect arguments.");
          }
        }
        Console.WriteLine(Json.Serialize(new { ok = true, result = result }));
        return 0;
      } catch (Exception ex) {
        Console.WriteLine(Json.Serialize(new { ok = false, error = ex.Message }));
        return 1;
      }
    }

    static string CheckedPath(string path) {
      if (path == null || path.Length > 256 || !path.StartsWith("/Engine/", StringComparison.Ordinal) || path.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
        throw new ArgumentException("Property path must start with /Engine/ and contain no control characters.");
      return path;
    }
    static string CheckedField(string field) {
      if (Array.IndexOf(new[] { "value", "unnormalized", "state", "index", "string", "user" }, field) < 0)
        throw new ArgumentException("Unsupported property field.");
      return field;
    }
    static object Drives() {
      var found = new List<object>();
      foreach (var drive in DriveInfo.GetDrives()) {
        try {
          if (!drive.IsReady) continue;
          string rigs = Path.Combine(drive.RootDirectory.FullName, "Rigs");
          if (drive.VolumeLabel.IndexOf("HeadRush", StringComparison.OrdinalIgnoreCase) < 0 && !Directory.Exists(rigs)) continue;
          found.Add(new { root = drive.Name, label = drive.VolumeLabel, rigs = Directory.Exists(rigs) ? Directory.GetFiles(rigs, "*.rig", SearchOption.AllDirectories).Length : 0 });
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
      }
      return found;
    }
    static object SelfTest() {
      string source = "Caf\u00E9 \uD83C\uDFB8";
      string encoded = MidiSession.Escape(source);
      if (MidiSession.Unescape(encoded) != source) throw new Exception("Unicode SysEx codec failed.");
      byte[] frame = MidiSession.Frame(7, 0x02, "/Engine/Test");
      if (frame[0] != 0xF0 || frame[1] != 0x7D || frame[4] != 7 || frame[5] != 0x22 || frame[frame.Length - 1] != 0xF7) throw new Exception("SysEx frame failed.");
      return new { codec = "ok", frame = "ok" };
    }
  }

  internal sealed class MidiSession : IDisposable {
    const uint HeaderDone = 0x01;
    const int BufferCount = 24;
    const int BufferSize = 1024;
    readonly IntPtr[] inputHeaders = new IntPtr[BufferCount];
    readonly IntPtr[] inputData = new IntPtr[BufferCount];
    IntPtr input, output;
    byte sequence;

    public MidiSession() {
      uint inputId = FindInput(), outputId = FindOutput();
      Check(WinMidi.midiInOpen(out input, inputId, IntPtr.Zero, IntPtr.Zero, 0), "Open MX5 MIDI input");
      try {
        Check(WinMidi.midiOutOpen(out output, outputId, IntPtr.Zero, IntPtr.Zero, 0), "Open MX5 MIDI output");
        for (int i = 0; i < BufferCount; i++) {
          inputData[i] = Marshal.AllocHGlobal(BufferSize);
          inputHeaders[i] = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinMidi.MidiHeader)));
          Marshal.StructureToPtr(new WinMidi.MidiHeader { lpData = inputData[i], dwBufferLength = BufferSize }, inputHeaders[i], false);
          Check(WinMidi.midiInPrepareHeader(input, inputHeaders[i], (uint)Marshal.SizeOf(typeof(WinMidi.MidiHeader))), "Prepare MX5 MIDI input");
          Check(WinMidi.midiInAddBuffer(input, inputHeaders[i], (uint)Marshal.SizeOf(typeof(WinMidi.MidiHeader))), "Queue MX5 MIDI input");
        }
        Check(WinMidi.midiInStart(input), "Start MX5 MIDI input");
      } catch { Dispose(); throw; }
    }

    public static object Ports() {
      var inputs = new List<string>(); var outputs = new List<string>();
      for (uint i = 0; i < WinMidi.midiInGetNumDevs(); i++) { var c = new WinMidi.InCaps(); if (WinMidi.midiInGetDevCapsW(i, ref c, (uint)Marshal.SizeOf(typeof(WinMidi.InCaps))) == 0) inputs.Add(c.szPname); }
      for (uint i = 0; i < WinMidi.midiOutGetNumDevs(); i++) { var c = new WinMidi.OutCaps(); if (WinMidi.midiOutGetDevCapsW(i, ref c, (uint)Marshal.SizeOf(typeof(WinMidi.OutCaps))) == 0) outputs.Add(c.szPname); }
      return new { inputs = inputs, outputs = outputs };
    }
    static uint FindInput() {
      for (uint i = 0; i < WinMidi.midiInGetNumDevs(); i++) { var c = new WinMidi.InCaps(); if (WinMidi.midiInGetDevCapsW(i, ref c, (uint)Marshal.SizeOf(typeof(WinMidi.InCaps))) == 0 && c.szPname.IndexOf("MX5", StringComparison.OrdinalIgnoreCase) >= 0) return i; }
      throw new IOException("No MX5 Bridge MIDI input found. Stock firmware does not expose this port.");
    }
    static uint FindOutput() {
      for (uint i = 0; i < WinMidi.midiOutGetNumDevs(); i++) { var c = new WinMidi.OutCaps(); if (WinMidi.midiOutGetDevCapsW(i, ref c, (uint)Marshal.SizeOf(typeof(WinMidi.OutCaps))) == 0 && c.szPname.IndexOf("MX5", StringComparison.OrdinalIgnoreCase) >= 0) return i; }
      throw new IOException("No MX5 Bridge MIDI output found. Stock firmware does not expose this port.");
    }
    static void Check(uint code, string operation) { if (code != 0) throw new IOException(operation + " failed (WinMM " + code + ")."); }

    public string Ping() { return Request(0x01, "", false, 3000); }
    public object Get(string path) { return ParseInfo(Request(0x02, path, true, 4000)); }
    public object Set(string path, string field, string value) { return ParseInfo(Request(0x03, path + "\t" + field + "\t" + value, true, 4000)); }
    public object Rigs() {
      string reply = Request(0x08, "select id, name, prog_num, color from rigs order by rowid", true, 20000);
      var response = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(reply) as Dictionary<string, object>;
      if (response == null || !Convert.ToBoolean(response["ok"])) throw new IOException("MX5 rig query failed: " + Convert.ToString(response == null ? null : response["err"]));
      return response["rows"];
    }
    static object ParseInfo(string reply) {
      int tab = reply.IndexOf('\t');
      if (tab < 0) throw new IOException("MX5 Bridge returned an invalid property reply.");
      return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(reply.Substring(tab + 1));
    }

    string Request(byte command, string payload, bool longHeader, int timeoutMs) {
      sequence = (byte)(sequence % 127 + 1);
      byte[] frame = Frame(sequence, command, payload, longHeader);
      Send(frame);
      var parts = new Dictionary<int, string>(); int total = -1;
      var watch = Stopwatch.StartNew();
      while (watch.ElapsedMilliseconds < timeoutMs) {
        for (int i = 0; i < BufferCount; i++) {
          var h = (WinMidi.MidiHeader)Marshal.PtrToStructure(inputHeaders[i], typeof(WinMidi.MidiHeader));
          if ((h.dwFlags & HeaderDone) == 0) continue;
          byte[] data = new byte[h.dwBytesRecorded];
          if (data.Length > 0) Marshal.Copy(inputData[i], data, 0, data.Length);
          h.dwBytesRecorded = 0;
          Marshal.StructureToPtr(h, inputHeaders[i], false);
          Check(WinMidi.midiInAddBuffer(input, inputHeaders[i], (uint)Marshal.SizeOf(typeof(WinMidi.MidiHeader))), "Requeue MX5 MIDI input");
          if (data.Length < 8 || data[0] != 0xF0 || data[data.Length - 1] != 0xF7 || data[1] != 0x7D || data[2] != 0x48 || data[3] != 0x52 || data[4] != sequence) continue;
          byte code = data[5]; int header = longHeader ? 10 : 8;
          if (data.Length < header + 1) continue;
          int part = longHeader ? (data[6] << 7) | data[7] : data[6];
          total = longHeader ? (data[8] << 7) | data[9] : data[7];
          if (total < 1 || total > 16383 || part >= total) throw new IOException("MX5 Bridge sent invalid reply numbering.");
          parts[part] = Encoding.ASCII.GetString(data, header, data.Length - header - 1);
          watch.Restart();
          if (parts.Count != total) continue;
          var body = new StringBuilder();
          for (int p = 0; p < total; p++) { string text; if (!parts.TryGetValue(p, out text)) throw new IOException("MX5 Bridge reply is incomplete."); body.Append(text); }
          string answer = Unescape(body.ToString());
          if (code == 0x7F) throw new IOException("MX5 Bridge: " + answer);
          return answer;
        }
        Thread.Sleep(2);
      }
      throw new TimeoutException(parts.Count > 0 ? "MX5 Bridge reply incomplete (" + parts.Count + "/" + total + ")." : "MX5 Bridge did not reply.");
    }
    void Send(byte[] frame) {
      IntPtr data = Marshal.AllocHGlobal(frame.Length), header = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WinMidi.MidiHeader)));
      try {
        Marshal.Copy(frame, 0, data, frame.Length);
        Marshal.StructureToPtr(new WinMidi.MidiHeader { lpData = data, dwBufferLength = (uint)frame.Length }, header, false);
        Check(WinMidi.midiOutPrepareHeader(output, header, (uint)Marshal.SizeOf(typeof(WinMidi.MidiHeader))), "Prepare MX5 MIDI output");
        try {
          Check(WinMidi.midiOutLongMsg(output, header, (uint)Marshal.SizeOf(typeof(WinMidi.MidiHeader))), "Send MX5 SysEx");
          var watch = Stopwatch.StartNew();
          while ((((WinMidi.MidiHeader)Marshal.PtrToStructure(header, typeof(WinMidi.MidiHeader))).dwFlags & HeaderDone) == 0) {
            if (watch.ElapsedMilliseconds > 3000) throw new TimeoutException("MX5 MIDI output timed out.");
            Thread.Sleep(1);
          }
        } finally { WinMidi.midiOutUnprepareHeader(output, header, (uint)Marshal.SizeOf(typeof(WinMidi.MidiHeader))); }
      } finally { Marshal.FreeHGlobal(header); Marshal.FreeHGlobal(data); }
    }

    public static byte[] Frame(byte seq, byte command, string payload) { return Frame(seq, command, payload, true); }
    public static byte[] Frame(byte seq, byte command, string payload, bool longHeader) {
      var bytes = new List<byte> { 0xF0, 0x7D, 0x48, 0x52, seq, (byte)(command | (longHeader ? 0x20 : 0)) };
      foreach (char c in Escape(payload)) bytes.Add((byte)c);
      bytes.Add(0xF7);
      return bytes.ToArray();
    }
    public static string Escape(string text) {
      var result = new StringBuilder();
      foreach (char c in text) {
        if (c == '\t' || c == '\n' || (c >= 0x20 && c <= 0x7E)) result.Append(c);
        else result.Append('\u007F').Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
      }
      return result.ToString();
    }
    public static string Unescape(string text) { return Regex.Replace(text, "\u007F([0-9A-Fa-f]{4})", m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString()); }
    public void Dispose() {
      if (input != IntPtr.Zero) { WinMidi.midiInStop(input); WinMidi.midiInReset(input); }
      for (int i = 0; i < BufferCount; i++) {
        if (inputHeaders[i] != IntPtr.Zero) { if (input != IntPtr.Zero) WinMidi.midiInUnprepareHeader(input, inputHeaders[i], (uint)Marshal.SizeOf(typeof(WinMidi.MidiHeader))); Marshal.FreeHGlobal(inputHeaders[i]); inputHeaders[i] = IntPtr.Zero; }
        if (inputData[i] != IntPtr.Zero) { Marshal.FreeHGlobal(inputData[i]); inputData[i] = IntPtr.Zero; }
      }
      if (input != IntPtr.Zero) { WinMidi.midiInClose(input); input = IntPtr.Zero; }
      if (output != IntPtr.Zero) { WinMidi.midiOutClose(output); output = IntPtr.Zero; }
    }
  }

  internal static class WinMidi {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct InCaps {
      public ushort wMid, wPid; public uint vDriverVersion;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
      public uint dwSupport;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct OutCaps {
      public ushort wMid, wPid; public uint vDriverVersion;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
      public ushort wTechnology, wVoices, wNotes, wChannelMask; public uint dwSupport;
    }
    [StructLayout(LayoutKind.Sequential)] public struct MidiHeader {
      public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags;
      public IntPtr lpNext, reserved; public uint dwOffset;
      public IntPtr r0, r1, r2, r3, r4, r5, r6, r7;
    }
    [DllImport("winmm.dll")] public static extern uint midiInGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiInGetDevCapsW")] public static extern uint midiInGetDevCapsW(uint id, ref InCaps caps, uint size);
    [DllImport("winmm.dll")] public static extern uint midiInOpen(out IntPtr handle, uint id, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] public static extern uint midiInPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] public static extern uint midiInAddBuffer(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] public static extern uint midiInStart(IntPtr handle);
    [DllImport("winmm.dll")] public static extern uint midiInStop(IntPtr handle);
    [DllImport("winmm.dll")] public static extern uint midiInReset(IntPtr handle);
    [DllImport("winmm.dll")] public static extern uint midiInUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] public static extern uint midiInClose(IntPtr handle);
    [DllImport("winmm.dll")] public static extern uint midiOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiOutGetDevCapsW")] public static extern uint midiOutGetDevCapsW(uint id, ref OutCaps caps, uint size);
    [DllImport("winmm.dll")] public static extern uint midiOutOpen(out IntPtr handle, uint id, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] public static extern uint midiOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] public static extern uint midiOutLongMsg(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] public static extern uint midiOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] public static extern uint midiOutClose(IntPtr handle);
  }
}
