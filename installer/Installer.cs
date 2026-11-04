using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
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
        internal static readonly string BackupDirName = "PCFishTool_Backup";
        internal static readonly string MarkerName = "PCFishTool.installed";

        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = false;
            bool uninstall = false;
            bool force = false;
            bool launch = false;
            string gameDir = null;

            foreach (string raw in args)
            {
                string a = raw.Trim();
                if (a == "--silent" || a == "/S") silent = true;
                else if (a == "--uninstall") uninstall = true;
                else if (a == "--force") force = true;
                else if (a == "--launch") launch = true;
                else if (a.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase)) gameDir = a.Substring(6).Trim('"');
            }

            try
            {
                if (uninstall)
                {
                    if (gameDir == null) gameDir = ReadInstalledGame();
                    Uninstall(gameDir);
                    Environment.ExitCode = 0;
                    return;
                }

                if (silent)
                {
                    if (gameDir == null) gameDir = DetectGame();
                    Install(gameDir, force, launch);
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

        internal static void Install(string gameDir, bool force, bool launch)
        {
            if (string.IsNullOrEmpty(gameDir) || !File.Exists(Path.Combine(gameDir, "PCFish.exe")))
                throw new InvalidOperationException("请选择包含 PCFish.exe 的《PC FISH》游戏目录。");
            if (!File.Exists(Path.Combine(gameDir, "GameAssembly.dll")))
                throw new InvalidOperationException("该目录缺少 GameAssembly.dll，确认是 IL2CPP 版《PC FISH》。");

            string versionFile = Path.Combine(gameDir, "version.dll");
            string melonDir = Path.Combine(gameDir, "MelonLoader");
            string dotnetDir = Path.Combine(gameDir, "dotnet");

            if (!force && (File.Exists(versionFile) || Directory.Exists(melonDir)))
            {
                throw new InvalidOperationException(
                    "游戏目录里已经有 version.dll / MelonLoader。\n\n" +
                    "为避免覆盖其它 MelonLoader 插件，安装器已停止。\n" +
                    "确认要覆盖请使用 --force，或先手动移除旧的 MelonLoader。"
                );
            }

            string tempZip = Path.Combine(Path.GetTempPath(), "pcfish-tool-payload-" + Guid.NewGuid().ToString("N") + ".zip");
            string tempDir = Path.Combine(Path.GetTempPath(), "pcfish-tool-payload-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                {
                    if (stream == null) throw new InvalidOperationException("安装包缺少内置 payload.zip。");
                    using (FileStream file = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                        stream.CopyTo(file);
                }

                Directory.CreateDirectory(tempDir);
                ZipFile.ExtractToDirectory(tempZip, tempDir);

                if (force)
                {
                    BackupExisting(gameDir, "version.dll");
                    BackupExisting(gameDir, "MelonLoader");
                    BackupExisting(gameDir, "dotnet");
                }

                CopyFile(Path.Combine(tempDir, "version.dll"), Path.Combine(gameDir, "version.dll"));
                CopyDirectory(Path.Combine(tempDir, "MelonLoader"), Path.Combine(gameDir, "MelonLoader"));
                CopyDirectory(Path.Combine(tempDir, "dotnet"), Path.Combine(gameDir, "dotnet"));

                string modsDir = Path.Combine(gameDir, "Mods");
                Directory.CreateDirectory(modsDir);
                CopyFile(Path.Combine(tempDir, "Mods", "PCFishTool.dll"), Path.Combine(modsDir, "PCFishTool.dll"));

                UpdateConfigHash(gameDir);

                File.WriteAllText(Path.Combine(gameDir, MarkerName), "PCFishTool " + Version + Environment.NewLine, Encoding.UTF8);
                File.WriteAllText(Path.Combine(gameDir, "PCFishTool_README.txt"),
                    ProductName + " " + Version + Environment.NewLine + Environment.NewLine +
                    "安装内容：" + Environment.NewLine +
                    "- MelonLoader 0.7.3" + Environment.NewLine +
                    "- .NET 6 portable runtime (dotnet/)" + Environment.NewLine +
                    "- Pre-generated Il2CppInterop assemblies (MelonLoader/Il2CppAssemblies/)" + Environment.NewLine +
                    "- Mods/PCFishTool.dll" + Environment.NewLine + Environment.NewLine +
                    "启动游戏即可加载插件；无需再运行额外工具。" + Environment.NewLine +
                    "完整卸载：PCFishTool-Setup.exe --uninstall --dir=\"" + gameDir + "\"" + Environment.NewLine,
                    Encoding.UTF8);

                if (launch)
                {
                    try { Process.Start(Path.Combine(gameDir, "PCFish.exe"), ""); }
                    catch { }
                }
            }
            finally
            {
                TryDelete(tempZip);
                TryDeleteDir(tempDir);
            }
        }

        private static void UpdateConfigHash(string gameDir)
        {
            string config = Path.Combine(gameDir, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Config.cfg");
            if (!File.Exists(config)) return;

            string assembly = Path.Combine(gameDir, "GameAssembly.dll");
            using (SHA512 sha = SHA512.Create())
            using (FileStream stream = File.OpenRead(assembly))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                List<string> lines = new List<string>();
                foreach (string line in File.ReadAllLines(config))
                {
                    if (!line.TrimStart().StartsWith("GameAssemblyHash", StringComparison.OrdinalIgnoreCase))
                        lines.Add(line);
                }
                lines.Add("GameAssemblyHash = \"" + hash + "\"");
                File.WriteAllLines(config, lines.ToArray(), new UTF8Encoding(false));
            }
        }

        private static void BackupExisting(string gameDir, string name)
        {
            string source = Path.Combine(gameDir, name);
            if (!File.Exists(source) && !Directory.Exists(source)) return;
            string backupRoot = Path.Combine(gameDir, BackupDirName);
            Directory.CreateDirectory(backupRoot);
            string target = Path.Combine(backupRoot, name);
            if (File.Exists(source))
            {
                if (!File.Exists(target)) File.Copy(source, target);
            }
            else if (Directory.Exists(source) && !Directory.Exists(target))
            {
                CopyDirectory(source, target);
            }
        }

        private static void RestoreExisting(string gameDir, string name)
        {
            string backup = Path.Combine(gameDir, BackupDirName, name);
            string target = Path.Combine(gameDir, name);
            if (File.Exists(backup) && !File.Exists(target)) File.Copy(backup, target);
            else if (Directory.Exists(backup) && !Directory.Exists(target)) CopyDirectory(backup, target);
        }

        internal static void Uninstall(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir) || !File.Exists(Path.Combine(gameDir, MarkerName)))
                throw new InvalidOperationException("没有找到 PCFish Tool 安装标记；请用 --dir= 指定游戏目录。");

            foreach (Process p in Process.GetProcessesByName("PCFish"))
            {
                try { p.Kill(); p.WaitForExit(3000); }
                catch { }
            }

            TryDelete(Path.Combine(gameDir, "Mods", "PCFishTool.dll"));
            TryDelete(Path.Combine(gameDir, "PCFishTool_README.txt"));
            TryDelete(Path.Combine(gameDir, MarkerName));

            TryDeleteDir(Path.Combine(gameDir, "MelonLoader"));
            TryDeleteDir(Path.Combine(gameDir, "dotnet"));
            TryDelete(Path.Combine(gameDir, "version.dll"));

            RestoreExisting(gameDir, "version.dll");
            RestoreExisting(gameDir, "MelonLoader");
            RestoreExisting(gameDir, "dotnet");
        }

        private static string ReadInstalledGame()
        {
            return null;
        }

        private static void CopyFile(string source, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void TryDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
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
                        if (File.Exists(Path.Combine(dir, "PCFish.exe"))) return dir;
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
                if (string.Equals(s, value, StringComparison.OrdinalIgnoreCase)) return;
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
            ClientSize = new Size(660, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);

            Label title = new Label();
            title.Text = Program.ProductName + "  " + Program.Version;
            title.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            title.Location = new Point(20, 16);
            title.Size = new Size(600, 36);
            Controls.Add(title);

            Label desc = new Label();
            desc.Text = "MelonLoader 插件版：安装后启动游戏自动加载（自动分组 + 结果动画跳过）";
            desc.Location = new Point(22, 56);
            desc.Size = new Size(610, 24);
            Controls.Add(desc);

            Label note = new Label();
            note.Text = "会安装 MelonLoader、.NET 6 运行时、预生成的 Il2CppInterop 程序集和 Mods/PCFishTool.dll；需要管理员权限。";
            note.ForeColor = Color.DimGray;
            note.Location = new Point(22, 80);
            note.Size = new Size(610, 40);
            Controls.Add(note);

            Label gameLabel = new Label();
            gameLabel.Text = "游戏目录（包含 PCFish.exe）：";
            gameLabel.Location = new Point(22, 126);
            gameLabel.Size = new Size(220, 22);
            Controls.Add(gameLabel);

            _gamePath = new TextBox();
            _gamePath.Location = new Point(22, 150);
            _gamePath.Size = new Size(510, 24);
            _gamePath.Text = detectedGame ?? "";
            Controls.Add(_gamePath);

            Button browse = new Button();
            browse.Text = "浏览...";
            browse.Location = new Point(540, 148);
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
            _launch.Text = "安装完成后启动游戏";
            _launch.Location = new Point(22, 186);
            _launch.Size = new Size(240, 24);
            _launch.Checked = true;
            Controls.Add(_launch);

            Button install = new Button();
            install.Text = "安装 / 更新";
            install.Location = new Point(22, 220);
            install.Size = new Size(130, 36);
            install.Click += delegate { DoInstall(); };
            Controls.Add(install);

            Button uninstall = new Button();
            uninstall.Text = "卸载";
            uninstall.Location = new Point(164, 220);
            uninstall.Size = new Size(100, 36);
            uninstall.Click += delegate { DoUninstall(); };
            Controls.Add(uninstall);

            _status = new Label();
            _status.Text = "安装到游戏目录；不会动存档。";
            _status.Location = new Point(22, 272);
            _status.Size = new Size(610, 48);
            _status.ForeColor = Color.DimGray;
            Controls.Add(_status);
        }

        private void DoInstall()
        {
            try
            {
                string game = _gamePath.Text.Trim();
                if (game.Length == 0 || !File.Exists(Path.Combine(game, "PCFish.exe")))
                {
                    MessageBox.Show(this, "请选择包含 PCFish.exe 的游戏目录。", Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bool force = false;
                if (File.Exists(Path.Combine(game, "version.dll")) || Directory.Exists(Path.Combine(game, "MelonLoader")))
                {
                    DialogResult r = MessageBox.Show(this,
                        "游戏目录里已有 version.dll / MelonLoader。\n继续会备份并覆盖现有 MelonLoader，确定吗？",
                        Program.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) return;
                    force = true;
                }

                Cursor.Current = Cursors.WaitCursor;
                Program.Install(game, force, false);
                Cursor.Current = Cursors.Default;
                _status.Text = "安装完成。启动游戏即可自动加载插件。";
                if (_launch.Checked)
                {
                    try { Process.Start(Path.Combine(game, "PCFish.exe"), ""); }
                    catch { }
                }
                MessageBox.Show(this, "安装完成。\n\n启动游戏后 MelonLoader 会加载 Mods/PCFishTool.dll。", Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                MessageBox.Show(this, ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoUninstall()
        {
            if (MessageBox.Show(this, "确定卸载 PCFish Tool？\n会移除 MelonLoader 相关文件和 Mods/PCFishTool.dll。", Program.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                Program.Uninstall(_gamePath.Text.Trim());
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

