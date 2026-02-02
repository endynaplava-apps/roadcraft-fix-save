using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RoadCraftSaveTool;

public sealed class MainForm : Form
{
    private readonly TextBox _txtFile = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button _btnBrowse = new() { Text = "Browse…", AutoSize = true };
    private readonly Button _btnProcess = new() { Text = "Process (Patch + Overwrite)", AutoSize = true };
    private readonly CheckBox _chkBackup = new() { Text = "Create backup (.bak_yyyyMMdd_HHmmss) before overwrite", Checked = true, AutoSize = true };
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly RadioButton _rbPatchBce = new() { Text = "Patch Map: infrastructure.request-system → Establish_Task_Build_Crane (Build Crane)", Checked = true, AutoSize = true };
    private readonly RadioButton _rbPatchMap07Route31 = new()
    {
        Text = "Patch Map: infrastructure.request-system → Route_Task_31_Construction_of_a_special_warehouse_stage (Toxic Waste storage)",
        AutoSize = true
    };
    public MainForm()
    {
        Text = "RoadCraft Save Patcher";
        Width = 860;
        Height = 540;
        StartPosition = FormStartPosition.CenterScreen;
        _rbPatchBce.Checked = false;
        _rbPatchMap07Route31.Checked = false;
        _rbPatchBce.CheckedChanged += (_, __) => UpdateActionLog();
        _rbPatchMap07Route31.CheckedChanged += (_, __) => UpdateActionLog();
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // File picker row
        var fileRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        fileRow.Controls.Add(new Label { Text = "Save file:", AutoSize = true, Padding = new Padding(0, 6, 8, 0) }, 0, 0);
        fileRow.Controls.Add(_txtFile, 1, 0);
        fileRow.Controls.Add(_btnBrowse, 2, 0);

        // Action row
        var actionBox = new GroupBox { Text = "Action", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        var actionLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        actionLayout.Controls.Add(_rbPatchBce);
        actionLayout.Controls.Add(_rbPatchMap07Route31);
        actionBox.Controls.Add(actionLayout);

        // Controls row
        var controlsRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        controlsRow.Controls.Add(_chkBackup);
        controlsRow.Controls.Add(new Label { Text = "   ", AutoSize = true });
        controlsRow.Controls.Add(_btnProcess);

        // Log header
        var logHeader = new Label { Text = "Log:", AutoSize = true };

        root.Controls.Add(fileRow, 0, 0);
        root.Controls.Add(actionBox, 0, 1);
        root.Controls.Add(controlsRow, 0, 2);
        root.Controls.Add(logHeader, 0, 3);
        root.Controls.Add(_txtLog, 0, 4);

        Controls.Add(root);

        _btnBrowse.Click += (_, __) => BrowseFile();
        _btnProcess.Click += async (_, __) => await ProcessAsync();

        UpdateActionLog();
    }
    private void UpdateActionLog()
    {
        ClearLog();

        if (_rbPatchBce.Checked)
        {
            AppendLog("Selected action:");
            AppendLog("→ Map 08 – Build Crane (Establish_Task_Build_Crane)");
            AppendLog("");
            AppendLog("1) Click Browse and select:");
            AppendLog("example: C:\\Users\\<YOU>\\AppData\\Local\\Saber\\RoadCraftGame\\storage\\steam\\user\\<STEAM_ID>\\Main\\save\\SLOT_0");
            AppendLog("Select file:   rb_map_08_contamination");
            AppendLog("");
            AppendLog("2) Click Process to apply the patch.");
            AppendLog("   The original file will be overwritten");
            AppendLog("   (backup is created if enabled).");
        }
        else if (_rbPatchMap07Route31.Checked)
        {
            AppendLog("Selected action:");
            AppendLog("→ Map 07 – Toxic waste facility (Construction of a special warehouse)");
            AppendLog("");
            AppendLog("1) Click Browse and select:");
            AppendLog("example: C:\\Users\\<YOU>\\AppData\\Local\\Saber\\RoadCraftGame\\storage\\steam\\user\\<STEAM_ID>\\Main\\save\\SLOT_0");
            AppendLog("Select file:   rb_map_07_rail_failure");
            AppendLog("");
            AppendLog("2) Click Process to replace:");
            AppendLog("   The original file will be overwritten");
            AppendLog("   (backup is created if enabled).");
        }
        else
        {
            AppendLog("No action selected.");
            AppendLog("");
            AppendLog("Please select an action first:");
            AppendLog("• Map 08 – Build Crane");
            AppendLog("• Map 07 – Toxic waste facility");
        }

        AppendLog("");
        AppendLog("Tip: Close the game before patching to avoid file lock errors.");
    }
    private void ClearLog()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ClearLog));
            return;
        }

        _txtLog.Clear();
    }
    private void BrowseFile()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Select RoadCraft save file",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (!string.IsNullOrWhiteSpace(_txtFile.Text))
        {
            try
            {
                var dir = Path.GetDirectoryName(_txtFile.Text);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    ofd.InitialDirectory = dir;
            }
            catch { /* ignore */ }
        }

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _txtFile.Text = ofd.FileName;
            var fn = Path.GetFileName(ofd.FileName).ToLowerInvariant();
            if (fn.Contains("rb_map_08_contamination"))
                _rbPatchBce.Checked = true;
            else if (fn.Contains("rb_map_07_rail_failure"))
                _rbPatchMap07Route31.Checked = true;

            // refresh instructions (will ClearLog inside)
            UpdateActionLog();
        }
    }

    private async Task ProcessAsync()
    {
        ClearLog();

        var path = _txtFile.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            AppendLog("ERROR: Please select an existing file first.");
            MessageBox.Show(this, "Please select an existing file first.", "Missing file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_rbPatchBce.Checked && !_rbPatchMap07Route31.Checked)
        {
            AppendLog("ERROR: No action selected.");
            MessageBox.Show(this, "Please select an action first.", "No action selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AppendLog("Processing started…");
        AppendLog($"File: {path}");
        AppendLog(_chkBackup.Checked ? "Backup: enabled" : "Backup: disabled");
        AppendLog("");

        SetUiEnabled(false);

        try
        {
            await Task.Run(() =>
            {
                if (_rbPatchBce.Checked)
                    PatchBuildCraneEstablishInPlace(path, createBackup: _chkBackup.Checked);
                else if (_rbPatchMap07Route31.Checked)
                    PatchMap07Route31InPlace(path, createBackup: _chkBackup.Checked);
            });

            AppendLog("");
            AppendLog("SUCCESS: File was patched and overwritten.");
            MessageBox.Show(this, "Done! Save file was patched successfully.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppendLog("");
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), "Patch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetUiEnabled(true);
        }
    }

    private void PatchMap07Route31InPlace(string inputFile, bool createBackup)
    {
        var dir = Path.GetDirectoryName(inputFile) ?? "";
        var name = Path.GetFileName(inputFile);
        var tmp = Path.Combine(dir, name + ".tmp");

        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        AppendLogThreadSafe($"Patching Map07 Route31 (temp): {tmp}");
        MapPatcher.PatchMap07RouteTask31(inputFile, tmp);

        if (!File.Exists(tmp) || new FileInfo(tmp).Length < 16)
            throw new InvalidDataException("Patch produced an invalid temp output file.");

        if (createBackup)
        {
            var backup = Path.Combine(dir, name + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}");
            AppendLogThreadSafe($"Replacing original (backup): {backup}");
            File.Replace(tmp, inputFile, backup, ignoreMetadataErrors: true);
            AppendLogThreadSafe($"Backup created: {backup}");
        }
        else
        {
            AppendLogThreadSafe("Overwriting original (no backup) …");
            File.Copy(tmp, inputFile, overwrite: true);
            try { File.Delete(tmp); } catch { }
        }
    }
    private void PatchBuildCraneEstablishInPlace(string inputFile, bool createBackup)
    {
        var dir = Path.GetDirectoryName(inputFile) ?? "";
        var name = Path.GetFileName(inputFile);

        var tmp = Path.Combine(dir, name + ".tmp");

        // Ensure tmp doesn't exist
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }

        AppendLogThreadSafe($"Patching (temp): {tmp}");
        MapPatcher.PatchBuildCraneEstablish(inputFile, tmp);

        if (!File.Exists(tmp) || new FileInfo(tmp).Length < 16)
            throw new InvalidDataException("Patch produced an invalid temp output file.");

        if (createBackup)
        {
            var backup = Path.Combine(dir, name + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}");
            AppendLogThreadSafe($"Replacing original (backup): {backup}");

            // File.Replace is atomic and preserves metadata better than Copy
            File.Replace(tmp, inputFile, backup, ignoreMetadataErrors: true);
            AppendLogThreadSafe($"Backup created: {backup}");
        }
        else
        {
            AppendLogThreadSafe("Overwriting original (no backup) …");
            File.Copy(tmp, inputFile, overwrite: true);
            try { File.Delete(tmp); } catch { /* ignore */ }
        }
    }

    private void SetUiEnabled(bool enabled)
    {
        _btnBrowse.Enabled = enabled;
        _btnProcess.Enabled = enabled;
        _chkBackup.Enabled = enabled;
        _rbPatchBce.Enabled = enabled;
        _rbPatchMap07Route31.Enabled = enabled;
    }

    private void AppendLog(string line)
    {
        _txtLog.AppendText(line + Environment.NewLine);
    }

    private void AppendLogThreadSafe(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(line)));
            return;
        }
        AppendLog(line);
    }
}
