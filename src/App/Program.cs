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
  public static class Program {
    [STAThread] public static void Main() {
      var app = new Application();
      app.ShutdownMode = ShutdownMode.OnMainWindowClose;
      var xaml = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MainWindow.xaml"));
      var window = (Window)XamlReader.Parse(xaml);
      new Controller(window);
      app.Run(window);
    }
  }

  public sealed partial class Controller {
    const string Base = "http://vx5.local/api/v1";
    const string StatePath = "/AutoTune/headrush/autotune/state";
    static readonly string[] Keys = { "C", "Db", "D", "Eb", "E", "F", "F#/Gb", "G", "Ab", "A", "Bb", "B" };
    static readonly string[] Scales = { "Chromatic", "Major", "Minor", "Melodic Minor", "Harmonic Minor", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Locrian" };
    static readonly string[] Ranges = { "Very Low", "Low", "Mid", "High", "Very High" };
    // Names 1–99 are transcribed from the HeadRush VX5 User Guide v1.3, appendix 5.1.
    static readonly string[] FactoryNames = ("Hard Tune|Warm Tune|Soft Tune|Mild Tune|Octaves|Fifths|HardVerb1|HardVerb2|HardVerb3|WarmVerb1|WarmVerb2|WarmVerb3|SoftVerb1|SoftVerb2|SoftVerb3|MildVerb1|MildVerb2|MildVerb3|HardSlap|HardEcho|HardPong|WarmSlap|WarmEcho|WarmDub|SoftSlap|SoftEcho|SoftPong|MildSlap|MildEcho|MildTail|Oct Vox|WarmSizzl|Oct LoFi|WarmZing|Oct Radio|HardJuice|WarmJuice|SoftJuice|MildJuice|HardWow|WarmWow|SoftWow|MildWow|Hall|Room|Chamber|Ambient|Church|SlapBack|ShortEcho|LongEcho|PingPong|DubDelay|Rhythmic|Resonator|Chorus|Smooth C|Deep C|Heavy C|Robot C|3HiVerbMj|3HiVerbMn|5HiVerb|OctHiVerb|3LoVerbMj|3LoVerbMn|5LoVerb|OctLoVerb|DblOctVrb|5ths Verb|Sizzle|HardSiz|HarmSiz|LoFi|HardLoFi|HarmLoFi|Tube|HardTube|HarmTube|Zinger|HardZing|HarmZing|Radio|HardRadio|HarmRadio|Mega|HardMega|HarmMega|Phone|HardPhone|HarmPhone|Crazinger|Flosizzle|MinorLoFi|MajorLoFi|RadioGaGa|Tubular|SpookyHrm|MeloMinor").Split('|');
    readonly Window window;
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    readonly Brush muted = new SolidColorBrush(Color.FromRgb(145, 165, 174));
    readonly Brush accent = new SolidColorBrush(Color.FromRgb(123, 238, 204));
    Dictionary<string, object> metadata;
    Dictionary<string, object> values;
    ComboBox vxMidi, mxMidi;
    readonly Dictionary<int, PresetRecord> presets = new Dictionary<int, PresetRecord>();
    readonly string presetFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets.user.json");
    PresetRecord selectedPreset;
    int lastRequestedPreset;
    bool editingStoredPreset;
    string activeMenu = "STAGE";
    int requestedAb;
    bool requestedTalk;

    public Controller(Window w) {
      window = w;
      json.MaxJsonLength = 1024 * 1024;
      vxMidi = Find<ComboBox>("VxMidi");
      LoadPresetLibrary();
      Find<TextBox>("PresetSearch").TextChanged += delegate { RefreshPresetList(); };
      Find<ListBox>("PresetList").SelectionChanged += delegate { SelectPreset(); };
      Bind("PresetSaveName", delegate { SavePresetName(); });
      Bind("PresetLoad", delegate { if (selectedPreset != null) RecallVx(selectedPreset.Number); });
      Bind("PresetCapture", delegate { CapturePreset(); });
      Bind("PresetCreate", delegate { CreateCustomPatch(); });
      Bind("PresetEdit", delegate { EditSavedPreset(); });
      Bind("PresetApply", delegate { ApplyPreset(); });
      foreach (var key in Keys) Find<ComboBox>("PedalKeyCombo").Items.Add(key);
      foreach (var scale in Scales) Find<ComboBox>("PedalScaleCombo").Items.Add(scale);
      foreach (var key in Keys) Find<ComboBox>("MenuKeyCombo").Items.Add(key);
      foreach (var scale in Scales) Find<ComboBox>("MenuScaleCombo").Items.Add(scale);
      foreach (var range in Ranges) Find<ComboBox>("MenuRangeCombo").Items.Add(range);
      Bind("PedalSetKeyScale", delegate { SetKeyScale(); });
      Bind("MenuApplyKey", delegate { SetKeyScaleRange(); });
      Bind("VxTab", delegate { ShowPage(true); });
      Bind("MenuHome", delegate { ShowPage(true); });
      Bind("MenuPreset", delegate { ShowSubmenu("PRESET"); });
      Bind("MenuKey", delegate { ShowSubmenu("KEY"); });
      Bind("MenuAutoTune", delegate { ShowSubmenu("AUTO-TUNE"); });
      Bind("MenuHarmony", delegate { ShowSubmenu("HARMONY"); });
      Bind("MenuFx", delegate { ShowSubmenu("FX"); });
      Bind("AdvancedButton", delegate { ShowSubmenu("ADVANCED"); });
      Bind("MxTab", delegate { ShowPage(false); });
      Bind("StageAb", delegate { RequestAb(requestedAb == 1 ? 2 : 1); });
      Bind("StageTalk", delegate { RequestTalk(!requestedTalk); });
      Bind("RefreshButton", delegate { if (Find<Grid>("VxPage").Visibility == Visibility.Visible) RefreshVx(); else ScanMidi(); });
      Bind("RefreshMidi", delegate { ScanMidi(); });
      Bind("VxRecall", delegate { int n; if (ParseInt(Find<TextBox>("VxPreset"), 1, 250, out n)) RecallVx(n); });
      Bind("PedalPrev", delegate { StepPreset(-1); });
      Bind("PedalNext", delegate { StepPreset(1); });
      Bind("PedalAutoTune", delegate { TogglePedal("Auto-Tune Enable"); });
      Bind("PedalHarmony", delegate { TogglePedal("Harmony Enable"); });
      Bind("PedalFx", delegate { TogglePedal("Fx Enable"); });
      BindPedalSlider("Volume", "PedalVolumeSlider", "PedalVolumeValue", "PedalVolumeSet");
      BindPedalSlider("Retune Speed", "PedalSpeedSlider", "PedalSpeedValue", "PedalSpeedSet");
      BindPedalSlider("Humanise Amount", "PedalHumanizeSlider", "PedalHumanizeValue", "PedalHumanizeSet");
      BindKnob("VolumeKnob", "PedalVolumeSlider", "Volume");
      BindKnob("SpeedKnob", "PedalSpeedSlider", "Retune Speed");
      BindKnob("HumanizeKnob", "PedalHumanizeSlider", "Humanise Amount");
      Bind("VxAbOff", delegate { RequestAb(0); });
      Bind("VxAbA", delegate { RequestAb(1); });
      Bind("VxAbB", delegate { RequestAb(2); });
      Bind("VxTalkOff", delegate { RequestTalk(false); });
      Bind("VxTalkOn", delegate { RequestTalk(true); });
      BuildMxPage();
      window.Loaded += delegate {
        ScanMidi(); RefreshVx();
        window.Dispatcher.BeginInvoke(new Action(delegate { Find<ScrollViewer>("VxActionsScroll").ScrollToTop(); }), DispatcherPriority.Loaded);
      };
    }

    T Find<T>(string name) where T : FrameworkElement { return (T)window.FindName(name); }
    void Bind(string name, Action action) { Find<Button>(name).Click += delegate { action(); }; }
    void Status(string message) { Find<TextBlock>("StatusLine").Text = DateTime.Now.ToString("HH:mm:ss") + "  ·  " + message; }
    void ShowPage(bool vx) {
      editingStoredPreset = false;
      activeMenu = "STAGE";
      Find<Grid>("VxPage").Visibility = vx ? Visibility.Visible : Visibility.Collapsed;
      Find<Grid>("MxPage").Visibility = vx ? Visibility.Collapsed : Visibility.Visible;
      Find<Grid>("VxStage").Visibility = Visibility.Visible;
      Find<Grid>("VxEditor").Visibility = Visibility.Collapsed;
      Find<Grid>("VxPresets").Visibility = Visibility.Collapsed;
      Find<Grid>("VxKeyMenu").Visibility = Visibility.Collapsed;
      Find<TextBlock>("PageEyebrow").Text = vx ? "HEADRUSH / VX5" : "HEADRUSH / MX5";
      Find<TextBlock>("PageTitle").Text = vx ? "Stage" : "Guitar control";
      Find<TextBlock>("PageSub").Text = vx ? "Hands-on control for the connected pedal." : "Rig and performance commands through MIDI.";
      Find<Button>("VxTab").Background = BrushOf(vx ? "#234D49" : "#24313B");
      Find<Button>("MxTab").Background = BrushOf(vx ? "#24313B" : "#234D49");
      HighlightMenu();
    }
    void HighlightMenu() {
      foreach (var pair in new[] { "PRESET", "KEY", "AUTO-TUNE", "HARMONY", "FX" }) {
        string control = pair == "PRESET" ? "MenuPreset" : pair == "KEY" ? "MenuKey" : pair == "AUTO-TUNE" ? "MenuAutoTune" : pair == "HARMONY" ? "MenuHarmony" : "MenuFx";
        Find<Button>(control).Background = BrushOf(activeMenu == pair ? "#216B56" : "#252B33");
      }
      Find<Button>("AdvancedButton").Background = BrushOf(activeMenu == "ADVANCED" ? "#216B56" : "#252B33");
      Find<Button>("MenuHome").Background = BrushOf(activeMenu == "STAGE" ? "#216B56" : "#303C46");
    }
    void ShowSubmenu(string menu) {
      editingStoredPreset = false;
      activeMenu = menu;
      Find<Grid>("VxPage").Visibility = Visibility.Visible;
      Find<Grid>("MxPage").Visibility = Visibility.Collapsed;
      Find<Grid>("VxStage").Visibility = Visibility.Collapsed;
      Find<Grid>("VxEditor").Visibility = menu == "AUTO-TUNE" || menu == "HARMONY" || menu == "FX" || menu == "ADVANCED" ? Visibility.Visible : Visibility.Collapsed;
      Find<Grid>("VxPresets").Visibility = menu == "PRESET" ? Visibility.Visible : Visibility.Collapsed;
      Find<Grid>("VxKeyMenu").Visibility = menu == "KEY" ? Visibility.Visible : Visibility.Collapsed;
      Find<TextBlock>("PageEyebrow").Text = "HEADRUSH / VX5";
      Find<TextBlock>("PageTitle").Text = menu == "AUTO-TUNE" ? "Auto-Tune" : menu == "PRESET" ? "Presets" : menu == "KEY" ? "Key" : menu == "HARMONY" ? "Harmony" : menu == "ADVANCED" ? "Advanced" : "FX";
      Find<TextBlock>("PageSub").Text = menu == "PRESET" ? "Names and custom patches for each slot." : menu == "KEY" ? "Choose the song key, scale, and vocal range." : "Live VX5 settings.";
      Find<TextBlock>("EditorHeading").Text = "VX5 / " + menu;
      Find<Button>("VxTab").Background = BrushOf("#234D49");
      Find<Button>("MxTab").Background = BrushOf("#24313B");
      HighlightMenu();
      if (menu == "PRESET") RefreshPresetList();
      if (metadata != null && Find<Grid>("VxEditor").Visibility == Visibility.Visible) BuildParameters();
    }
  }
}
