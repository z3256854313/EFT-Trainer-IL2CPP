using System.ComponentModel;
using System.Text;

namespace TrainerEditor;

internal sealed record PartChoice(string Value, string Display)
{
	public override string ToString()
	{
		return Display;
	}
}

internal sealed class EditorForm : Form
{
	private const string AimbotPrefix = "EFT.Trainer.Features.Aimbot.";
	private const string PlayersPrefix = "EFT.Trainer.Features.Players.";

	private readonly TextBox _pathBox;
	private readonly TextBox _rawText;
	private readonly DataGridView _allSettings;
	private readonly TabControl _tabs;
	private readonly StatusStrip _statusStrip;
	private readonly ToolStripStatusLabel _statusText;
	private readonly CheckBox _createBackup;

	private readonly ComboBox _aimPart;
	private readonly CheckBox _preferHeadChest;
	private readonly CheckBox _visibleOnly;
	private readonly CheckBox _silentAim;
	private readonly NumericUpDown _smoothness;
	private readonly NumericUpDown _maximumDistance;
	private readonly NumericUpDown _fovRadius;

	private readonly CheckBox _playersEnabled;
	private readonly CheckBox _showBoxes;
	private readonly CheckBox _xrayVision;
	private readonly CheckBox _showCharms;
	private readonly CheckBox _useRendererBounds;
	private readonly CheckBox _clampBoxToScope;
	private readonly NumericUpDown _boxThickness;
	private readonly NumericUpDown _boxPadding;
	private readonly NumericUpDown _espMaximumDistance;

	private IniDocument _document = IniDocument.Empty();
	private bool _loading;
	private bool _dirty;
	private int _rawTabIndex;

	public EditorForm(string initialPath)
	{
		Text = "EscapeFromTarkov Trainer 配置编辑器";
		MinimumSize = new Size(820, 640);
		Size = new Size(960, 760);
		StartPosition = FormStartPosition.CenterScreen;
		Font = new Font("Segoe UI", 9F);

		var top = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 106,
			Padding = new Padding(12, 8, 12, 6),
			ColumnCount = 3,
			RowCount = 3
		};
		top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
		top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		var title = new Label
		{
			Text = "Trainer 配置编辑器",
			AutoSize = true,
			Font = new Font("Segoe UI Semibold", 14F),
			Margin = new Padding(0, 0, 0, 7)
		};

