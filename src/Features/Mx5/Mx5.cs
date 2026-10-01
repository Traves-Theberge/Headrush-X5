using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
    TextBlock mxUsbStatus;
    TextBlock mxLiveStatus;
    ListBox mxRigList;
    Button mxLoadRigButton;
    bool mxRigLive;
    TextBox mxPropertyPath, mxPropertyField, mxPropertyValue;
    readonly List<Dictionary<string, object>> mxRigs = new List<Dictionary<string, object>>();
    Border MxCard(string title, string description) {
      var border = new Border { Width = 330, Background = BrushOf("#19242D"), BorderBrush = BrushOf("#30434D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(18), Margin = new Thickness(0, 0, 14, 14) };
      var stack = new StackPanel(); border.Child = stack;
      stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold });
      stack.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 15) });
      Find<WrapPanel>("MxCards").Children.Add(border); return border;
    }
    void BuildMxPage() {
      var live = (StackPanel)MxCard("MX5 LIVE USB", "Full rig and parameter control through the local X5 API and MX5 Bridge firmware.").Child;
      mxLiveStatus = new TextBlock { Text = "Start the X5 API, then scan the MX5.", Foreground = muted, TextWrapping = TextWrapping.Wrap };
      live.Children.Add(mxLiveStatus);
      var liveScan = new Button { Content = "Connect and scan", Margin = new Thickness(0, 12, 0, 0) };
      liveScan.Click += async delegate { await RefreshMxLive(); }; live.Children.Add(liveScan);
      for (int i = 1; i <= 3; i++) {
        int number = i;
        var foot = new Button { Content = "Press live footswitch " + i, Margin = new Thickness(0, 6, 0, 0) };
        foot.Click += async delegate {
          try {
            await Task.Run(() => MxApi("/api/v1/mx5/footswitches/" + number + "/press", "POST", null));
            Status("MX5 footswitch " + number + " pressed");
          } catch (Exception ex) { Status("MX5 footswitch failed: " + Message(ex)); }
        };
        live.Children.Add(foot);
      }
      var rigsCard = (StackPanel)MxCard("LIVE RIG LIBRARY", "Names read directly from the MX5. Select a rig and load it on the pedal.").Child;
      mxRigList = new ListBox { Height = 195, Background = BrushOf("#101820"), Foreground = BrushOf("#F2F3F5") };
      rigsCard.Children.Add(mxRigList);
      mxLoadRigButton = new Button { Content = "Load selected rig", Background = BrushOf("#216B56"), Margin = new Thickness(0, 10, 0, 0), IsEnabled = false };
      mxLoadRigButton.Click += async delegate { await LoadMxRig(); }; rigsCard.Children.Add(mxLoadRigButton);
      var propertyCard = (StackPanel)MxCard("LIVE PARAMETER", "Read and edit a bridge property path, including effect block settings.").Child;
      mxPropertyPath = new TextBox { Text = "/Engine/Patch/Amp/Bass" }; propertyCard.Children.Add(mxPropertyPath);
      mxPropertyField = new TextBox { Text = "unnormalized", Margin = new Thickness(0, 7, 0, 0) }; propertyCard.Children.Add(mxPropertyField);
      mxPropertyValue = new TextBox { Margin = new Thickness(0, 7, 0, 0) }; propertyCard.Children.Add(mxPropertyValue);
      var readProperty = new Button { Content = "Read property", Margin = new Thickness(0, 10, 6, 0) };
      readProperty.Click += async delegate { await ReadMxProperty(); }; propertyCard.Children.Add(readProperty);
      var writeProperty = new Button { Content = "Set property", Background = BrushOf("#216B56"), Margin = new Thickness(0, 6, 0, 0) };
      writeProperty.Click += async delegate { await WriteMxProperty(); }; propertyCard.Children.Add(writeProperty);
      var usbCard = (StackPanel)MxCard("USB TRANSFER", "The stock MX5 exposes rig files as a drive only after Global Settings > USB Transfer. This scan reads files and makes no changes.").Child;
      mxUsbStatus = new TextBlock { Text = "Checking for a HeadRush drive...", Foreground = muted, TextWrapping = TextWrapping.Wrap };
      usbCard.Children.Add(mxUsbStatus);
      var usbScan = new Button { Content = "Scan USB drive", Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
      usbScan.Click += delegate { ScanMxUsb(); }; usbCard.Children.Add(usbScan);
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
    object MxApi(string route, string method, object body) {
      var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:8765" + route);
      req.Method = method; req.Timeout = 30000; req.ReadWriteTimeout = 30000;
      if (body != null) {
        var data = Encoding.UTF8.GetBytes(json.Serialize(body));
        req.ContentType = "application/json"; req.ContentLength = data.Length;
        using (var stream = req.GetRequestStream()) stream.Write(data, 0, data.Length);
      }
      using (var response = (HttpWebResponse)req.GetResponse())
      using (var reader = new StreamReader(response.GetResponseStream())) return json.DeserializeObject(reader.ReadToEnd());
    }
    async Task RefreshMxLive() {
      try {
        mxLiveStatus.Text = "Scanning MX5 Bridge...";
        var status = Dict(await Task.Run(() => MxApi("/api/v1/mx5/status", "GET", null)));
        if (!Convert.ToBoolean(status["connected"])) {
          mxRigLive = false; mxLoadRigButton.IsEnabled = false;
          mxRigList.Items.Clear(); mxRigs.Clear();
          var drives = status["transferDrives"] as object[];
          if (drives != null && drives.Length > 0) {
            var transfer = Dict(await Task.Run(() => MxApi("/api/v1/mx5/transfer/rigs", "GET", null)));
            foreach (object item in (object[])transfer["rigs"]) {
              var row = Dict(item); if (row == null) continue;
              mxRigs.Add(row); mxRigList.Items.Add(Convert.ToString(row["name"]));
            }
            mxLiveStatus.Text = "USB Transfer connected: " + mxRigs.Count + " rig files. Eject and Sync on the MX5 when finished. Live controls require MX5 Bridge firmware.";
          } else mxLiveStatus.Text = "No MX5 Bridge MIDI port or USB Transfer drive found.";
          return;
        }
        mxRigLive = true; mxLoadRigButton.IsEnabled = true;
        mxLiveStatus.Text = "Connected: " + Convert.ToString(status["bridgeVersion"]);
        var result = Dict(await Task.Run(() => MxApi("/api/v1/mx5/rigs", "GET", null)));
        mxRigs.Clear(); mxRigList.Items.Clear();
        foreach (object item in (object[])result["rigs"]) {
          var row = Dict(item); if (row == null) continue;
          mxRigs.Add(row);
          mxRigList.Items.Add(Convert.ToString(row["name"]));
        }
        Status("MX5: " + mxRigs.Count + " live rigs loaded");
      } catch (Exception ex) { mxLiveStatus.Text = "X5 API unavailable: " + Message(ex) + " Run run-service.ps1."; }
    }
    async Task LoadMxRig() {
      if (!mxRigLive) { Status("USB Transfer lists files only; live rig loading requires MX5 Bridge"); return; }
      int index = mxRigList.SelectedIndex;
      if (index < 0 || index >= mxRigs.Count) { Status("Select an MX5 rig first"); return; }
      try {
        int program = Convert.ToInt32(mxRigs[index]["prog_num"], CultureInfo.InvariantCulture) + 1;
        await Task.Run(() => MxApi("/api/v1/mx5/rigs/load", "POST", new { program = program }));
        Status("MX5 rig loaded: " + Convert.ToString(mxRigs[index]["name"]));
      } catch (Exception ex) { Status("MX5 load failed: " + Message(ex)); }
    }
    async Task ReadMxProperty() {
      try {
        string path = mxPropertyPath.Text;
        var result = Dict(await Task.Run(() => MxApi("/api/v1/mx5/properties?path=" + Uri.EscapeDataString(path), "GET", null)));
        var property = Dict(result["property"]);
        string field = mxPropertyField.Text;
        mxPropertyValue.Text = property != null && property.ContainsKey(field) ? Convert.ToString(property[field], CultureInfo.InvariantCulture) : json.Serialize(property);
        Status("MX5 property read: " + path);
      } catch (Exception ex) { Status("MX5 read failed: " + Message(ex)); }
    }
    async Task WriteMxProperty() {
      try {
        string path = mxPropertyPath.Text, field = mxPropertyField.Text, value = mxPropertyValue.Text;
        await Task.Run(() => MxApi("/api/v1/mx5/properties", "PUT", new { path = path, field = field, value = value }));
        Status("MX5 property set: " + path);
        await ReadMxProperty();
      } catch (Exception ex) { Status("MX5 write failed: " + Message(ex)); }
    }
    void ScanMxUsb() {
      foreach (var drive in DriveInfo.GetDrives()) {
        try {
          if (!drive.IsReady) continue;
          string rigsPath = Path.Combine(drive.RootDirectory.FullName, "Rigs");
          if (drive.VolumeLabel.IndexOf("HeadRush", StringComparison.OrdinalIgnoreCase) < 0 && !Directory.Exists(rigsPath)) continue;
          int count = Directory.Exists(rigsPath) ? Directory.GetFiles(rigsPath, "*.rig", SearchOption.AllDirectories).Length : 0;
          mxUsbStatus.Text = "HeadRush drive " + drive.Name + " found. " + count + " rig files visible (read only).";
          Status("MX5 USB Transfer drive found at " + drive.Name);
          return;
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
      }
      mxUsbStatus.Text = "No HeadRush transfer drive. Power on the MX5, check the USB cable, then select Global Settings > USB Transfer on the pedal.";
      Status("No MX5 USB Transfer drive detected");
    }
  }
}
