using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Forms
{
    /// <summary>Compact modal dashboard. Buttons run synchronously on the Revit command thread.</summary>
    public sealed class RuntimeQaForm : Form
    {
        private readonly RuntimeQaContext _context;
        private readonly RuntimeQaRegistry _registry;
        private readonly RuntimeQaRunner _runner;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private QaRunResult _lastRun;

        public RuntimeQaForm(RuntimeQaContext context, RuntimeQaRegistry registry)
        {
            _context = context;
            _registry = registry ?? RuntimeQaRegistry.Default;
            _runner = new RuntimeQaRunner(_registry);
            Text = "K-TOOLS RUNTIME QA";
            Width = 980;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            var header = new Label { Dock = DockStyle.Fill, Text = "K-TOOLS RUNTIME QA\nRevit: " + (context?.Document?.Application?.VersionNumber ?? "unknown") + "    Document: " + (context?.Document?.Title ?? "<none>"), Font = new Font(Font, FontStyle.Bold), AutoEllipsis = true };
            root.Controls.Add(header, 0, 0);
            var warning = new Label { Dock = DockStyle.Fill, Text = "Warning: fixtures create temporary Revit elements and roll them back. Use a detached/test model where possible.", ForeColor = Color.DarkOrange, AutoEllipsis = true };
            root.Controls.Add(warning, 0, 1);

            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fixture", DataPropertyName = "Name", Width = 300 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = "FixtureId", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Checks", DataPropertyName = "CheckCount", Width = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duration", DataPropertyName = "DurationText", Width = 90 });
            root.Controls.Add(_grid, 0, 2);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            AddButton(footer, "Run All", (s, e) => Run(() => _runner.RunAll(_context)));
            AddButton(footer, "Documentation", (s, e) => Run(() => _runner.RunSuite(_context, "DOCUMENTATION")));
            AddButton(footer, "Rebar", (s, e) => Run(() => _runner.RunSuite(_context, "REBAR")));
            AddButton(footer, "Run Selected", (s, e) => RunSelected());
            AddButton(footer, "Failed", (s, e) => Filter(QaStatus.FAIL));
            AddButton(footer, "Blocked", (s, e) => Filter(QaStatus.BLOCKED));
            AddButton(footer, "Open Report", (s, e) => OpenReportFolder());
            AddButton(footer, "Copy Summary", (s, e) => CopySummary());
            AddButton(footer, "Close", (s, e) => Close());
            _status = new Label { AutoSize = true, Padding = new Padding(12, 8, 0, 0), Text = "Ready. Choose a suite to begin." };
            footer.Controls.Add(_status);
            root.Controls.Add(footer, 0, 3);
        }

        private void AddButton(Control parent, string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 30 };
            button.Click += handler;
            parent.Controls.Add(button);
        }

        private void Run(Func<QaRunResult> action)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _lastRun = action();
                _grid.DataSource = _lastRun.Fixtures.Select(f => new FixtureRow(f)).ToList();
                _status.Text = string.Format("{0}: PASS {1}, FAIL {2}, BLOCKED {3}. Report: {4}", _lastRun.CertificationStatus, _lastRun.Passed, _lastRun.Failed, _lastRun.Blocked, _lastRun.ReportDirectory);
            }
            catch (Exception ex)
            {
                _status.Text = "Runner error: " + ex.Message;
            }
            finally { Cursor = Cursors.Default; }
        }

        private void Filter(QaStatus status)
        {
            if (_lastRun == null) return;
            _grid.DataSource = _lastRun.Fixtures.Where(f => f.Status == status).Select(f => new FixtureRow(f)).ToList();
        }

        private void RunSelected()
        {
            if (_grid.SelectedRows.Count == 0) return;
            FixtureRow row = _grid.SelectedRows[0].DataBoundItem as FixtureRow;
            if (row == null || string.IsNullOrWhiteSpace(row.FixtureId)) return;
            Run(() => _runner.RunFixture(_context, row.FixtureId));
        }

        private void OpenReportFolder()
        {
            if (_lastRun == null || string.IsNullOrWhiteSpace(_lastRun.ReportDirectory)) return;
            try { Process.Start(new ProcessStartInfo { FileName = _lastRun.ReportDirectory, UseShellExecute = true }); } catch { }
        }

        private void CopySummary()
        {
            if (_lastRun == null) return;
            try { Clipboard.SetText(RuntimeQaReportWriter.BuildSummary(_lastRun)); } catch { }
        }

        private sealed class FixtureRow
        {
            public string Status { get; private set; }
            public string Name { get; private set; }
            public string FixtureId { get; private set; }
            public int CheckCount { get; private set; }
            public string DurationText { get; private set; }
            public FixtureRow(QaFixtureResult fixture)
            {
                Status = fixture.Status.ToString(); Name = fixture.Name; FixtureId = fixture.FixtureId;
                CheckCount = fixture.Checks == null ? 0 : fixture.Checks.Count;
                DurationText = fixture.Duration.TotalMilliseconds.ToString("F0") + " ms";
            }
        }
    }
}
