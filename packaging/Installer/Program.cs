using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace YiZhiMao.Dc.Installer
{
    internal static class Program
    {
        private const string ProductName = "一只猫dc";
        private const string BundleName = "YiZhiMaoDc.bundle";
        private const string PayloadResource = "YiZhiMaoDc.Payload.zip";
        private const string UninstallKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\YiZhiMaoDc";

        [STAThread]
        private static int Main(string[] args)
        {
            using (var mutex = new Mutex(false, @"Local\YiZhiMaoDcInstaller"))
            {
                bool ownsMutex = false;
                try
                {
                    ownsMutex = TryAcquireInstallerMutex(
                        mutex,
                        HasArgument(args, "--elevated") ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
                    if (!ownsMutex)
                    {
                        return Fail("另一个一只猫dc安装程序正在运行。", silent: IsSelfTest(args));
                    }

                    if (IsSelfTest(args))
                    {
                        ExtractAndValidate(args[1]);
                        File.WriteAllText(
                            Path.Combine(args[1], "selftest-result.txt"),
                            "SELFTEST_OK");
                        return 0;
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    if (args.Length > 0 &&
                        (string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase)))
                    {
                        InstallationScope uninstallScope = HasArgument(args, "--all-users")
                            ? InstallationScope.AllUsers
                            : InstallationScope.CurrentUser;
                        return Uninstall(uninstallScope);
                    }

                    InstallationScope initialScope = HasArgument(args, "--all-users")
                        ? InstallationScope.AllUsers
                        : InstallationScope.CurrentUser;
                    return Install(initialScope, HasArgument(args, "--elevated"));
                }
                catch (Exception ex)
                {
                    return Fail(ex.Message, silent: IsSelfTest(args));
                }
                finally
                {
                    if (ownsMutex)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private static bool TryAcquireInstallerMutex(Mutex mutex, TimeSpan timeout)
        {
            try
            {
                return mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }

        private static bool HasArgument(IEnumerable<string> args, string expected)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSelfTest(IReadOnlyList<string> args)
        {
            return args.Count >= 2 &&
                   string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase);
        }

        private static int Install(InstallationScope initialScope, bool startAtConfirmation)
        {
            string version = GetVersion();
            using (var wizard = new InstallerWizardForm(
                       ProductName,
                       version,
                       GetInstalledBundlePath(InstallationScope.CurrentUser),
                       GetInstalledBundlePath(InstallationScope.AllUsers),
                       initialScope,
                       startAtConfirmation,
                       PerformInstall))
            {
                Application.Run(wizard);
                return wizard.ExitCode;
            }
        }

        private static InstallationOutcome PerformInstall(
            InstallationScope scope,
            Action<int, string> reportProgress)
        {
            if (scope == InstallationScope.AllUsers && !IsAdministrator())
            {
                string elevationError;
                if (TryRelaunchAsAdministrator(out elevationError))
                {
                    return new InstallationOutcome { Relaunched = true };
                }

                return new InstallationOutcome { Error = elevationError };
            }

            using (InstallerLog log = InstallerLog.Create("install"))
            {
                log.Info("开始安装；范围=" + scope);
                reportProgress(5, "正在检查 AutoCAD 运行状态…");
                if (!EnsureAutoCadClosed("安装"))
                {
                    log.Info("用户取消安装或未关闭 AutoCAD。");
                    return new InstallationOutcome { Cancelled = true, LogPath = log.LogPath };
                }

                reportProgress(15, "正在准备安装目录…");
                string version = GetVersion();
                string applicationPlugins = GetApplicationPluginsPath(scope);
                string targetBundle = Path.Combine(applicationPlugins, BundleName);
                string stagingRoot = Path.Combine(
                    applicationPlugins,
                    ".YiZhiMaoDc-installing-" + Guid.NewGuid().ToString("N"));
                string backupBundle = Path.Combine(
                    applicationPlugins,
                    "YiZhiMaoDc-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 8));
                bool existingMoved = false;
                bool newActivated = false;
                var mergeResult = new ResourceMergeResult();

                try
                {
                    Directory.CreateDirectory(applicationPlugins);
                    log.Info("目标目录=" + targetBundle);
                    reportProgress(35, "正在解压并校验安装文件…");
                    string extractedBundle = ExtractAndValidate(stagingRoot);
                    log.Info("安装文件校验通过。");
                    if (Directory.Exists(targetBundle))
                    {
                        reportProgress(60, "正在备份旧版本…");
                        Directory.Move(targetBundle, backupBundle);
                        existingMoved = true;
                        log.Info("旧版本备份=" + backupBundle);
                    }

                    reportProgress(72, "正在启用新版本…");
                    Directory.Move(extractedBundle, targetBundle);
                    newActivated = true;

                    if (existingMoved)
                    {
                        reportProgress(82, "正在恢复用户自定义资源…");
                        mergeResult = InstallerFileOperations.MergeUserResources(backupBundle, targetBundle);
                        log.Info(
                            "用户资源恢复：新增=" + mergeResult.AddedCount +
                            "，修改=" + mergeResult.ModifiedRestoredCount +
                            "，同名冲突=" + mergeResult.ConflictCount);
                    }

                    reportProgress(92, "正在注册卸载入口…");
                    RegisterUninstaller(version, scope);
                    log.Info("卸载入口注册完成。");
                }
                catch (Exception ex)
                {
                    log.Error("安装失败：" + ex);
                    try
                    {
                        RollbackInstallation(targetBundle, backupBundle, existingMoved, newActivated, log);
                    }
                    catch (Exception rollbackError)
                    {
                        log.Error("回滚失败：" + rollbackError);
                        return new InstallationOutcome
                        {
                            Error = "安装失败，并且自动恢复旧版本失败。请保留备份并查看日志：" +
                                    log.LogPath + "\r\n\r\n" + rollbackError.Message,
                            LogPath = log.LogPath
                        };
                    }

                    return new InstallationOutcome
                    {
                        Error = "安装失败，已自动恢复到安装前状态。\r\n\r\n" + ex.Message +
                                (string.IsNullOrEmpty(log.LogPath)
                                    ? string.Empty
                                    : "\r\n\r\n安装日志：" + log.LogPath),
                        LogPath = log.LogPath
                    };
                }
                finally
                {
                    TryDeleteDirectory(stagingRoot, log);
                }

                try
                {
                    reportProgress(100, "安装完成");
                }
                catch (Exception ex)
                {
                    log.Error("完成页进度更新失败，但安装已完成：" + ex.Message);
                }

                log.Info("安装完成。");
                return new InstallationOutcome
                {
                    Success = true,
                    InstallPath = targetBundle,
                    BackupPath = existingMoved ? backupBundle : string.Empty,
                    PreservedResourceCount = mergeResult.PreservedCount,
                    ResourceConflictCount = mergeResult.ConflictCount,
                    LogPath = log.LogPath
                };
            }
        }

        private static int Uninstall(InstallationScope scope)
        {
            if (scope == InstallationScope.AllUsers && !IsAdministrator())
            {
                string elevationError;
                if (TryRelaunchAsAdministrator(out elevationError, uninstall: true))
                {
                    return 0;
                }

                return Fail(elevationError, false);
            }

            string targetBundle = GetInstalledBundlePath(scope);
            bool deleteUserData;
            using (var options = new UninstallOptionsDialog(targetBundle))
            {
                if (options.ShowDialog() != DialogResult.OK)
                {
                    return 0;
                }

                deleteUserData = options.DeleteUserData;
            }

            if (deleteUserData)
            {
                DialogResult confirmation = MessageBox.Show(
                    "这会永久删除当前 Windows 用户的一只猫dc配置、快捷键、打印设置和自定义资源，无法恢复。\r\n\r\n确认继续吗？",
                    ProductName + " 卸载程序 - 确认删除用户数据",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (confirmation != DialogResult.Yes)
                {
                    return 0;
                }
            }

            if (!EnsureAutoCadClosed("卸载"))
            {
                return 0;
            }

            using (InstallerLog log = InstallerLog.Create("uninstall"))
            {
                log.Info("开始卸载；范围=" + scope + "；删除用户数据=" + deleteUserData);
                string preservedPath = string.Empty;
                int preservedCount = 0;
                string operationId = Guid.NewGuid().ToString("N");
                string bundleBackup = targetBundle + ".uninstall-" + operationId;
                string userDataPath = GetUserDataPath();
                string userDataBackup = userDataPath + ".uninstall-" + operationId;
                bool bundleMoved = false;
                bool userDataMoved = false;
                RegistryKey registryRoot = scope == InstallationScope.AllUsers
                    ? Registry.LocalMachine
                    : Registry.CurrentUser;
                RegistryKeySnapshot registrySnapshot = CaptureRegistryKey(registryRoot);
                try
                {
                    if (!deleteUserData && Directory.Exists(targetBundle))
                    {
                        preservedPath = Path.Combine(
                            GetUserDataPath(),
                            "PreservedResources",
                            DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
                            Guid.NewGuid().ToString("N").Substring(0, 8));
                        ResourceExportResult export = InstallerFileOperations.ExportUserResources(
                            targetBundle,
                            preservedPath);
                        preservedCount = export.PreservedCount;
                        log.Info("保留用户资源=" + preservedCount + "；位置=" + preservedPath);
                    }

                    if (Directory.Exists(targetBundle))
                    {
                        Directory.Move(targetBundle, bundleBackup);
                        bundleMoved = true;
                    }

                    if (deleteUserData && Directory.Exists(userDataPath))
                    {
                        Directory.Move(userDataPath, userDataBackup);
                        userDataMoved = true;
                    }

                    registryRoot.DeleteSubKeyTree(UninstallKeyPath, false);
                    log.Info("卸载完成。");
                }
                catch (Exception ex)
                {
                    log.Error("卸载失败：" + ex);
                    try
                    {
                        RestoreRegistryKey(registryRoot, registrySnapshot);
                        if (userDataMoved && !Directory.Exists(userDataPath) && Directory.Exists(userDataBackup))
                        {
                            Directory.Move(userDataBackup, userDataPath);
                        }

                        if (bundleMoved && !Directory.Exists(targetBundle) && Directory.Exists(bundleBackup))
                        {
                            Directory.Move(bundleBackup, targetBundle);
                        }

                        log.Info("卸载失败后已恢复到卸载前状态。");
                    }
                    catch (Exception rollbackError)
                    {
                        log.Error("卸载回滚失败：" + rollbackError);
                        return Fail(
                            "卸载失败，并且自动恢复失败。请查看日志：" + log.LogPath +
                            "\r\n\r\n" + rollbackError.Message,
                            false);
                    }

                    return Fail(
                        "卸载失败，已恢复到卸载前状态：" + ex.Message +
                        (string.IsNullOrEmpty(log.LogPath)
                            ? string.Empty
                            : "\r\n\r\n卸载日志：" + log.LogPath),
                        false);
                }

                bool bundleCleanupSucceeded = TryDeleteDirectory(bundleBackup, log);
                bool userDataCleanupSucceeded = TryDeleteDirectory(userDataBackup, log);
                string cleanupWarning = bundleCleanupSucceeded && userDataCleanupSucceeded
                    ? string.Empty
                    : "\r\n\r\n注意：部分已停用的卸载临时备份未能删除，详情请查看日志。";

                string preservedText = deleteUserData
                    ? "当前用户配置和自定义资源已删除。"
                    : preservedCount > 0
                        ? "已保留 " + preservedCount + " 项用户资源：\r\n" + preservedPath
                        : "用户配置已保留；未检测到需要单独备份的自定义资源。";
                MessageBox.Show(
                    "插件已卸载。\r\n\r\n" + preservedText +
                    cleanupWarning +
                    (string.IsNullOrEmpty(log.LogPath)
                        ? string.Empty
                        : "\r\n\r\n卸载日志：\r\n" + log.LogPath),
                    ProductName + " 卸载程序",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }
        }

        private static string ExtractAndValidate(string destinationRoot)
        {
            string fullDestination = Path.GetFullPath(destinationRoot);
            if (Path.GetPathRoot(fullDestination) == fullDestination)
            {
                throw new InvalidOperationException("拒绝把安装内容解压到磁盘根目录。");
            }

            if (Directory.Exists(fullDestination) &&
                Directory.GetFileSystemEntries(fullDestination).Length > 0)
            {
                throw new InvalidOperationException("安装临时目录不是空目录。");
            }

            Directory.CreateDirectory(fullDestination);
            string destinationPrefix = fullDestination.TrimEnd(Path.DirectorySeparatorChar) +
                                       Path.DirectorySeparatorChar;
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream payload = assembly.GetManifestResourceStream(PayloadResource)
                                    ?? throw new InvalidOperationException("安装器缺少内嵌Bundle。"))
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string normalizedEntry = entry.FullName.Replace('\\', '/');
                    if (!normalizedEntry.StartsWith(BundleName + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("安装包条目不在Bundle根目录内：" + entry.FullName);
                    }

                    string relativePath = normalizedEntry.Replace('/', Path.DirectorySeparatorChar);
                    string destination = Path.GetFullPath(Path.Combine(fullDestination, relativePath));
                    if (!destination.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("安装包包含不安全路径：" + entry.FullName);
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    string parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    using (Stream source = entry.Open())
                    using (FileStream target = File.Create(destination))
                    {
                        source.CopyTo(target);
                    }
                }
            }

            string bundlePath = Path.Combine(fullDestination, BundleName);
            ValidateBundle(bundlePath);
            return bundlePath;
        }

        private static void ValidateBundle(string bundlePath)
        {
            string manifestPath = Path.Combine(bundlePath, "PackageContents.xml");
            string hashPath = Path.Combine(bundlePath, "bundle-hashes.sha256");
            if (!File.Exists(manifestPath) || !File.Exists(hashPath))
            {
                throw new InvalidOperationException("安装包缺少Bundle清单或SHA-256清单。");
            }

            foreach (string series in new[]
                     {
                         "R21.0", "R22.0", "R23.0", "R23.1", "R24.0", "R24.1",
                         "R24.2", "R24.3", "R25.0", "R25.1", "R26.0"
                     })
            {
                string versionDirectory = Path.Combine(bundlePath, "Contents", series);
                if (!File.Exists(Path.Combine(versionDirectory, "YiZhiMao.Dc.Plugin.dll")) ||
                    !File.Exists(Path.Combine(versionDirectory, "YiZhiMao.Dc.Core.dll")))
                {
                    throw new InvalidOperationException("安装包缺少版本组件：" + series);
                }
            }

            string bundlePrefix = Path.GetFullPath(bundlePath).TrimEnd(Path.DirectorySeparatorChar) +
                                  Path.DirectorySeparatorChar;
            var listedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(hashPath))
            {
                int separator = line.IndexOf(" *", StringComparison.Ordinal);
                if (separator != 64)
                {
                    throw new InvalidOperationException("SHA-256清单格式无效。");
                }

                string expectedHash = line.Substring(0, 64);
                string normalizedRelativePath = line.Substring(separator + 2).Replace('\\', '/');
                string[] pathSegments = normalizedRelativePath.Split('/');
                if (string.IsNullOrWhiteSpace(normalizedRelativePath) ||
                    Path.IsPathRooted(normalizedRelativePath) ||
                    Array.IndexOf(pathSegments, "..") >= 0 ||
                    !listedFiles.Add(normalizedRelativePath))
                {
                    throw new InvalidOperationException("SHA-256清单包含无效或重复路径。");
                }

                string relativePath = normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar);
                string filePath = Path.GetFullPath(Path.Combine(bundlePath, relativePath));
                if (!filePath.StartsWith(bundlePrefix, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(filePath))
                {
                    throw new InvalidOperationException("SHA-256清单包含无效文件：" + relativePath);
                }

                string actualHash = ComputeSha256(filePath);
                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("安装文件校验失败：" + relativePath);
                }
            }

            foreach (string filePath in Directory.GetFiles(bundlePath, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(filePath, hashPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = filePath.Substring(bundlePrefix.Length).Replace('\\', '/');
                if (!listedFiles.Contains(relativePath))
                {
                    throw new InvalidOperationException("安装包包含未列入SHA-256清单的文件：" + relativePath);
                }
            }
        }

        private static void RegisterUninstaller(string version, InstallationScope scope)
        {
            Environment.SpecialFolder installerRootFolder = scope == InstallationScope.AllUsers
                ? Environment.SpecialFolder.CommonApplicationData
                : Environment.SpecialFolder.LocalApplicationData;
            string installerDirectory = Path.Combine(
                Environment.GetFolderPath(installerRootFolder),
                "YiZhiMaoDc",
                "Installer");
            Directory.CreateDirectory(installerDirectory);
            string installedInstaller = Path.Combine(
                installerDirectory,
                "YiZhiMaoDc-Setup-v" + version + ".exe");
            string currentInstaller = Application.ExecutablePath;
            RegistryKey registryRoot = scope == InstallationScope.AllUsers
                ? Registry.LocalMachine
                : Registry.CurrentUser;
            RegistryKeySnapshot registrySnapshot = CaptureRegistryKey(registryRoot);
            string installerBackup = installedInstaller + ".rollback-" + Guid.NewGuid().ToString("N");
            bool installerChanged = false;
            bool keepInstallerBackup = false;
            try
            {
                if (!string.Equals(
                        Path.GetFullPath(currentInstaller),
                        Path.GetFullPath(installedInstaller),
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(installedInstaller))
                    {
                        File.Copy(installedInstaller, installerBackup, true);
                    }

                    installerChanged = true;
                    File.Copy(currentInstaller, installedInstaller, true);
                }

                using (RegistryKey key = registryRoot.CreateSubKey(UninstallKeyPath))
                {
                    key.SetValue("DisplayName", ProductName + " CAD工具箱");
                    key.SetValue("DisplayVersion", version);
                    key.SetValue("Publisher", ProductName);
                    key.SetValue("InstallLocation", GetInstalledBundlePath(scope));
                    string uninstallArguments = scope == InstallationScope.AllUsers
                        ? " --uninstall --all-users"
                        : " --uninstall";
                    key.SetValue("UninstallString", "\"" + installedInstaller + "\"" + uninstallArguments);
                    key.SetValue("DisplayIcon", installedInstaller);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    RestoreRegistryKey(registryRoot, registrySnapshot);
                    if (installerChanged)
                    {
                        if (File.Exists(installerBackup))
                        {
                            File.Copy(installerBackup, installedInstaller, true);
                        }
                        else if (File.Exists(installedInstaller))
                        {
                            File.Delete(installedInstaller);
                        }
                    }
                }
                catch (Exception restoreError)
                {
                    keepInstallerBackup = true;
                    throw new InvalidOperationException(
                        "注册卸载入口失败，且恢复原注册信息失败：" + restoreError.Message,
                        ex);
                }

                throw new InvalidOperationException("注册卸载入口失败：" + ex.Message, ex);
            }
            finally
            {
                try
                {
                    if (!keepInstallerBackup && File.Exists(installerBackup))
                    {
                        File.Delete(installerBackup);
                    }
                }
                catch
                {
                    // 临时注册备份清理失败不改变安装结果。
                }
            }
        }

        private static RegistryKeySnapshot CaptureRegistryKey(RegistryKey registryRoot)
        {
            var snapshot = new RegistryKeySnapshot();
            using (RegistryKey key = registryRoot.OpenSubKey(UninstallKeyPath, false))
            {
                if (key == null)
                {
                    return snapshot;
                }

                snapshot.Exists = true;
                foreach (string name in key.GetValueNames())
                {
                    object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value != null)
                    {
                        snapshot.Values.Add(new RegistryValueSnapshot(name, value, key.GetValueKind(name)));
                    }
                }
            }

            return snapshot;
        }

        private static void RestoreRegistryKey(RegistryKey registryRoot, RegistryKeySnapshot snapshot)
        {
            registryRoot.DeleteSubKeyTree(UninstallKeyPath, false);
            if (!snapshot.Exists)
            {
                return;
            }

            using (RegistryKey key = registryRoot.CreateSubKey(UninstallKeyPath))
            {
                foreach (RegistryValueSnapshot value in snapshot.Values)
                {
                    key.SetValue(value.Name, value.Value, value.Kind);
                }
            }
        }

        private static string GetInstalledBundlePath(InstallationScope scope)
        {
            return Path.Combine(GetApplicationPluginsPath(scope), BundleName);
        }

        private static string GetApplicationPluginsPath(InstallationScope scope)
        {
            Environment.SpecialFolder rootFolder = scope == InstallationScope.AllUsers
                ? Environment.SpecialFolder.CommonApplicationData
                : Environment.SpecialFolder.ApplicationData;
            return Path.Combine(
                Environment.GetFolderPath(rootFolder),
                "Autodesk",
                "ApplicationPlugins");
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static bool TryRelaunchAsAdministrator(out string error, bool uninstall = false)
        {
            error = string.Empty;
            string arguments = uninstall
                ? "--uninstall --all-users --elevated"
                : "--all-users --elevated";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                error = "已取消管理员权限请求，未执行所有用户安装。";
                return false;
            }
            catch (Exception ex)
            {
                error = "无法以管理员权限继续安装：" + ex.Message;
                return false;
            }
        }

        private static string GetVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version.Major + "." + version.Minor + "." + version.Build;
        }

        private static bool EnsureAutoCadClosed(string operationName)
        {
            while (true)
            {
                int processCount;
                try
                {
                    Process[] processes = Process.GetProcessesByName("acad");
                    processCount = processes.Length;
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Fail("无法检测 AutoCAD 运行状态：" + ex.Message, false);
                    return false;
                }

                if (processCount == 0)
                {
                    return true;
                }

                AutoCadRunningAction action;
                using (var dialog = new AutoCadRunningDialog(processCount, operationName))
                {
                    dialog.ShowDialog();
                    action = dialog.SelectedAction;
                }

                if (action == AutoCadRunningAction.Cancel)
                {
                    return false;
                }

                if (action == AutoCadRunningAction.Retry)
                {
                    continue;
                }

                DialogResult confirmation = MessageBox.Show(
                    "强制关闭会立即结束所有已识别的 AutoCAD 进程，未保存的图纸修改会丢失。\r\n\r\n" +
                    "确认已经保存图纸，并强制关闭后继续" + operationName + "吗？",
                    ProductName + " 安装程序 - 确认强制关闭",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (confirmation != DialogResult.Yes)
                {
                    continue;
                }

                string error;
                if (!TryForceCloseAutoCadProcesses(out error))
                {
                    MessageBox.Show(
                        error,
                        ProductName + " 安装程序 - 无法关闭 AutoCAD",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    continue;
                }

                Thread.Sleep(250);
            }
        }

        private static bool TryForceCloseAutoCadProcesses(out string error)
        {
            error = string.Empty;
            var candidates = new List<AutoCadProcessCandidate>();
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName("acad");
            }
            catch (Exception ex)
            {
                error = "无法读取 AutoCAD 进程：" + ex.Message;
                return false;
            }

            try
            {
                foreach (Process process in processes)
                {
                    string executablePath;
                    if (!TryGetVerifiedAutoCadExecutable(process, out executablePath))
                    {
                        error = "检测到名为 acad.exe 的进程，但无法确认它来自 Autodesk AutoCAD。" +
                                "为避免误关其他程序，本次没有结束任何进程。请手动关闭后点击“重新检测”。";
                        return false;
                    }

                    candidates.Add(new AutoCadProcessCandidate(process.Id, executablePath));
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }

            foreach (AutoCadProcessCandidate candidate in candidates)
            {
                try
                {
                    using (Process process = Process.GetProcessById(candidate.ProcessId))
                    {
                        string currentPath;
                        if (!TryGetVerifiedAutoCadExecutable(process, out currentPath) ||
                            !string.Equals(
                                Path.GetFullPath(currentPath),
                                Path.GetFullPath(candidate.ExecutablePath),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            error = "AutoCAD 进程在关闭前发生变化。为避免误关其他程序，请重新检测后再试。";
                            return false;
                        }

                        process.Kill();
                        if (!process.WaitForExit(10000))
                        {
                            error = "AutoCAD 进程未能在 10 秒内退出，请手动关闭后再继续。";
                            return false;
                        }
                    }
                }
                catch (ArgumentException)
                {
                    // The process exited between detection and termination.
                }
                catch (Exception ex)
                {
                    error = "强制关闭 AutoCAD 失败：" + ex.Message;
                    return false;
                }
            }

            try
            {
                Process[] remaining = Process.GetProcessesByName("acad");
                int remainingCount = remaining.Length;
                foreach (Process process in remaining)
                {
                    process.Dispose();
                }

                if (remainingCount > 0)
                {
                    error = "仍有 AutoCAD 进程没有退出，请手动关闭后点击“重新检测”。";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "关闭后无法确认 AutoCAD 是否已经完全退出：" + ex.Message;
                return false;
            }

            return true;
        }

        private static bool TryGetVerifiedAutoCadExecutable(Process process, out string executablePath)
        {
            executablePath = string.Empty;
            try
            {
                if (process.MainModule == null || string.IsNullOrWhiteSpace(process.MainModule.FileName))
                {
                    return false;
                }

                executablePath = Path.GetFullPath(process.MainModule.FileName);
                return IsVerifiedAutoCadExecutable(executablePath);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsVerifiedAutoCadExecutable(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) ||
                !string.Equals(Path.GetFileName(executablePath), "acad.exe", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(executablePath))
            {
                return false;
            }

            try
            {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
                return ContainsIgnoreCase(versionInfo.CompanyName, "Autodesk") ||
                       ContainsIgnoreCase(versionInfo.ProductName, "AutoCAD");
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsIgnoreCase(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string GetUserDataPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YiZhiMaoDc");
        }

        private static void RollbackInstallation(
            string targetBundle,
            string backupBundle,
            bool existingMoved,
            bool newActivated,
            InstallerLog log)
        {
            if (newActivated && Directory.Exists(targetBundle))
            {
                Directory.Delete(targetBundle, true);
                log.Info("已删除未完成的新Bundle。");
            }

            if (existingMoved && Directory.Exists(backupBundle))
            {
                Directory.Move(backupBundle, targetBundle);
                log.Info("已恢复旧Bundle。");
            }
        }

        private static bool TryDeleteDirectory(string path, InstallerLog log)
        {
            try
            {
                DeleteDirectoryIfPresent(path);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("清理临时目录失败：" + path + "；" + ex.Message);
                return false;
            }
        }

        private static void DeleteDirectoryIfPresent(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static int Fail(string message, bool silent)
        {
            if (!silent)
            {
                MessageBox.Show(
                    message,
                    ProductName + " 安装程序 - 错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return 1;
        }

        private sealed class AutoCadProcessCandidate
        {
            public AutoCadProcessCandidate(int processId, string executablePath)
            {
                ProcessId = processId;
                ExecutablePath = executablePath;
            }

            public int ProcessId { get; }

            public string ExecutablePath { get; }
        }

        private sealed class RegistryKeySnapshot
        {
            public bool Exists { get; set; }

            public List<RegistryValueSnapshot> Values { get; } = new List<RegistryValueSnapshot>();
        }

        private sealed class RegistryValueSnapshot
        {
            public RegistryValueSnapshot(string name, object value, RegistryValueKind kind)
            {
                Name = name;
                Value = value;
                Kind = kind;
            }

            public string Name { get; }

            public object Value { get; }

            public RegistryValueKind Kind { get; }
        }
    }

    internal enum AutoCadRunningAction
    {
        Cancel,
        Retry,
        ForceClose
    }

    internal sealed class AutoCadRunningDialog : Form
    {
        public AutoCadRunningDialog(int processCount, string operationName)
        {
            SelectedAction = AutoCadRunningAction.Cancel;
            Text = "一只猫dc 安装程序";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(560, 220);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            var title = new Label
            {
                AutoSize = false,
                Location = new Point(24, 20),
                Size = new Size(512, 32),
                Font = new Font(Font, FontStyle.Bold),
                Text = "检测到 " + processCount + " 个 AutoCAD 进程仍在运行"
            };
            var message = new Label
            {
                AutoSize = false,
                Location = new Point(24, 58),
                Size = new Size(512, 62),
                Text = "请先保存所有图纸。关闭 AutoCAD 后可重新检测；也可以选择强制关闭并继续" +
                       operationName + "。"
            };

            var retryButton = CreateActionButton("重新检测", 24, AutoCadRunningAction.Retry);
            var forceButton = CreateActionButton("强制关闭并继续", 204, AutoCadRunningAction.ForceClose);
            var cancelButton = CreateActionButton(
                operationName == "卸载" ? "取消卸载" : "取消安装",
                384,
                AutoCadRunningAction.Cancel);

            AcceptButton = retryButton;
            CancelButton = cancelButton;
            Controls.Add(title);
            Controls.Add(message);
            Controls.Add(retryButton);
            Controls.Add(forceButton);
            Controls.Add(cancelButton);
        }

        public AutoCadRunningAction SelectedAction { get; private set; }

        private Button CreateActionButton(string text, int left, AutoCadRunningAction action)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(left, 150),
                Size = new Size(152, 38),
                DialogResult = action == AutoCadRunningAction.Cancel ? DialogResult.Cancel : DialogResult.OK
            };
            button.Click += delegate
            {
                SelectedAction = action;
                Close();
            };
            return button;
        }
    }
}