		var pathLabel = new Label
		{
			Text = "配置文件",
			AutoSize = true,
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 7, 8, 5)
		};

		_pathBox = new TextBox
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 3, 10, 3)
		};

		_createBackup = new CheckBox
		{
			Text = "保存前自动备份",
			Checked = true,
			AutoSize = true,
			Margin = new Padding(72, 2, 0, 0)
		};

		var openButton = MakeButton("打开", 0, 0, 68);
		var saveButton = MakeButton("保存", 0, 0, 68);
		var saveAsButton = MakeButton("另存为", 0, 0, 78);
		var backupButton = MakeButton("备份", 0, 0, 74);

		var actions = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			WrapContents = false,
			FlowDirection = FlowDirection.LeftToRight,
			Margin = new Padding(0, 0, 0, 0)
		};
		actions.Controls.AddRange([openButton, saveButton, saveAsButton, backupButton]);

		openButton.Click += (_, _) => OpenFile();
		saveButton.Click += (_, _) => SaveFile();
		saveAsButton.Click += (_, _) => SaveFile(saveAs: true);
		backupButton.Click += (_, _) => CreateManualBackup();

		top.Controls.Add(title, 0, 0);
		top.SetColumnSpan(title, 3);
		top.Controls.Add(pathLabel, 0, 1);
		top.Controls.Add(_pathBox, 1, 1);
		top.Controls.Add(actions, 2, 1);
		top.Controls.Add(_createBackup, 0, 2);
		top.SetColumnSpan(_createBackup, 3);

		_tabs = new TabControl
		{
			Dock = DockStyle.Fill,
			Padding = new Point(14, 6)
		};

		var aimPage = new TabPage("锁定");
		var espPage = new TabPage("ESP / 玩家");
		var allPage = new TabPage("全部配置");
		var rawPage = new TabPage("原始文本");

		_aimPart = MakeCombo(
		[
			new("Head", "头"),
			new("Chest", "胸"),
			new("Stomach", "腹部"),
			new("Arms", "胳膊"),
			new("Legs", "腿")
		]);
		_preferHeadChest = MakeCheckBox("优先级：头 > 胸 > 当前选择部位");
		_visibleOnly = MakeCheckBox("只锁定可见部位");
		_silentAim = MakeCheckBox("启用 Silent Aim");
		_smoothness = MakeNumeric(0.01m, 1m, 0.01m, 2);
		_maximumDistance = MakeNumeric(1m, 2000m, 10m, 0);
		_fovRadius = MakeNumeric(0m, 2000m, 10m, 0);

		BuildAimbotTab(aimPage);

		_playersEnabled = MakeCheckBox("启用玩家 ESP");
		_showBoxes = MakeCheckBox("显示玩家方框");
		_xrayVision = MakeCheckBox("隔墙可见");
		_showCharms = MakeCheckBox("启用人形轮廓");
		_useRendererBounds = MakeCheckBox("使用 Renderer 包围盒计算方框");
		_clampBoxToScope = MakeCheckBox("将方框裁剪到瞄具范围");
		_boxThickness = MakeNumeric(0.5m, 10m, 0.25m, 2);
		_boxPadding = MakeNumeric(0m, 0.5m, 0.01m, 2);
		_espMaximumDistance = MakeNumeric(0m, 2000m, 25m, 0);

		BuildEspTab(espPage);

		_allSettings = new DataGridView
		{
			Dock = DockStyle.Fill,
			AllowUserToAddRows = false,
			AllowUserToDeleteRows = false,
			AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
			EditMode = DataGridViewEditMode.EditOnEnter,
			RowHeadersVisible = false,
			SelectionMode = DataGridViewSelectionMode.CellSelect
		};
		_allSettings.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Key",
			HeaderText = "Key",
			ReadOnly = true,
			FillWeight = 62
		});
		_allSettings.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = "Value",
			HeaderText = "Value",
			FillWeight = 38
		});
		_allSettings.CellEndEdit += OnGridCellEndEdit;
		allPage.Controls.Add(_allSettings);

		_rawText = new TextBox
		{
			Dock = DockStyle.Fill,
			Multiline = true,
			AcceptsReturn = true,
			AcceptsTab = true,
			ScrollBars = ScrollBars.Both,
			WordWrap = false,
			Font = new Font("Consolas", 9F)
		};
		_rawText.TextChanged += (_, _) => MarkDirty();
		rawPage.Controls.Add(_rawText);
		_tabs.TabPages.Add(rawPage);

		_tabs.TabPages.Insert(0, aimPage);
		_tabs.TabPages.Insert(1, espPage);
		_tabs.TabPages.Insert(2, allPage);
		_rawTabIndex = _tabs.TabPages.IndexOf(rawPage);
		_tabs.SelectedIndexChanged += OnTabChanged;

		_statusStrip = new StatusStrip();
		_statusText = new ToolStripStatusLabel("正在加载...")
		{
			Spring = true,
			TextAlign = ContentAlignment.MiddleLeft
		};
		_statusStrip.Items.Add(_statusText);

		Controls.Add(_tabs);
		Controls.Add(_statusStrip);
		Controls.Add(top);
		FormClosing += OnFormClosing;

		WireEvents();
		LoadDocument(initialPath);
	}

	private void BuildAimbotTab(TabPage page)
	{
		var content = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			ColumnCount = 2,
			Padding = new Padding(14),
			RowCount = 10
		};
		content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
		content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

		var help = new Label
		{
			Text = "配置会保存为 trainer.ini 使用的 JSON 值。可见性检查同时作用于普通锁定和 Silent Aim。",
			AutoSize = true,
			MaximumSize = new Size(680, 0),
			ForeColor = Color.DimGray
		};
		content.Controls.Add(help, 0, 0);
		content.SetColumnSpan(help, 2);

		AddRow(content, 1, "锁定部位", _aimPart);
		AddRow(content, 2, "锁定优先级", _preferHeadChest);
		AddRow(content, 3, "可见性", _visibleOnly);
		AddRow(content, 4, "平滑度（0.01–1.0）", _smoothness);
		AddRow(content, 5, "最大距离", _maximumDistance);
		AddRow(content, 6, "锁定视野半径", _fovRadius);
		AddRow(content, 7, "Silent Aim", _silentAim);

		var partNote = new Label
		{
			Text = "优先级顺序为头 > 胸 > 当前部位。关闭优先级后只锁定当前选择部位。",
			AutoSize = true,
			MaximumSize = new Size(680, 0),
			ForeColor = Color.DimGray
		};
		content.Controls.Add(partNote, 0, 8);
		content.SetColumnSpan(partNote, 2);

		page.Controls.Add(content);
	}

	private void BuildEspTab(TabPage page)
	{
		var content = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			ColumnCount = 2,
			Padding = new Padding(14),
			RowCount = 13
		};
		content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
		content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

		var help = new Label
		{
			Text = "方框通过当前普通摄像机或瞄具摄像机投影。瞄具裁剪可避免方框超出瞄具范围。",
			AutoSize = true,
			MaximumSize = new Size(680, 0),
			ForeColor = Color.DimGray
		};
		content.Controls.Add(help, 0, 0);
		content.SetColumnSpan(help, 2);

		AddRow(content, 1, "启用 ESP", _playersEnabled);
		AddRow(content, 2, "显示方框", _showBoxes);
		AddRow(content, 3, "隔墙显示", _xrayVision);
		AddRow(content, 4, "人体轮廓", _showCharms);
		AddRow(content, 5, "Renderer 包围盒", _useRendererBounds);
		AddRow(content, 6, "裁剪到瞄具", _clampBoxToScope);
		AddRow(content, 7, "边框粗细", _boxThickness);
		AddRow(content, 8, "方框内边距（0–0.5）", _boxPadding);
		AddRow(content, 9, "最大距离（0 = 不限）", _espMaximumDistance);

		page.Controls.Add(content);
	}

	private void WireEvents()
	{
		_aimPart.SelectedIndexChanged += (_, _) => UpdateControl(
			() => _document.SetString(
				AimbotPrefix + "TargetPart",
				(_aimPart.SelectedItem as PartChoice)?.Value ?? "Head"));
		_preferHeadChest.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(AimbotPrefix + "PreferHeadChest", _preferHeadChest.Checked));
		_visibleOnly.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(AimbotPrefix + "VisibleOnly", _visibleOnly.Checked));
		_silentAim.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(AimbotPrefix + "SilentAim", _silentAim.Checked));
		_smoothness.ValueChanged += (_, _) => UpdateControl(
			() => _document.SetDecimal(AimbotPrefix + "Smoothness", _smoothness.Value));
		_maximumDistance.ValueChanged += (_, _) => UpdateControl(
			() => _document.SetDecimal(AimbotPrefix + "MaximumDistance", _maximumDistance.Value));
		_fovRadius.ValueChanged += (_, _) => UpdateControl(
			() => _document.SetDecimal(AimbotPrefix + "FovRadius", _fovRadius.Value));

		_playersEnabled.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(PlayersPrefix + "Enabled", _playersEnabled.Checked));
		_showBoxes.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(PlayersPrefix + "ShowBoxes", _showBoxes.Checked));
		_xrayVision.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(PlayersPrefix + "XRayVision", _xrayVision.Checked));
		_showCharms.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(PlayersPrefix + "ShowCharms", _showCharms.Checked));
		_useRendererBounds.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(PlayersPrefix + "UseRendererBounds", _useRendererBounds.Checked));
		_clampBoxToScope.CheckedChanged += (_, _) => UpdateControl(
			() => _document.SetBool(PlayersPrefix + "ClampBoxToScope", _clampBoxToScope.Checked));
		_boxThickness.ValueChanged += (_, _) => UpdateControl(
			() => _document.SetDecimal(PlayersPrefix + "BoxThickness", _boxThickness.Value));
		_boxPadding.ValueChanged += (_, _) => UpdateControl(
			() => _document.SetDecimal(PlayersPrefix + "BoxPadding", _boxPadding.Value));
		_espMaximumDistance.ValueChanged += (_, _) => UpdateControl(
			() => _document.SetDecimal(PlayersPrefix + "MaximumDistance", _espMaximumDistance.Value));
	}

	private void LoadDocument(string path)
	{
		try
		{
			_pathBox.Text = Path.GetFullPath(path);
			_document = IniDocument.Load(_pathBox.Text);
			RefreshViews();
			_statusText.Text = File.Exists(_pathBox.Text)
				? $"已加载：{_pathBox.Text}"
				: "文件尚不存在，保存时会创建。";
			_dirty = false;
		}
		catch (Exception exception)
		{
			MessageBox.Show(this, exception.Message, "无法加载配置", MessageBoxButtons.OK, MessageBoxIcon.Error);
			_document = IniDocument.Empty();
			RefreshViews();
		}
	}

	private void RefreshViews()
	{
		_loading = true;
		try
		{
			_rawText.Text = _document.Text;
			RefreshControls();
			RefreshGrid();
			_dirty = false;
		}
		finally
		{
			_loading = false;
		}
	}

	private void RefreshControls()
	{
		var targetPart = _document.GetString(AimbotPrefix + "TargetPart", "Head");
		var targetChoice = _aimPart.Items
			.OfType<PartChoice>()
			.FirstOrDefault(choice => choice.Value.Equals(targetPart, StringComparison.OrdinalIgnoreCase));
		if (targetChoice == null)
		{
			targetChoice = new PartChoice(targetPart, targetPart);
			_aimPart.Items.Add(targetChoice);
		}
		_aimPart.SelectedItem = targetChoice;

		_preferHeadChest.Checked = _document.GetBool(AimbotPrefix + "PreferHeadChest", true);
		_visibleOnly.Checked = _document.GetBool(AimbotPrefix + "VisibleOnly", true);
		_silentAim.Checked = _document.GetBool(AimbotPrefix + "SilentAim", false);
		_smoothness.Value = Clamp(_document.GetDecimal(AimbotPrefix + "Smoothness", 0.35m), _smoothness);
		_maximumDistance.Value = Clamp(_document.GetDecimal(AimbotPrefix + "MaximumDistance", 200m), _maximumDistance);
		_fovRadius.Value = Clamp(_document.GetDecimal(AimbotPrefix + "FovRadius", 50m), _fovRadius);

		_playersEnabled.Checked = _document.GetBool(PlayersPrefix + "Enabled", true);
		_showBoxes.Checked = _document.GetBool(PlayersPrefix + "ShowBoxes", true);
		_xrayVision.Checked = _document.GetBool(PlayersPrefix + "XRayVision", true);
		_showCharms.Checked = _document.GetBool(PlayersPrefix + "ShowCharms", true);
		_useRendererBounds.Checked = _document.GetBool(PlayersPrefix + "UseRendererBounds", true);
		_clampBoxToScope.Checked = _document.GetBool(PlayersPrefix + "ClampBoxToScope", true);
		_boxThickness.Value = Clamp(_document.GetDecimal(PlayersPrefix + "BoxThickness", 1m), _boxThickness);
		_boxPadding.Value = Clamp(_document.GetDecimal(PlayersPrefix + "BoxPadding", 0.08m), _boxPadding);
		_espMaximumDistance.Value = Clamp(_document.GetDecimal(PlayersPrefix + "MaximumDistance", 0m), _espMaximumDistance);
	}

	private void RefreshGrid()
	{
		_allSettings.Rows.Clear();
		foreach (var (key, value) in _document.GetEntries())
			_allSettings.Rows.Add(key, value);
	}

	private void UpdateControl(Action update)
	{
		if (_loading)
			return;

		update();
		_rawText.Text = _document.Text;
		RefreshGrid();
		MarkDirty();
	}

	private void OnGridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
	{
		if (_loading || e.RowIndex < 0 || e.ColumnIndex != 1)
			return;

		var key = _allSettings.Rows[e.RowIndex].Cells[0].Value?.ToString();
		if (string.IsNullOrWhiteSpace(key))
			return;

		var value = _allSettings.Rows[e.RowIndex].Cells[1].Value?.ToString() ?? string.Empty;
		_document.Set(key, value);
		_rawText.Text = _document.Text;
		_loading = true;
		try
		{
			RefreshControls();
		}
		finally
		{
			_loading = false;
		}
		MarkDirty();
	}

	private void OnTabChanged(object? sender, EventArgs e)
	{
		if (_loading || _tabs.SelectedIndex == _rawTabIndex || _rawText.Text == _document.Text)
			return;

		_document = IniDocument.FromText(_rawText.Text);
		RefreshViews();
		MarkDirty();
	}

	private void OpenFile()
	{
		if (!ConfirmDiscardChanges())
			return;

		using var dialog = new OpenFileDialog
		{
			Filter = "Trainer configuration (*.ini)|*.ini|All files (*.*)|*.*",
			FileName = Path.GetFileName(_pathBox.Text)
		};
		if (dialog.ShowDialog(this) == DialogResult.OK)
			LoadDocument(dialog.FileName);
	}

	private void SaveFile(bool saveAs = false)
	{
		if (saveAs)
		{
			using var dialog = new SaveFileDialog
			{
				Filter = "Trainer configuration (*.ini)|*.ini|All files (*.*)|*.*",
				FileName = Path.GetFileName(_pathBox.Text)
			};
			if (dialog.ShowDialog(this) != DialogResult.OK)
				return;
			_pathBox.Text = dialog.FileName;
		}

		try
		{
			var path = Path.GetFullPath(_pathBox.Text);
			_document = IniDocument.FromText(_rawText.Text);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);

			if (_createBackup.Checked && File.Exists(path))
				CreateBackup(path);

			var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				File.WriteAllText(temporary, _document.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				File.Move(temporary, path, overwrite: true);
			}
			finally
			{
				if (File.Exists(temporary))
					File.Delete(temporary);
			}

			_dirty = false;
			RefreshViews();
			_statusText.Text = $"已保存：{path}";
		}
		catch (Exception exception)
		{
			MessageBox.Show(this, exception.Message, "无法保存配置", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void CreateManualBackup()
	{
		var path = Path.GetFullPath(_pathBox.Text);
		if (!File.Exists(path))
		{
			MessageBox.Show(this, "配置文件尚不存在。", "备份", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		try
		{
			var backup = CreateBackup(path);
			_statusText.Text = $"已创建备份：{backup}";
		}
		catch (Exception exception)
		{
			MessageBox.Show(this, exception.Message, "无法创建备份", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private static string CreateBackup(string path)
	{
		var backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss-fff}.bak";
		File.Copy(path, backup, overwrite: false);
		return backup;
	}

	private void MarkDirty()
	{
		if (_loading)
			return;

		_dirty = true;
		_statusText.Text = "存在未保存的修改";
	}

	private bool ConfirmDiscardChanges()
	{
		if (!_dirty)
			return true;

		return MessageBox.Show(
			this,
			"放弃未保存的修改？",
			"Trainer 配置编辑器",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Warning) == DialogResult.Yes;
	}

	private void OnFormClosing(object? sender, FormClosingEventArgs e)
	{
		if (!ConfirmDiscardChanges())
			e.Cancel = true;
	}

	private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
	{
		table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		table.Controls.Add(new Label
		{
			Text = label,
			AutoSize = true,
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 8, 12, 8)
		}, 0, row);
		control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
		control.Margin = new Padding(0, 5, 0, 5);
		table.Controls.Add(control, 1, row);
	}

	private static ComboBox MakeCombo(PartChoice[] values)
	{
		var combo = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 260
		};
		combo.Items.AddRange(values);
		combo.SelectedIndex = 0;
		return combo;
	}

	private static CheckBox MakeCheckBox(string text)
	{
		return new CheckBox
		{
			Text = text,
			AutoSize = true
		};
	}

	private static NumericUpDown MakeNumeric(decimal minimum, decimal maximum, decimal increment, int decimals)
	{
		return new NumericUpDown
		{
			Minimum = minimum,
			Maximum = maximum,
			Increment = increment,
			DecimalPlaces = decimals,
			Width = 150
		};
	}

	private static decimal Clamp(decimal value, NumericUpDown control)
	{
		return Math.Max(control.Minimum, Math.Min(control.Maximum, value));
	}

	private static Button MakeButton(string text, int x, int y, int width)
	{
		return new Button
		{
			Text = text,
			Location = new Point(x, y),
			Width = width,
			Height = 28
		};
	}
}
