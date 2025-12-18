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

    public MainForm()
    {
        Text = "RoadCraft Save Patcher";
        Width = 860;
        Height = 540;
        StartPosition = FormStartPosition.CenterScreen;

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

        AppendLog("1) Click Browse… and select a RoadCraft save file.");
        AppendLog("example: C:\\Users\\<YOU>\\AppData\\Local\\Saber\\RoadCraftGame\\storage\\steam\\user\\<STEAM_ID>\\Main\\save\\SLOT_0");
        AppendLog("2) Select file: rb_map_08_contamination");
        AppendLog("3) Click Process to patch and overwrite the selected file (backup optional).");
        AppendLog("");
        AppendLog("Tip: Close the game before patching to avoid file lock errors.");
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
            AppendLog($"Selected: {ofd.FileName}");
        }
    }

    private async Task ProcessAsync()
    {
        var path = _txtFile.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Please select an existing file first.", "Missing file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetUiEnabled(false);

        try
        {
            await Task.Run(() =>
            {
                if (_rbPatchBce.Checked)
                    PatchBuildCraneEstablishInPlace(path, createBackup: _chkBackup.Checked);
                else
                    throw new InvalidOperationException("No action selected.");
            });

            AppendLog("SUCCESS: File was patched and overwritten.");
            MessageBox.Show(this, "Done! Save file was patched successfully.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), "Patch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetUiEnabled(true);
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
