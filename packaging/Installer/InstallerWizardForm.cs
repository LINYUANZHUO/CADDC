using System;
using System.Drawing;
using System.Windows.Forms;

namespace YiZhiMao.Dc.Installer
{
    internal enum InstallationScope
    {
        CurrentUser,
        AllUsers
    }

    internal sealed class InstallationOutcome
    {
        public bool Success { get; set; }

        public bool Cancelled { get; set; }

        public bool Relaunched { get; set; }

        public string InstallPath { get; set; } = string.Empty;

        public string BackupPath { get; set; } = string.Empty;

        public int PreservedResourceCount { get; set; }

        public int ResourceConflictCount { get; set; }

        public string LogPath { get; set; } = string.Empty;

        public string Warning { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;
    }

    internal sealed class InstallerWizardForm : Form
    {
        private readonly string _productName;
        private readonly string _version;
        private readonly string _currentUserPath;
        private readonly string _allUsersPath;
        private readonly Func<InstallationScope, Action<int, string>, InstallationOutcome> _install;
        private readonly Label _headerLabel;
        private readonly Label _stepLabel;
        private readonly Panel _contentPanel;
        private readonly Button _backButton;
        private readonly Button _nextButton;
        private readonly Button _cancelButton;
        private ProgressBar _progressBar;
        private Label _progressLabel;
        private InstallationOutcome _outcome;
        private int _page;
        private bool _installing;

        public InstallerWizardForm(
            string productName,
            string version,
            string currentUserPath,
            string allUsersPath,
            InstallationScope initialScope,
            bool startAtConfirmation,
            Func<InstallationScope, Action<int, string>, InstallationOutcome> install)
        {
            _productName = productName;
            _version = version;
            _currentUserPath = currentUserPath;
            _allUsersPath = allUsersPath;
            _install = install;
            SelectedScope = initialScope;

            Text = productName + " 安装程序";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(680, 430);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _headerLabel = new Label
            {
                AutoSize = false,
                Location = new Point(28, 20),
                Size = new Size(624, 34),
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point)
            };
            _stepLabel = new Label
            {
                AutoSize = false,
                Location = new Point(30, 58),
                Size = new Size(620, 24),
                ForeColor = Color.DimGray
            };
            _contentPanel = new Panel
            {
                Location = new Point(28, 92),
                Size = new Size(624, 268),
                BorderStyle = BorderStyle.FixedSingle
            };
            _backButton = CreateButton("上一步", 310);
            _nextButton = CreateButton("下一步", 422);
            _cancelButton = CreateButton("取消安装", 534);

            _backButton.Click += delegate { ShowPage(_page - 1); };
            _nextButton.Click += delegate { MoveNext(); };
            _cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(_headerLabel);
            Controls.Add(_stepLabel);
            Controls.Add(_contentPanel);
            Controls.Add(_backButton);
            Controls.Add(_nextButton);
            Controls.Add(_cancelButton);
            CancelButton = _cancelButton;

            ShowPage(startAtConfirmation ? 2 : 0);
        }

        public InstallationScope SelectedScope { get; private set; }

        public int ExitCode { get; private set; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_installing)
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }

        private Button CreateButton(string text, int left)
        {
            return new Button
            {
                Text = text,
                Location = new Point(left, 378),
                Size = new Size(104, 34)
            };
        }

        private void ShowPage(int page)
        {
            _page = Math.Max(0, Math.Min(4, page));
            _contentPanel.Controls.Clear();
            _backButton.Visible = _page > 0 && _page < 3;
            _backButton.Enabled = true;
            _nextButton.Visible = true;
            _nextButton.Enabled = true;
            _cancelButton.Visible = _page < 4;
            _cancelButton.Enabled = true;

            switch (_page)
            {
                case 0:
                    RenderWelcomePage();
                    break;
                case 1:
                    RenderLocationPage();
                    break;
                case 2:
                    RenderConfirmationPage();
                    break;
                case 3:
                    RenderProgressPage();
                    break;
                default:
                    RenderCompletionPage();
                    break;
            }
        }

