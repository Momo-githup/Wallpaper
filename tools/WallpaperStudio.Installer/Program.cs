using System.Diagnostics;
using System.Reflection;

namespace WallpaperStudio.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--update", StringComparer.OrdinalIgnoreCase))
            return RunUpdateModeAsync(args).GetAwaiter().GetResult();

        Application.Run(new InstallerForm());
        return 0;
    }

    private static async Task<int> RunUpdateModeAsync(string[] args)
    {
        try
        {
            var installPath = GetOption(args, "--installto")
                ?? throw new ArgumentException("更新命令缺少安装目录。");
            var processIdText = GetOption(args, "--waitpid")
                ?? throw new ArgumentException("更新命令缺少进程编号。");
            if (!int.TryParse(processIdText, out var processId))
                throw new ArgumentException("更新进程编号无效。");

            try
            {
                using var runningApp = Process.GetProcessById(processId);
                await runningApp.WaitForExitAsync();
            }
            catch (ArgumentException)
            {
                // 应用已退出，可以直接更新。
            }

            await InstallerEngine.InstallAsync(installPath, silent: true);
            if (args.Contains("--restart", StringComparer.OrdinalIgnoreCase))
                InstallerEngine.RestartInstalledApplication(installPath);
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show($"自动更新失败：{exception.Message}\n\n请从 GitHub Releases 手动下载安装程序。",
                "Wallpaper Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }
        return null;
    }
}

internal static class InstallerEngine
{
    private const string SetupResourceName = "WallpaperStudio.Setup.exe";

    internal static async Task InstallAsync(string targetPath, bool silent)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "WallpaperStudioInstaller", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var setupPath = Path.Combine(temporaryDirectory, "WallpaperStudio-Setup.exe");
            await using (var output = File.Create(setupPath))
            await using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(SetupResourceName)
                ?? throw new InvalidOperationException("安装包内容不完整，请重新下载。"))
            {
                await payload.CopyToAsync(output);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = setupPath,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--installto");
            startInfo.ArgumentList.Add(Path.GetFullPath(targetPath));
            if (silent) startInfo.ArgumentList.Add("--silent");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动安装程序。");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"安装程序返回错误代码 {process.ExitCode}。");
        }
        finally
        {
            try { Directory.Delete(temporaryDirectory, recursive: true); }
            catch { /* 安装器退出时由系统临时目录清理残留文件。 */ }
        }
    }

    internal static void RestartInstalledApplication(string installPath)
    {
        var root = Path.GetFullPath(installPath);
        var preferredNames = new[] { "Wallpaper Studio.exe", "WallpaperStudio.App.exe" };
        var launcher = preferredNames.Select(name => Path.Combine(root, name)).FirstOrDefault(File.Exists)
            ?? Directory.EnumerateFiles(root, "*.exe", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => !Path.GetFileName(path).Equals("Update.exe", StringComparison.OrdinalIgnoreCase));
        if (launcher is null)
            throw new FileNotFoundException("更新完成，但没有找到应用启动程序。", root);

        Process.Start(new ProcessStartInfo { FileName = launcher, UseShellExecute = true });
    }
}

internal sealed class InstallerForm : Form
{
    private readonly TextBox _installPath = new();
    private readonly Button _browseButton = new();
    private readonly Button _installButton = new();
    private readonly Label _statusLabel = new();
    private bool _installing;

    public InstallerForm()
    {
        Text = "安装 Wallpaper Studio";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 310);
        MinimumSize = new Size(620, 349);
        MaximizeBox = false;
        BackColor = Color.FromArgb(247, 247, 252);
        Font = new Font("Microsoft YaHei UI", 9F);

        var title = new Label
        {
            Text = "Wallpaper Studio",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold),
            ForeColor = Color.FromArgb(47, 43, 67),
            Location = new Point(30, 27)
        };
        var subtitle = new Label
        {
            Text = "选择安装位置，然后开始安装",
            AutoSize = true,
            ForeColor = Color.FromArgb(101, 96, 121),
            Location = new Point(33, 73)
        };
        var pathLabel = new Label
        {
            Text = "安装路径",
            AutoSize = true,
            ForeColor = Color.FromArgb(47, 43, 67),
            Location = new Point(33, 116)
        };

        _installPath.Location = new Point(36, 141);
        _installPath.Size = new Size(446, 28);
        _installPath.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperStudio.Desktop");

        _browseButton.Text = "浏览...";
        _browseButton.Location = new Point(493, 139);
        _browseButton.Size = new Size(92, 31);
        _browseButton.Click += BrowseButton_Click;

        var hint = new Label
        {
            Text = "如已安装旧版本，请先在 Windows“已安装的应用”中卸载，再更换安装路径。用户壁纸和设置不会被删除。",
            AutoSize = false,
            Size = new Size(550, 40),
            ForeColor = Color.FromArgb(112, 107, 130),
            Location = new Point(36, 181)
        };

        _statusLabel.AutoSize = false;
        _statusLabel.Size = new Size(390, 31);
        _statusLabel.Location = new Point(36, 248);
        _statusLabel.ForeColor = Color.FromArgb(101, 96, 121);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        _installButton.Text = "开始安装";
        _installButton.Location = new Point(444, 242);
        _installButton.Size = new Size(141, 40);
        _installButton.BackColor = Color.FromArgb(124, 92, 252);
        _installButton.ForeColor = Color.White;
        _installButton.FlatStyle = FlatStyle.Flat;
        _installButton.FlatAppearance.BorderSize = 0;
        _installButton.Click += InstallButton_Click;

        Controls.AddRange([title, subtitle, pathLabel, _installPath, _browseButton, hint, _statusLabel, _installButton]);
        AcceptButton = _installButton;
        FormClosing += (_, args) =>
        {
            if (!_installing) return;
            args.Cancel = true;
            MessageBox.Show(this, "安装正在进行，请稍候。", "Wallpaper Studio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 Wallpaper Studio 的安装文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(_installPath.Text) ? _installPath.Text : string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _installPath.Text = dialog.SelectedPath;
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        string targetPath;
        try
        {
            targetPath = ValidateInstallPath(_installPath.Text);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "安装路径无效",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _installing = true;
        SetControlsEnabled(false);
        _statusLabel.Text = "正在安装，请勿关闭窗口...";
        try
        {
            await InstallerEngine.InstallAsync(targetPath, silent: false);
            _installing = false;
            MessageBox.Show(this, "Wallpaper Studio 已安装完成。", "安装完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception exception)
        {
            _installing = false;
            _statusLabel.Text = "安装未完成";
            SetControlsEnabled(true);
            MessageBox.Show(this, $"安装失败：{exception.Message}", "Wallpaper Studio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string ValidateInstallPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("请选择安装路径。");
        if (!Path.IsPathFullyQualified(value))
            throw new ArgumentException("请输入完整的安装路径，例如 D:\\Apps\\WallpaperStudio。");

        var fullPath = Path.GetFullPath(value.Trim());
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar),
                root?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("不能直接安装到磁盘根目录，请选择一个子文件夹。");
        return fullPath.TrimEnd(Path.DirectorySeparatorChar);
    }

    private void SetControlsEnabled(bool enabled)
    {
        _installPath.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _installButton.Enabled = enabled;
    }
}
