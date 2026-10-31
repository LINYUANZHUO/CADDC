using System;
using System.IO;
using System.Text;

namespace YiZhiMao.Dc.Installer
{
    internal sealed class InstallerLog : IDisposable
    {
        private readonly StreamWriter _writer;

        private InstallerLog(string path, StreamWriter writer)
        {
            LogPath = path;
            _writer = writer;
        }

        public string LogPath { get; }

        public static InstallerLog Create(string operation)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YiZhiMaoDc",
                    "Installer",
                    "Logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(
                    directory,
                    operation + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 8) + ".log");
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                var writer = new StreamWriter(stream, new UTF8Encoding(true)) { AutoFlush = true };
                return new InstallerLog(path, writer);
            }
            catch
            {
                return new InstallerLog(string.Empty, null);
            }
        }

        public void Info(string message)
        {
            Write("INFO", message);
        }

        public void Error(string message)
        {
            Write("ERROR", message);
        }

        private void Write(string level, string message)
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.WriteLine(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message);
            }
            catch
            {
                // 日志失败不能破坏安装或回滚流程。
            }
        }

        public void Dispose()
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.Dispose();
            }
            catch
            {
                // 安装结果优先，关闭日志失败不再抛出。
            }
        }
    }
}
