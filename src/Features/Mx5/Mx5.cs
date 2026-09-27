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
    Border MxCard(string title, string description) {
      var border = new Border { Width = 330, Background = BrushOf("#19242D"), BorderBrush = BrushOf("#30434D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(18), Margin = new Thickness(0, 0, 14, 14) };
      var stack = new StackPanel(); border.Child = stack;
      stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold });
      stack.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 15) });
      Find<WrapPanel>("MxCards").Children.Add(border); return border;
    }
    void BuildMxPage() {
      var portCard = (StackPanel)MxCard("MIDI CONNECTION", "Choose the USB MIDI interface connected to the MX5 MIDI input.").Child;
      mxMidi = new ComboBox(); portCard.Children.Add(mxMidi);
      var scan = new Button { Content = "Scan ports", Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left }; scan.Click += delegate { ScanMidi(); }; portCard.Children.Add(scan);
      var preset = (StackPanel)MxCard("RIG RECALL", "Send a MIDI Program Change to the MX5. Set Program Change to Receive on the pedal.").Child;
      var rig = new TextBox { Text = "0", Width = 80, HorizontalAlignment = HorizontalAlignment.Left }; preset.Children.Add(rig);
      var recall = new Button { Content = "Send program change", Background = BrushOf("#54CAA9"), Foreground = BrushOf("#092820"), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
      recall.Click += delegate { int n; if (ParseInt(rig, 0, 127, out n)) SendMx(0xC0, n, 0); }; preset.Children.Add(recall);
      var switches = (StackPanel)MxCard("FOOTSWITCHES", "Remote presses for the three MX5 footswitches.").Child;
      for (int i = 1; i <= 3; i++) { int cc = 49 + i; var b = new Button { Content = "Footswitch " + i, Margin = new Thickness(0, 0, 0, 8) }; b.Click += delegate { SendMx(0xB0, cc, 127); }; switches.Children.Add(b); }
      var blocks = (StackPanel)MxCard("EFFECT BLOCKS", "Toggle the loaded rig's effect blocks through CC 75–85.").Child;
      var blockGrid = new UniformGrid { Columns = 4 };
      for (int i = 1; i <= 11; i++) { int cc = 74 + i; var b = new Button { Content = i.ToString(), Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(5) }; b.Click += delegate { SendMx(0xB0, cc, 127); }; blockGrid.Children.Add(b); } blocks.Children.Add(blockGrid);
      var looper = (StackPanel)MxCard("LOOPER & TEMPO", "Documented MX5 MIDI actions.").Child;
      var actions = new Dictionary<string, int> { { "Tap tempo", 64 }, { "Half speed", 65 }, { "Double speed", 66 }, { "Half loop", 67 }, { "Double loop", 68 }, { "Start / stop", 69 }, { "Record", 70 }, { "Insert", 71 }, { "Peel", 72 }, { "Mute", 73 }, { "Reverse", 74 } };
      foreach (var action in actions) { int cc = action.Value; var b = new Button { Content = action.Key, Margin = new Thickness(0, 0, 0, 6) }; b.Click += delegate { SendMx(0xB0, cc, 127); }; looper.Children.Add(b); }
    }
  }
}
