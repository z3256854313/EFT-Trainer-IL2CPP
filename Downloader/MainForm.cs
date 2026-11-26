using System.Diagnostics;

namespace EftTrainerDownloader;

internal sealed class MainForm : Form
{
	private readonly TextBox _eftRootText = new();
	private readonly Label _statusLabel = new();
	private readonly ProgressBar _progressBar = new();
	private readonly Button _browseButton = new();
	private readonly Button _installButton = new();
	private readonly Button _releaseButton = new();
	private readonly TextBox _logText = new();
	private readonly ReleaseInstaller _installer = new();
	private CancellationTokenSource? _cancellation;

	public MainForm()
	{
		Text = "EFT Trainer 下载器";
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(680, 500);
		Size = new Size(760, 560);
		Font = new Font("Microsoft YaHei UI", 9F);

		var layout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(22),
			ColumnCount = 3,
			RowCount = 8
		};
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		var title = new Label
		{
			Text = "EFT Trainer IL2CPP 下载器",
			AutoSize = true,
			Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
			Margin = new Padding(0, 0, 0, 4)
		};
		layout.Controls.Add(title, 0, 0);
		layout.SetColumnSpan(title, 2);

		var description = new Label
		{
			Text = "自动下载 GitHub 最新版，校验 SHA-256，备份旧 DLL 并安装到 BepInEx。",
			AutoSize = true,
			ForeColor = Color.DimGray,
			Margin = new Padding(0, 0, 0, 18)
		};
		layout.Controls.Add(description, 0, 1);
		layout.SetColumnSpan(description, 2);

