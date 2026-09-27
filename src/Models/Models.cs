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
  public sealed class PresetRecord {
    public int Number { get; set; }
    public string Name { get; set; }
    public string Source { get; set; }
    public Dictionary<string, object> State { get; set; }
  }
  public sealed class PresetListItem {
    public int Number;
    public string Name;
    public override string ToString() { return Number.ToString("000", CultureInfo.InvariantCulture) + "   " + Name; }
  }
  public sealed class MidiPort { public uint Id; public string Name; public override string ToString() { return Name + "  (#" + Id + ")"; } }
}
