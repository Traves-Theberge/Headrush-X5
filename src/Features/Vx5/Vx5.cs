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
    void RequestAb(int mode) {
      if (!SendVx(0xB0, 16, mode)) return;
      requestedAb = mode;
      Find<Button>("StageAb").Content = mode == 0 ? "◉" : mode == 1 ? "A" : "B";
      Find<Button>("StageAb").BorderBrush = BrushOf(mode == 0 ? "#AEB8BE" : "#67DDB4");
      Status("VX5 A/B mode " + (mode == 0 ? "off" : mode == 1 ? "A" : "B") + " requested");
    }
    void RequestTalk(bool on) {
      if (!SendVx(0xB0, 17, on ? 127 : 0)) return;
      requestedTalk = on;
      Find<Button>("StageTalk").BorderBrush = BrushOf(on ? "#FF8A73" : "#AEB8BE");
      Status("VX5 Talk mode " + (on ? "on" : "off") + " requested");
    }
    static Brush BrushOf(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }

    object Request(string path, string method, object body) {
      var req = (HttpWebRequest)WebRequest.Create(Base + path);
      req.Method = method; req.Timeout = 3500; req.ReadWriteTimeout = 3500;
      if (body != null) {
        var bytes = Encoding.UTF8.GetBytes(json.Serialize(body));
        req.ContentType = "application/json"; req.ContentLength = bytes.Length;
        using (var stream = req.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
      }
      using (var response = (HttpWebResponse)req.GetResponse())
      using (var reader = new StreamReader(response.GetResponseStream())) {
        var text = reader.ReadToEnd();
        return text.Length == 0 ? null : json.DeserializeObject(text);
      }
    }
    static Dictionary<string, object> Dict(object value) { return value as Dictionary<string, object>; }
    static string Message(Exception ex) {
      var web = ex as WebException;
      if (web != null && web.Response != null) return web.Message + " (HTTP " + ((HttpWebResponse)web.Response).StatusCode + ")";
      return ex.Message;
    }
    void RefreshVx() {
      try {
        var tree = Dict(Request("/subtree", "GET", null));
        var stateNode = Dict(tree[StatePath]);
        metadata = Dict(Dict(stateNode["meta"])["properties"]);
        values = Dict(Request("/object-properties" + StatePath, "GET", null));
        var info = Dict(Request("/object-properties/AutoTune/info", "GET", null));
        Find<TextBlock>("VxConnection").Text = "Connected · live parameter control";
        Find<TextBlock>("VxVersion").Text = "Firmware " + Convert.ToString(info["version"]) + "  ·  " + metadata.Count + " parameters available";
        Find<TextBlock>("SideStatus").Text = "VX5 · " + Convert.ToString(info["version"]);
        Find<TextBlock>("ParameterCount").Text = metadata.Count + " parameters";
        BuildParameters(); SyncPedal(); Status("VX5 state refreshed");
      } catch (Exception ex) {
        Find<TextBlock>("VxConnection").Text = "VX5 API unavailable";
        Find<TextBlock>("VxVersion").Text = Message(ex);
        Find<TextBlock>("SideStatus").Text = "VX5 API unavailable";
        Find<StackPanel>("ParameterPanel").Children.Clear();
        Find<TextBlock>("ParameterCount").Text = "";
        Status("Connect and power on the VX5 by USB, then refresh");
      }
    }
    string Group(string name) {
      if (name.StartsWith("Auto-Tune") || name == "Humanise Amount" || name == "Retune Speed" || name == "Key" || name == "Scale" || name == "Range") return "AUTO-TUNE";
      if (name.StartsWith("Fx ") || name == "Volume") return "MASTER";
      return name.Split(' ')[0].ToUpperInvariant();
    }
    void BuildParameters() {
      var panel = Find<StackPanel>("ParameterPanel"); panel.Children.Clear();
      if (metadata == null || EditedValues() == null) return;
      var groups = new SortedDictionary<string, List<string>>();
      foreach (var name in metadata.Keys) {
        var group = Group(name);
        if (!editingStoredPreset && activeMenu != "ADVANCED" && activeMenu != "STAGE" && ((activeMenu == "AUTO-TUNE" && group != "AUTO-TUNE") || (activeMenu == "HARMONY" && group != "HARMONY") || (activeMenu == "FX" && group != "MASTER" && group != "CHORUS" && group != "COMPRESSOR" && group != "DELAY" && group != "FLAVOR" && group != "REVERB"))) continue;
        if (!groups.ContainsKey(group)) groups[group] = new List<string>();
        groups[group].Add(name);
      }
      foreach (var pair in groups) {
        pair.Value.Sort(StringComparer.OrdinalIgnoreCase);
        var card = new Border { Background = BrushOf("#1B2730"), BorderBrush = BrushOf("#30434D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12) };
        var content = new StackPanel(); card.Child = content;
        content.Children.Add(new TextBlock { Text = pair.Key, Foreground = accent, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var name in pair.Value) AddParameter(content, name);
        panel.Children.Add(card);
      }
    }
    void AddParameter(StackPanel parent, string name) {
      var spec = Dict(metadata[name]);
      var editValues = EditedValues();
      var type = Convert.ToString(spec["type"]);
      var row = new Grid { Margin = new Thickness(0, 4, 0, 8) };
      row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
      row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
      var label = new TextBlock { Text = name, Foreground = BrushOf("#D6E3E4"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
      Grid.SetColumn(label, 0); row.Children.Add(label);
      if (type == "boolean") {
        var toggle = new CheckBox { IsChecked = Convert.ToBoolean(editValues[name]), VerticalAlignment = VerticalAlignment.Center, Foreground = accent, Content = Convert.ToBoolean(editValues[name]) ? "On" : "Off", Cursor = Cursors.Hand };
        toggle.Click += delegate { var next = toggle.IsChecked == true; CommitEditedValue(name, next); var actual = Convert.ToBoolean(EditedValues()[name]); toggle.IsChecked = actual; toggle.Content = actual ? "On" : "Off"; };
        Grid.SetColumn(toggle, 1); row.Children.Add(toggle);
      } else {
        var stack = new Grid(); stack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); stack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) }); stack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        double min = Convert.ToDouble(spec["minimum"], CultureInfo.InvariantCulture), max = Convert.ToDouble(spec["maximum"], CultureInfo.InvariantCulture);
        double current = Convert.ToDouble(editValues[name], CultureInfo.InvariantCulture);
        var slider = new Slider { Minimum = min, Maximum = max, Value = current, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = type == "integer", TickFrequency = type == "integer" ? 1 : (max - min) / 100.0 };
        var edit = new TextBox { Text = Format(current, type), FontSize = 12, Margin = new Thickness(0, 0, 5, 0), HorizontalContentAlignment = HorizontalAlignment.Right, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(3) };
        var apply = new Button { Content = "Set", Padding = new Thickness(5, 5, 5, 5), FontSize = 11, Background = BrushOf("#31594F") };
        slider.ValueChanged += delegate { edit.Text = Format(slider.Value, type); };
        Action submit = delegate {
          double n;
          if (!double.TryParse(edit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out n) || n < min || n > max || (type == "integer" && n != Math.Truncate(n))) { Status(name + ": enter a value from " + min + " to " + max); edit.Text = Format(Convert.ToDouble(EditedValues()[name], CultureInfo.InvariantCulture), type); return; }
          var candidate = type == "integer" ? (object)(int)n : (object)n;
          CommitEditedValue(name, candidate);
          var actual = Convert.ToDouble(EditedValues()[name], CultureInfo.InvariantCulture);
          slider.Value = actual;
          edit.Text = Format(actual, type);
        };
        apply.Click += delegate { submit(); };
        edit.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { submit(); e.Handled = true; } };
        Grid.SetColumn(slider, 0); Grid.SetColumn(edit, 1); Grid.SetColumn(apply, 2);
        stack.Children.Add(slider); stack.Children.Add(edit); stack.Children.Add(apply);
        Grid.SetColumn(stack, 1); row.Children.Add(stack);
      }
      parent.Children.Add(row);
    }
    Dictionary<string, object> EditedValues() { return editingStoredPreset && selectedPreset != null ? selectedPreset.State : values; }
    void CommitEditedValue(string name, object candidate) {
      if (editingStoredPreset && selectedPreset != null && selectedPreset.State != null) {
        selectedPreset.State[name] = candidate;
        SavePresetLibrary();
        Status("Saved " + name + " in preset " + selectedPreset.Number + " PC snapshot");
      } else WriteValue(name, candidate);
    }
    static string Format(double n, string type) { return type == "integer" ? Math.Round(n).ToString(CultureInfo.InvariantCulture) : n.ToString("0.###", CultureInfo.InvariantCulture); }
    void BindPedalSlider(string property, string sliderName, string valueName, string buttonName) {
      var slider = Find<Slider>(sliderName);
      slider.ValueChanged += delegate { Find<TextBlock>(valueName).Text = slider.Value.ToString("0.###", CultureInfo.InvariantCulture); };
      Bind(buttonName, delegate { if (values == null) { Status("Connect the VX5 first"); return; } WriteValue(property, Math.Round(slider.Value, 3)); SyncPedal(); });
      slider.PreviewMouseLeftButtonUp += delegate { if (values != null) WriteValue(property, Math.Round(slider.Value, 3)); };
    }
    void BindKnob(string knobName, string sliderName, string property) {
      var knob = Find<Border>(knobName);
      var slider = Find<Slider>(sliderName);
      var dial = Find<Grid>(knobName.Replace("Knob", "Dial"));
      var rotation = new RotateTransform();
      dial.RenderTransform = rotation;
      Action updateAngle = delegate {
        double fraction = (slider.Value - slider.Minimum) / (slider.Maximum - slider.Minimum);
        rotation.Angle = -135 + 270 * Math.Max(0, Math.Min(1, fraction));
      };
      slider.ValueChanged += delegate { updateAngle(); };
      updateAngle();
      knob.Cursor = Cursors.Hand;
      knob.ToolTip = "Twist around the rim or drag vertically to change " + property;
      double startY = 0, startValue = 0;
      bool circularDrag = false;
      Action<Point> setFromAngle = delegate(Point point) {
        double dx = point.X - knob.ActualWidth / 2, dy = point.Y - knob.ActualHeight / 2;
        double angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        slider.Value = slider.Minimum + (Math.Max(-135, Math.Min(135, angle)) + 135) / 270.0 * (slider.Maximum - slider.Minimum);
      };
      knob.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
        startY = e.GetPosition(window).Y;
        startValue = slider.Value;
        var point = e.GetPosition(knob);
        double dx = point.X - knob.ActualWidth / 2, dy = point.Y - knob.ActualHeight / 2;
        circularDrag = Math.Sqrt(dx * dx + dy * dy) > knob.ActualWidth * 0.24;
        if (circularDrag) setFromAngle(point);
        knob.CaptureMouse();
        e.Handled = true;
      };
      knob.MouseMove += delegate(object sender, MouseEventArgs e) {
        if (!knob.IsMouseCaptured) return;
        if (circularDrag) {
          setFromAngle(e.GetPosition(knob));
        } else {
          double travel = startY - e.GetPosition(window).Y;
          slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, startValue + travel / 160.0 * (slider.Maximum - slider.Minimum)));
        }
      };
      knob.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) {
        if (!knob.IsMouseCaptured) return;
        knob.ReleaseMouseCapture();
        if (values != null) WriteValue(property, Math.Round(slider.Value, 3));
        e.Handled = true;
      };
    }
    void SyncPedal() {
      if (values == null) return;
      Find<Slider>("PedalVolumeSlider").Value = Convert.ToDouble(values["Volume"], CultureInfo.InvariantCulture);
      Find<Slider>("PedalSpeedSlider").Value = Convert.ToDouble(values["Retune Speed"], CultureInfo.InvariantCulture);
      Find<Slider>("PedalHumanizeSlider").Value = Convert.ToDouble(values["Humanise Amount"], CultureInfo.InvariantCulture);
      Find<TextBlock>("PedalVolumeValue").Text = Format(Convert.ToDouble(values["Volume"], CultureInfo.InvariantCulture), "number");
      Find<TextBlock>("PedalSpeedValue").Text = Format(Convert.ToDouble(values["Retune Speed"], CultureInfo.InvariantCulture), "number");
      Find<TextBlock>("PedalHumanizeValue").Text = Format(Convert.ToDouble(values["Humanise Amount"], CultureInfo.InvariantCulture), "number");
      int keyIndex = Convert.ToInt32(values["Key"], CultureInfo.InvariantCulture);
      int scaleIndex = Convert.ToInt32(values["Scale"], CultureInfo.InvariantCulture);
      Find<ComboBox>("PedalKeyCombo").SelectedIndex = keyIndex;
      Find<ComboBox>("PedalScaleCombo").SelectedIndex = scaleIndex;
      Find<ComboBox>("MenuKeyCombo").SelectedIndex = keyIndex;
      Find<ComboBox>("MenuScaleCombo").SelectedIndex = scaleIndex;
      int rangeIndex = Convert.ToInt32(values["Range"], CultureInfo.InvariantCulture);
      if (rangeIndex >= 0 && rangeIndex < Ranges.Length) Find<ComboBox>("MenuRangeCombo").SelectedIndex = rangeIndex;
      Find<TextBlock>("MenuKeyReadback").Text = "VX5 NOW: " + Keys[keyIndex] + " / " + Scales[scaleIndex] + " / " + (rangeIndex >= 0 && rangeIndex < Ranges.Length ? Ranges[rangeIndex] : "Unknown range");
      Find<TextBlock>("PedalKeyDisplay").Text = "KEY " + Keys[keyIndex] + "  ·  " + Scales[scaleIndex];
      SetPedalToggle("PedalAutoTune", "Auto-Tune Enable");
      SetPedalToggle("PedalHarmony", "Harmony Enable");
      SetPedalToggle("PedalFx", "Fx Enable");
    }
    void SetKeyScale() {
      int key = Find<ComboBox>("PedalKeyCombo").SelectedIndex;
      int scale = Find<ComboBox>("PedalScaleCombo").SelectedIndex;
      if (values == null || key < 0 || scale < 0) { Status("Connect the VX5 and choose a key and scale"); return; }
      try {
        Request("/object-properties" + StatePath, "PUT", new Dictionary<string, object> { { "Key", key }, { "Scale", scale } });
        values = Dict(Request("/object-properties" + StatePath, "GET", null));
        SyncPedal();
        Status("VX5 key " + Keys[Convert.ToInt32(values["Key"])] + ", scale " + Scales[Convert.ToInt32(values["Scale"])] + " applied");
      } catch (Exception ex) { Status("Could not set key/scale: " + Message(ex)); }
    }
    void SetKeyScaleRange() {
      int key = Find<ComboBox>("MenuKeyCombo").SelectedIndex;
      int scale = Find<ComboBox>("MenuScaleCombo").SelectedIndex;
      int range = Find<ComboBox>("MenuRangeCombo").SelectedIndex;
      if (values == null || key < 0 || scale < 0 || range < 0) { Status("Connect the VX5 and choose a key, scale, and range"); return; }
      try {
        Request("/object-properties" + StatePath, "PUT", new Dictionary<string, object> { { "Key", key }, { "Scale", scale }, { "Range", range } });
        values = Dict(Request("/object-properties" + StatePath, "GET", null));
        SyncPedal();
        Status("VX5 key, scale, and range applied");
      } catch (Exception ex) { Status("Could not set key/scale/range: " + Message(ex)); }
    }
    void SetPedalToggle(string button, string property) { Find<Button>(button).Background = BrushOf(Convert.ToBoolean(values[property]) ? "#236F5D" : "#28323B"); }
    void TogglePedal(string property) {
      if (values == null) { Status("Connect the VX5 first"); return; }
      WriteValue(property, !Convert.ToBoolean(values[property])); SyncPedal();
    }
    void StepPreset(int delta) {
      int current;
      if (!ParseInt(Find<TextBox>("VxPreset"), 1, 250, out current)) return;
      int next = Math.Max(1, Math.Min(250, current + delta));
      RecallVx(next);
    }
    void RecallVx(int number) {
      if (vxMidi.SelectedItem == null) { Status("Choose the VX5 MIDI output first"); return; }
      if (!SendVx(0xB0, 32, number <= 128 ? 0 : 1)) return;
      if (!SendVx(0xC0, (number - 1) % 128, 0)) return;
      Find<TextBox>("VxPreset").Text = number.ToString(CultureInfo.InvariantCulture);
      Find<TextBlock>("PedalPresetDisplay").Text = number.ToString("00", CultureInfo.InvariantCulture);
      Find<TextBlock>("PedalPresetName").Text = presets[number].Name;
      lastRequestedPreset = number;
      Status("VX5 preset " + number + " requested via MIDI");
    }
    bool WriteValue(string name, object candidate) {
      try {
        Request("/object-properties" + StatePath, "PUT", new Dictionary<string, object> { { name, candidate } });
        var fresh = Dict(Request("/object-properties" + StatePath, "GET", null));
        values = fresh; SyncPedal();
        bool matches = Convert.ToString(fresh[name], CultureInfo.InvariantCulture) == Convert.ToString(candidate, CultureInfo.InvariantCulture);
        Status(name + " = " + Convert.ToString(fresh[name], CultureInfo.InvariantCulture) + (matches ? "" : "  ·  device adjusted the value"));
        return matches;
      } catch (Exception ex) { Status("Could not set " + name + ": " + Message(ex)); return false; }
    }
    static bool ParseInt(TextBox box, int min, int max, out int number) {
      if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number >= min && number <= max) return true;
      MessageBox.Show("Enter a whole number from " + min + " to " + max + ".", "X5 Control", MessageBoxButton.OK, MessageBoxImage.Information); return false;
    }
  }
}