        private void RenderWelcomePage()
        {
            _headerLabel.Text = "欢迎安装 " + _productName;
            _stepLabel.Text = "步骤 1/4 · 欢迎";
            _nextButton.Text = "下一步";
            AddText(
                "版本：" + _version + "\r\n\r\n" +
                "支持 AutoCAD 2017–2027。安装程序会先校验内嵌文件，升级时会备份现有插件。\r\n\r\n" +
                "继续前不需要立即关闭 AutoCAD；确认安装后再处理正在运行的 AutoCAD。",
                28,
                28,
                566,
                180);
        }

        private void RenderLocationPage()
        {
            _headerLabel.Text = "选择安装位置";
            _stepLabel.Text = "步骤 2/4 · 安装范围";
            _nextButton.Text = "下一步";

            var currentUser = new RadioButton
            {
                Text = "当前用户（推荐）",
                Location = new Point(28, 24),
                Size = new Size(260, 26),
                Checked = SelectedScope == InstallationScope.CurrentUser
            };
            var allUsers = new RadioButton
            {
                Text = "所有用户（需要管理员权限）",
                Location = new Point(28, 58),
                Size = new Size(310, 26),
                Checked = SelectedScope == InstallationScope.AllUsers
            };
            var pathCaption = new Label
            {
                Text = "安装位置：",
                Location = new Point(28, 104),
                Size = new Size(120, 24)
            };
            var pathBox = new TextBox
            {
                Location = new Point(28, 132),
                Size = new Size(566, 28),
                ReadOnly = true,
                Text = GetSelectedPath()
            };
            var hint = new Label
            {
                AutoSize = false,
                Location = new Point(28, 174),
                Size = new Size(566, 54),
                ForeColor = Color.DimGray,
                Text = "为保证 AutoCAD 自动加载，插件只能安装到 Autodesk 标准 ApplicationPlugins 目录，因此不提供任意文件夹浏览。"
            };
            currentUser.CheckedChanged += delegate
            {
                if (currentUser.Checked)
                {
                    SelectedScope = InstallationScope.CurrentUser;
                    pathBox.Text = GetSelectedPath();
                }
            };
            allUsers.CheckedChanged += delegate
            {
                if (allUsers.Checked)
                {
                    SelectedScope = InstallationScope.AllUsers;
                    pathBox.Text = GetSelectedPath();
                }
            };

            _contentPanel.Controls.Add(currentUser);
            _contentPanel.Controls.Add(allUsers);
            _contentPanel.Controls.Add(pathCaption);
            _contentPanel.Controls.Add(pathBox);
            _contentPanel.Controls.Add(hint);
        }

        private void RenderConfirmationPage()
        {
            _headerLabel.Text = "确认安装";
            _stepLabel.Text = "步骤 3/4 · 安装确认";
            _nextButton.Text = "开始安装";
            string scopeText = SelectedScope == InstallationScope.CurrentUser
                ? "当前用户"
                : "所有用户";
            AddText(
                "产品：" + _productName + " " + _version + "\r\n\r\n" +
                "安装范围：" + scopeText + "\r\n\r\n" +
                "安装位置：\r\n" + GetSelectedPath() + "\r\n\r\n" +
                "点击“开始安装”后，程序才会检测 AutoCAD 并提示保存图纸。",
                28,
                24,
                566,
                210);
        }

