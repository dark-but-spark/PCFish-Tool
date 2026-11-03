using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCFishToolSetup
{
    internal static class Program
    {
        internal const string ProductName = "PCFish Tool";
        internal const string Version = "__VERSION__";
        internal static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCFishTool");

        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = false;
            bool uninstall = false;
            bool noLaunch = false;
            string gameDir = null;

            foreach (string raw in args)
            {
                string a = raw.Trim();
                if (a == "--silent" || a == "/S") silent = true;
                else if (a == "--uninstall") uninstall = true;
                else if (a == "--no-launch") noLaunch = true;
                else if (a.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase)) gameDir = a.Substring(6).Trim('"');
            }

            try
            {
                if (uninstall)
                {
                    Uninstall();
                    Environment.ExitCode = 0;
                    return;
                }

                if (silent)
                {
                    Install(gameDir, noLaunch);
                    Environment.ExitCode = 0;
                    return;
                }
            }
            catch (Exception ex)
            {
                if (silent)
                {
                    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "PCFishToolSetup.log"), ex + Environment.NewLine); }
                    catch { }
                    Environment.ExitCode = 1;
                    return;
                }
                MessageBox.Show(ex.Message, ProductName + " Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1;
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(gameDir ?? DetectGame()));
        }

        private static void Install(string gameDir, bool noLaunch)
        {
            Directory.CreateDirectory(InstallDir);
            ExtractResource("PCFishTool.exe", Path.Combine(InstallDir, "PCFishTool.exe"));
            ExtractResource("pcfish_autogroup.js", Path.Combine(InstallDir, "pcfish_autogroup.js"));

            string readme =
                ProductName + " " + Version + Environment.NewLine + Environment.NewLine +
                "使用步骤：" + Environment.NewLine +
                "1. 先启动《PC FISH》，进入鱼缸/主界面。" + Environment.NewLine +
                "2. 运行 PCFishTool.exe（桌面/开始菜单快捷方式）。" + Environment.NewLine +
                "3. 合成界面先放一条样板鱼，再点“自动添加”。" + Environment.NewLine + Environment.NewLine +
                "配置：编辑 pcfish_autogroup.js 顶部的 CFG。" + Environment.NewLine +
                "skipResultFx = true 时，服务器响应后立即结束结果动画。" + Environment.NewLine + Environment.NewLine +
                "本工具为个人研究用途，与 NANOO COMPANY 无关。" + Environment.NewLine;
            File.WriteAllText(Path.Combine(InstallDir, "README.txt"), readme, Encoding.UTF8);

            if (!string.IsNullOrEmpty(gameDir))
            {
                File.WriteAllText(Path.Combine(InstallDir, "gamepath.txt"), gameDir, Encoding.UTF8);
            }

            string exe = Path.Combine(InstallDir, "PCFishTool.exe");
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ProductName + ".lnk"), exe);
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk"), exe);

            if (!noLaunch)
            {
                try { Process.Start(exe); }
                catch { }
            }
        }

        private static void Uninstall()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("PCFishTool"))
                {
                    try { p.Kill(); p.WaitForExit(3000); }
                    catch { }
                }
            }
            catch { }

            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ProductName + ".lnk"));
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk"));

            if (Directory.Exists(InstallDir))
            {
                try { Directory.Delete(InstallDir, true); }
                catch (Exception ex) { throw new IOException("无法删除安装目录：" + InstallDir + " — " + ex.Message); }
            }
        }

        internal static void InstallForUi(string gameDir, bool noLaunch)
        {
            Install(gameDir, noLaunch);
        }

        internal static void UninstallForUi()
        {
            Uninstall();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void ExtractResource(string name, string target)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("缺少内置文件: " + name);
                using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(output);
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { InstallDir });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { ProductName + " - " + Version });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static string DetectGame()
        {
            List<string> roots = new List<string>();
            AddRegistryPath(roots, @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");
            AddRegistryPath(roots, @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            roots.Add(@"C:\Program Files (x86)\Steam");
            roots.Add(@"C:\Program Files\Steam");

            List<string> libraries = new List<string>();
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                AddUnique(libraries, root);
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                try
                {
                    string text = File.ReadAllText(vdf);
                    foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                    {
                        string path = m.Groups[1].Value.Replace("\\\\", "\\");
                        if (Directory.Exists(path)) AddUnique(libraries, path);
                    }
                }
                catch { }
            }

            foreach (string lib in libraries)
            {
                string common = Path.Combine(lib, "steamapps", "common");
                if (!Directory.Exists(common)) continue;
                try
                {
                    foreach (string dir in Directory.GetDirectories(common))
                    {
                        string exe = Path.Combine(dir, "PCFish.exe");
                        if (File.Exists(exe)) return dir;
                    }
                }
                catch { }
            }
            return "";
        }

        private static void AddRegistryPath(List<string> list, string key, string value)
        {
            try
            {
                object v = Registry.GetValue(key, value, null);
                string s = v as string;
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            catch { }
        }

        private static void AddUnique(List<string> list, string value)
        {
            foreach (string s in list)
            {
                if (string.Equals(s, value, StringComparison.OrdinalIgnoreCase)) return;
            }
            list.Add(value);
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox _gamePath;
        private readonly Label _status;
        private readonly CheckBox _launch;

        public SetupForm(string detectedGame)
        {
            Text = Program.ProductName + " Setup " + Program.Version;
            ClientSize = new Size(620, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);

            Label title = new Label();
            title.Text = Program.ProductName + "  " + Program.Version;
            title.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            title.Location = new Point(20, 18);
            title.Size = new Size(560, 36);
            Controls.Add(title);

            Label desc = new Label();
            desc.Text = "《PC FISH》标准合成自动分组 + 结果动画跳过（服务器响应后立即显示）";
            desc.Location = new Point(22, 58);
            desc.Size = new Size(570, 24);
            Controls.Add(desc);

            Label gameLabel = new Label();
            gameLabel.Text = "游戏目录（用于检测，可留空）：";
            gameLabel.Location = new Point(22, 100);
            gameLabel.Size = new Size(220, 22);
            Controls.Add(gameLabel);

            _gamePath = new TextBox();
            _gamePath.Location = new Point(22, 124);
            _gamePath.Size = new Size(470, 24);
            _gamePath.Text = detectedGame ?? "";
            Controls.Add(_gamePath);

            Button browse = new Button();
            browse.Text = "浏览...";
            browse.Location = new Point(500, 122);
            browse.Size = new Size(90, 28);
            browse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择 PC FISH 目录（包含 PCFish.exe）";
                    if (Directory.Exists(_gamePath.Text)) dialog.SelectedPath = _gamePath.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) _gamePath.Text = dialog.SelectedPath;
                }
            };
            Controls.Add(browse);

            _launch = new CheckBox();
            _launch.Text = "安装完成后运行 PCFishTool（需先启动游戏）";
            _launch.Location = new Point(22, 160);
            _launch.Size = new Size(400, 24);
            _launch.Checked = true;
            Controls.Add(_launch);

            Button install = new Button();
            install.Text = "安装 / 更新";
            install.Location = new Point(22, 200);
            install.Size = new Size(130, 36);
            install.Click += delegate { DoInstall(); };
            Controls.Add(install);

            Button uninstall = new Button();
            uninstall.Text = "卸载";
            uninstall.Location = new Point(164, 200);
            uninstall.Size = new Size(100, 36);
            uninstall.Click += delegate { DoUninstall(); };
            Controls.Add(uninstall);

            _status = new Label();
            _status.Text = "安装到：" + Program.InstallDir;
            _status.Location = new Point(22, 252);
            _status.Size = new Size(570, 36);
            _status.ForeColor = Color.DimGray;
            Controls.Add(_status);
        }

        private void DoInstall()
        {
            try
            {
                string game = _gamePath.Text.Trim();
                if (game.Length > 0 && !File.Exists(Path.Combine(game, "PCFish.exe")))
                {
                    DialogResult r = MessageBox.Show(this, "该目录下没有 PCFish.exe，仍要安装吗？", Program.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) return;
                }
                Program.InstallForUi(game, !_launch.Checked);
                _status.Text = "安装完成：" + Program.InstallDir;
                MessageBox.Show(this, "安装完成。\n\n请先启动游戏，再运行桌面/开始菜单的 PCFish Tool。", Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoUninstall()
        {
            if (MessageBox.Show(this, "确定卸载 PCFish Tool？", Program.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                Program.UninstallForUi();
                _status.Text = "已卸载。";
                MessageBox.Show(this, "卸载完成。", Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