		var folderLabel = new Label
		{
			Text = "EFT / SPT 根目录",
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 6)
		};
		layout.Controls.Add(folderLabel, 0, 2);
		layout.SetColumnSpan(folderLabel, 2);

		_eftRootText.Dock = DockStyle.Fill;
		_eftRootText.Margin = new Padding(0, 0, 8, 8);
		layout.Controls.Add(_eftRootText, 0, 3);

		_browseButton.Text = "浏览...";
		_browseButton.Dock = DockStyle.Fill;
		_browseButton.Margin = new Padding(0, 0, 0, 8);
		_browseButton.Click += BrowseButtonOnClick;
		layout.Controls.Add(_browseButton, 1, 3);

		_progressBar.Dock = DockStyle.Fill;
		_progressBar.Height = 20;
		_progressBar.Margin = new Padding(0, 4, 0, 8);
		layout.Controls.Add(_progressBar, 0, 4);
		layout.SetColumnSpan(_progressBar, 2);

		_statusLabel.Text = "准备就绪";
		_statusLabel.AutoSize = true;
		_statusLabel.Margin = new Padding(0, 0, 0, 12);
		layout.Controls.Add(_statusLabel, 0, 5);
		layout.SetColumnSpan(_statusLabel, 2);

		var logLabel = new Label
		{
			Text = "运行日志",
			AutoSize = true,
			Font = new Font(Font, FontStyle.Bold),
			Margin = new Padding(0, 4, 0, 6)
		};

		var logPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = new Padding(0)
		};
		logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
		logPanel.Controls.Add(logLabel, 0, 0);

		_logText.Dock = DockStyle.Fill;
		_logText.Multiline = true;
		_logText.ReadOnly = true;
		_logText.ScrollBars = ScrollBars.Vertical;
		_logText.BackColor = Color.FromArgb(248, 248, 248);
		_logText.Margin = new Padding(0);
		logPanel.Controls.Add(_logText, 0, 1);
		layout.Controls.Add(logPanel, 0, 6);
		layout.SetColumnSpan(logPanel, 2);

		var actionPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft,
			AutoSize = true,
			WrapContents = false,
			Margin = new Padding(0, 12, 0, 0)
		};
		_installButton.Text = "下载并安装最新版";
		_installButton.AutoSize = true;
		_installButton.Padding = new Padding(10, 6, 10, 6);
		_installButton.Click += InstallButtonOnClick;
		actionPanel.Controls.Add(_installButton);

		_releaseButton.Text = "打开 GitHub Releases";
		_releaseButton.AutoSize = true;
		_releaseButton.Padding = new Padding(10, 6, 10, 6);
		_releaseButton.Click += ReleaseButtonOnClick;
		actionPanel.Controls.Add(_releaseButton);
		layout.Controls.Add(actionPanel, 0, 7);
		layout.SetColumnSpan(actionPanel, 2);

		Controls.Add(layout);
		Shown += MainFormOnShown;
		FormClosing += MainFormOnClosing;
	}

	private void MainFormOnShown(object? sender, EventArgs e)
	{
		var detected = ReleaseInstaller.TryAutoDetectEftRoot();
		if (detected != null)
		{
			_eftRootText.Text = detected;
			AppendLog($"已自动找到 EFT / SPT 目录：{detected}");
		}
		else
		{
			AppendLog("没有自动找到 EFT / SPT 目录，请点击“浏览”选择游戏根目录。");
		}
	}

	private void BrowseButtonOnClick(object? sender, EventArgs e)
	{
		using var dialog = new FolderBrowserDialog
		{
			Description = "选择包含 EscapeFromTarkov.exe 和 BepInEx 的 EFT / SPT 根目录",
			UseDescriptionForTitle = true,
			ShowNewFolderButton = false,
			SelectedPath = Directory.Exists(_eftRootText.Text)
				? _eftRootText.Text
				: string.Empty
		};
		if (dialog.ShowDialog(this) == DialogResult.OK)
			_eftRootText.Text = dialog.SelectedPath;
	}

	private async void InstallButtonOnClick(object? sender, EventArgs e)
	{
		if (_cancellation != null)
		{
			_cancellation.Cancel();
			return;
		}

		try
		{
			ReleaseInstaller.ValidateEftRoot(_eftRootText.Text);
		}
		catch (Exception exception)
		{
			MessageBox.Show(
				this,
				exception.Message,
				"无法开始安装",
				MessageBoxButtons.OK,
				MessageBoxIcon.Warning);
			return;
		}

		_cancellation = new CancellationTokenSource();
		SetBusyState(isBusy: true);
		_progressBar.Style = ProgressBarStyle.Continuous;
		_progressBar.Value = 0;
		_statusLabel.Text = "正在下载...";
		_logText.Clear();

		var progress = new Progress<DownloadProgress>(value =>
		{
			if (value.Percent is { } percent)
			{
				_progressBar.Value = percent;
				_statusLabel.Text =
					$"正在下载 {percent}%  ({FormatBytes(value.ReceivedBytes)} / {FormatBytes(value.TotalBytes)})";
			}
			else
			{
				_progressBar.Style = ProgressBarStyle.Marquee;
				_statusLabel.Text = $"正在下载  {FormatBytes(value.ReceivedBytes)}";
			}
		});

		try
		{
			var result = await _installer.InstallLatestAsync(
				_eftRootText.Text,
				progress,
				AppendLog,
				_cancellation.Token);
			_progressBar.Style = ProgressBarStyle.Continuous;
			_progressBar.Value = 100;
			_statusLabel.Text = $"安装完成：{result.Version}";
			AppendLog($"安装完成，版本 {result.Version}。");
			AppendLog("请通过正常的 SPT 启动流程进入游戏，然后按 Right Alt 打开面板。");
			MessageBox.Show(
				this,
				$"EFT Trainer IL2CPP {result.Version} 安装完成。\n\n" +
				"请完全退出游戏、SPT Server 和启动器后重新启动游戏。\n" +
				"默认按 Right Alt 打开面板。",
				"安装成功",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information);
		}
		catch (OperationCanceledException)
		{
			_statusLabel.Text = "已取消";
			AppendLog("操作已取消，没有修改现有插件。");
		}
		catch (Exception exception)
		{
			_statusLabel.Text = "安装失败";
			AppendLog($"错误：{exception}");
			MessageBox.Show(
				this,
				exception.Message,
				"安装失败",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
		}
		finally
		{
			_cancellation.Dispose();
			_cancellation = null;
			SetBusyState(isBusy: false);
		}
	}

	private void ReleaseButtonOnClick(object? sender, EventArgs e)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = ReleaseInstaller.RepositoryUrl + "/releases",
			UseShellExecute = true
		});
	}

	private void SetBusyState(bool isBusy)
	{
		_browseButton.Enabled = !isBusy;
		_eftRootText.Enabled = !isBusy;
		_releaseButton.Enabled = !isBusy;
		_installButton.Text = isBusy ? "取消" : "下载并安装最新版";
	}

	private void AppendLog(string message)
	{
		if (InvokeRequired)
		{
			BeginInvoke(() => AppendLog(message));
			return;
		}

		_logText.AppendText(
			$"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
	}

	private void MainFormOnClosing(object? sender, FormClosingEventArgs e)
	{
		_cancellation?.Cancel();
	}

	private static string FormatBytes(long? bytes)
	{
		if (bytes == null)
			return "大小未知";
		if (bytes < 1024)
			return $"{bytes} B";
		if (bytes < 1024 * 1024)
			return $"{bytes / 1024d:0.0} KB";
		return $"{bytes / 1024d / 1024d:0.0} MB";
	}
}