        private void RenderProgressPage()
        {
            _headerLabel.Text = "正在准备安装";
            _stepLabel.Text = "步骤 4/4 · 安装进度";
            _backButton.Visible = false;
            _nextButton.Visible = false;
            _cancelButton.Enabled = false;

            _progressLabel = new Label
            {
                AutoSize = false,
                Location = new Point(28, 62),
                Size = new Size(566, 56),
                Text = "正在准备安装…"
            };
            _progressBar = new ProgressBar
            {
                Location = new Point(28, 128),
                Size = new Size(566, 28),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            _contentPanel.Controls.Add(_progressLabel);
            _contentPanel.Controls.Add(_progressBar);
        }

        private void RenderCompletionPage()
        {
            _headerLabel.Text = "安装完成";
            _stepLabel.Text = "请重新启动 AutoCAD";
            _backButton.Visible = false;
            _nextButton.Text = "完成";
            _cancelButton.Visible = false;

            string backupText = string.IsNullOrEmpty(_outcome.BackupPath)
                ? "无（首次安装）"
                : _outcome.BackupPath;
            string warningText = string.IsNullOrEmpty(_outcome.Warning)
                ? string.Empty
                : "\r\n\r\n注意：" + _outcome.Warning;
            AddText(
                "安装位置：\r\n" + _outcome.InstallPath + "\r\n\r\n" +
                "旧版本备份：\r\n" + backupText + "\r\n\r\n" +
                "保留的用户资源：" + _outcome.PreservedResourceCount + " 项" +
                (_outcome.ResourceConflictCount > 0
                    ? "（另有 " + _outcome.ResourceConflictCount + " 项同名冲突保留在旧版本备份中）"
                    : string.Empty) + "\r\n\r\n" +
                "安装日志：\r\n" +
                (string.IsNullOrEmpty(_outcome.LogPath) ? "未能创建日志" : _outcome.LogPath) + warningText,
                28,
                18,
                566,
                240);
        }

        private void MoveNext()
        {
            if (_page < 2)
            {
                ShowPage(_page + 1);
                return;
            }

            if (_page == 2)
            {
                BeginInstallation();
                return;
            }

            if (_page == 4)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void BeginInstallation()
        {
            ShowPage(3);
            _installing = true;
            ExitCode = 0;
            try
            {
                Application.DoEvents();
                _outcome = _install(SelectedScope, ReportProgress) ?? new InstallationOutcome
                {
                    Error = "安装程序没有返回结果。"
                };
            }
            catch (Exception ex)
            {
                _outcome = new InstallationOutcome { Error = ex.Message };
            }
            finally
            {
                _installing = false;
            }

            if (_outcome.Relaunched)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (_outcome.Cancelled)
            {
                ShowPage(2);
                return;
            }

            if (!_outcome.Success)
            {
                ExitCode = 1;
                MessageBox.Show(
                    string.IsNullOrEmpty(_outcome.Error) ? "安装失败。" : _outcome.Error,
                    _productName + " 安装程序 - 错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ShowPage(2);
                return;
            }

            ExitCode = 0;
            ShowPage(4);
        }

        private void ReportProgress(int percent, string message)
        {
            if (_progressBar == null || _progressLabel == null)
            {
                return;
            }

            _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, percent));
            _progressLabel.Text = message;
            _headerLabel.Text = message;
            Refresh();
            Application.DoEvents();
        }

        private string GetSelectedPath()
        {
            return SelectedScope == InstallationScope.CurrentUser
                ? _currentUserPath
                : _allUsersPath;
        }

        private void AddText(string text, int left, int top, int width, int height)
        {
            _contentPanel.Controls.Add(new Label
            {
                AutoSize = false,
                Location = new Point(left, top),
                Size = new Size(width, height),
                Text = text
            });
        }
    }

    internal sealed class UninstallOptionsDialog : Form
    {
        private readonly CheckBox _deleteUserData;

        public UninstallOptionsDialog(string bundlePath)
        {
            Text = "一只猫dc 卸载程序";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 270);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            Controls.Add(new Label
            {
                AutoSize = false,
                Location = new Point(24, 20),
                Size = new Size(572, 94),
                Text = "将卸载以下插件：\r\n" + bundlePath +
                       "\r\n\r\n默认只删除程序文件；快捷键、打印设置和用户新增或修改的模板、LISP、五金资源会保留。"
            });

            _deleteUserData = new CheckBox
            {
                AutoSize = false,
                Location = new Point(24, 130),
                Size = new Size(572, 30),
                Text = "同时删除当前用户配置和自定义资源（不可恢复）",
                Checked = false
            };
            Controls.Add(_deleteUserData);

            var uninstall = new Button
            {
                Text = "开始卸载",
                Location = new Point(364, 208),
                Size = new Size(108, 36),
                DialogResult = DialogResult.OK
            };
            var cancel = new Button
            {
                Text = "取消",
                Location = new Point(488, 208),
                Size = new Size(108, 36),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(uninstall);
            Controls.Add(cancel);
            AcceptButton = uninstall;
            CancelButton = cancel;
        }

        public bool DeleteUserData => _deleteUserData.Checked;
    }
}
