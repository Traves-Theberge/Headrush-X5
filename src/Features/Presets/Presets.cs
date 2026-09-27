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
    void LoadPresetLibrary() {
      for (int number = 1; number <= 250; number++) presets[number] = new PresetRecord {
        Number = number,
        Name = number <= FactoryNames.Length ? FactoryNames[number - 1] : "Preset " + number,
        Source = number <= FactoryNames.Length ? "Factory" : "Placeholder"
      };
      try {
        if (File.Exists(presetFile)) {
          var saved = json.DeserializeObject(File.ReadAllText(presetFile)) as object[];
          if (saved != null) foreach (var entry in saved) {
            var item = Dict(entry);
            if (item == null) continue;
            int number = Convert.ToInt32(item["Number"], CultureInfo.InvariantCulture);
            if (number < 1 || number > 250) continue;
            var record = presets[number];
            record.Name = Convert.ToString(item["Name"]);
            record.Source = Convert.ToString(item["Source"]);
            if (item.ContainsKey("State")) record.State = Dict(item["State"]);
          }
        }
      } catch (Exception ex) { Status("Could not read the preset library: " + Message(ex)); }
      RefreshPresetList();
    }
    void SavePresetLibrary() {
      try {
        var records = new List<PresetRecord>();
        for (int number = 1; number <= 250; number++) {
          var record = presets[number];
          if (record.Source == "Local" || record.State != null) records.Add(record);
        }
        File.WriteAllText(presetFile, json.Serialize(records));
      } catch (Exception ex) { Status("Could not save preset library: " + Message(ex)); }
    }
    void RefreshPresetList() {
      var list = Find<ListBox>("PresetList");
      int keep = selectedPreset == null ? 1 : selectedPreset.Number;
      string query = Find<TextBox>("PresetSearch").Text.Trim();
      list.Items.Clear();
      for (int number = 1; number <= 250; number++) {
        var record = presets[number];
        if (query.Length > 0 && record.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && number.ToString(CultureInfo.InvariantCulture).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
        var item = new PresetListItem { Number = number, Name = record.Name };
        list.Items.Add(item);
        if (number == keep) list.SelectedItem = item;
      }
      if (list.SelectedItem == null && list.Items.Count > 0) list.SelectedIndex = 0;
    }
    void SelectPreset() {
      var item = Find<ListBox>("PresetList").SelectedItem as PresetListItem;
      if (item == null) return;
      selectedPreset = presets[item.Number];
      Find<TextBlock>("PresetNumberTitle").Text = "PRESET " + item.Number.ToString("000", CultureInfo.InvariantCulture);
      Find<TextBox>("PresetNameInput").Text = selectedPreset.Name;
      Find<TextBlock>("PresetSource").Text = selectedPreset.Source == "Factory" ? "Factory name from HeadRush user guide" : selectedPreset.Source == "Local" ? "Name stored in this PC library" : "Unassigned device slot · local placeholder name";
      Find<TextBlock>("PresetStateCount").Text = selectedPreset.State == null ? "No snapshot captured" : selectedPreset.State.Count + " saved parameters for this slot";
      Find<Button>("PresetEdit").IsEnabled = selectedPreset.State != null;
      Find<Button>("PresetApply").IsEnabled = selectedPreset.State != null;
    }
    void SavePresetName() {
      if (selectedPreset == null) return;
      string name = Find<TextBox>("PresetNameInput").Text.Trim();
      if (name.Length == 0 || name.Length > 32) { Status("Preset name must be 1–32 characters"); return; }
      selectedPreset.Name = name;
      selectedPreset.Source = "Local";
      SavePresetLibrary(); RefreshPresetList(); SelectPreset();
      if (lastRequestedPreset == selectedPreset.Number) Find<TextBlock>("PedalPresetName").Text = name;
      Status("Preset " + selectedPreset.Number + " name saved in PC library");
    }
    void CapturePreset() {
      if (selectedPreset == null || values == null) { Status("Connect the VX5 first"); return; }
      if (lastRequestedPreset != selectedPreset.Number) { Status("Load this preset on the VX5 before capturing it"); return; }
      try {
        values = Dict(Request("/object-properties" + StatePath, "GET", null));
        selectedPreset.State = new Dictionary<string, object>(values);
        SavePresetLibrary(); SelectPreset(); SyncPedal();
        Status("Captured " + selectedPreset.State.Count + " live parameters for preset " + selectedPreset.Number);
      } catch (Exception ex) { Status("Capture failed: " + Message(ex)); }
    }
    void CreateCustomPatch() {
      if (selectedPreset == null || values == null) { Status("Connect the VX5 and select a destination slot"); return; }
      string name = Find<TextBox>("PresetNameInput").Text.Trim();
      if (name.Length == 0 || name.Length > 32) { Status("Choose a patch name of 1 to 32 characters"); return; }
      selectedPreset.Name = name;
      selectedPreset.Source = "Local";
      selectedPreset.State = new Dictionary<string, object>(values);
      SavePresetLibrary(); RefreshPresetList(); SelectPreset(); EditSavedPreset();
      Status("Custom patch created in PC library. Edit it, then load and apply to the VX5.");
    }
    void EditSavedPreset() {
      if (selectedPreset == null || selectedPreset.State == null) { Status("Capture this preset first"); return; }
      ShowSubmenu("PRESET"); editingStoredPreset = true;
      Find<Grid>("VxPresets").Visibility = Visibility.Collapsed;
      Find<Grid>("VxEditor").Visibility = Visibility.Visible;
      Find<TextBlock>("PageTitle").Text = "Preset " + selectedPreset.Number + " · " + selectedPreset.Name;
      Find<TextBlock>("PageSub").Text = "Editing the PC snapshot. Apply it live from Presets.";
      Find<TextBlock>("EditorHeading").Text = "SAVED SETTINGS / PRESET " + selectedPreset.Number.ToString("000", CultureInfo.InvariantCulture);
      BuildParameters();
    }
    void ApplyPreset() {
      if (selectedPreset == null || selectedPreset.State == null) { Status("Capture and edit this preset first"); return; }
      if (lastRequestedPreset != selectedPreset.Number) { Status("Load this preset on the VX5 before applying its saved settings"); return; }
      try {
        var live = Dict(Request("/object-properties" + StatePath, "GET", null));
        var changes = new Dictionary<string, object>();
        foreach (var pair in selectedPreset.State) if (live.ContainsKey(pair.Key) && Convert.ToString(live[pair.Key], CultureInfo.InvariantCulture) != Convert.ToString(pair.Value, CultureInfo.InvariantCulture)) changes[pair.Key] = pair.Value;
        if (changes.Count > 0) Request("/object-properties" + StatePath, "PUT", changes);
        values = Dict(Request("/object-properties" + StatePath, "GET", null));
        SyncPedal();
        Status("Applied " + changes.Count + " settings live to preset " + selectedPreset.Number + ". Save on the pedal to persist.");
      } catch (Exception ex) { Status("Could not apply preset: " + Message(ex)); }
    }
  }
}
