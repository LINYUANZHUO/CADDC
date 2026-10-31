using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

internal static class SmartInputMethodRuntime
{
    private static readonly Dictionary<Document, DocumentState> AttachedDocuments = [];
    private static readonly HashSet<string> StringInputCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "TEXT",
        "DTEXT"
    };
    private static readonly HashSet<string> RichTextCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "DDEDIT",
        "MTEDIT",
        "TABLEDIT",
        "TEXTEDIT"
    };
    private static readonly WindowsInputMethodController Controller = new();
    private static bool initialized;
    private static Document? pendingChineseDocument;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Controller.Initialize();
        AcadApplication.DocumentManager.DocumentCreated += OnDocumentCreated;
        AcadApplication.DocumentManager.DocumentActivated += OnDocumentActivated;
        AcadApplication.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;

        foreach (Document document in AcadApplication.DocumentManager)
        {
            Attach(document);
        }
    }

    public static void Dispose()
    {
        if (!initialized)
        {
            return;
        }

        AcadApplication.Idle -= OnIdleSwitchToChinese;
        pendingChineseDocument = null;
        AcadApplication.DocumentManager.DocumentCreated -= OnDocumentCreated;
        AcadApplication.DocumentManager.DocumentActivated -= OnDocumentActivated;
        AcadApplication.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;

        foreach (var document in AttachedDocuments.Keys.ToArray())
        {
            Detach(document);
        }

        initialized = false;
    }

    public static void RefreshSettings()
    {
        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (!options.Enabled || !options.AutoChineseForText)
        {
            CancelPendingChineseSwitch();
        }
    }

    private static void OnDocumentCreated(object sender, DocumentCollectionEventArgs e) =>
        RunEventSafely(() => Attach(e.Document));

    private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e) =>
        RunEventSafely(() => HandleDocumentActivated(e));

    private static void HandleDocumentActivated(DocumentCollectionEventArgs e)
    {
        Document? document = e.Document;
        document ??= AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        Attach(document);
        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (options.Enabled && options.KeepCadCommandsEnglish)
        {
            Controller.TryUseEnglish();
        }
    }

    private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e) =>
        RunEventSafely(() => Detach(e.Document));

    private static void Attach(Document? document)
    {
        if (document is null || AttachedDocuments.ContainsKey(document))
        {
            return;
        }

        AttachedDocuments.Add(document, new DocumentState());

        document.CommandWillStart += OnCommandWillStart;
        document.CommandEnded += OnCommandFinished;
        document.CommandCancelled += OnCommandFinished;
        document.CommandFailed += OnCommandFinished;
        document.Editor.PromptingForString += OnPromptingForString;
        document.Editor.PromptedForString += OnPromptedForString;
        document.Editor.PromptedForPoint += OnPromptedForPoint;
    }

    private static void Detach(Document? document)
    {
        if (document is null || !AttachedDocuments.Remove(document))
        {
            return;
        }

        document.CommandWillStart -= OnCommandWillStart;
        document.CommandEnded -= OnCommandFinished;
        document.CommandCancelled -= OnCommandFinished;
        document.CommandFailed -= OnCommandFinished;
        document.Editor.PromptingForString -= OnPromptingForString;
        document.Editor.PromptedForString -= OnPromptedForString;
        document.Editor.PromptedForPoint -= OnPromptedForPoint;

        if (ReferenceEquals(pendingChineseDocument, document))
        {
            CancelPendingChineseSwitch();
        }
    }

    private static void OnCommandWillStart(object sender, CommandEventArgs e) =>
        RunEventSafely(() => HandleCommandWillStart(sender, e));

    private static void HandleCommandWillStart(object sender, CommandEventArgs e)
    {
        CancelPendingChineseSwitch();
        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (!options.Enabled)
        {
            return;
        }

        var document = sender as Document;
        if (document is null)
        {
            return;
        }

        var commandName = NormalizeCommandName(e.GlobalCommandName);
        if (!AttachedDocuments.TryGetValue(document, out var state))
        {
            return;
        }

        state.ActiveCommand = commandName;
        state.MtextPointCount = 0;

        if (options.KeepCadCommandsEnglish)
        {
            Controller.TryUseEnglish();
        }

        if (options.AutoChineseForText && RichTextCommands.Contains(commandName))
        {
            ScheduleChineseSwitch(document);
        }
    }

    private static void OnCommandFinished(object sender, CommandEventArgs e) =>
        RunEventSafely(() => HandleCommandFinished(sender));

    private static void HandleCommandFinished(object sender)
    {
        CancelPendingChineseSwitch();
        var wasTextCommand = false;
        if (sender is Document document && AttachedDocuments.TryGetValue(document, out var state))
        {
            wasTextCommand = IsTextCommand(state.ActiveCommand);
            state.ActiveCommand = string.Empty;
            state.MtextPointCount = 0;
        }

        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (options.Enabled &&
            (options.KeepCadCommandsEnglish || (options.RestoreEnglishAfterText && wasTextCommand)))
        {
            Controller.TryUseEnglish();
        }
    }

    private static void OnPromptingForString(object sender, PromptStringOptionsEventArgs e) =>
        RunEventSafely(() => HandlePromptingForString(sender));

    private static void HandlePromptingForString(object sender)
    {
        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (!options.Enabled || !options.AutoChineseForText ||
            sender is not Editor editor || !TryGetDocument(editor, out var document) ||
            !StringInputCommands.Contains(GetActiveCommand(document)))
        {
            return;
        }

        Controller.TryUseChinese();
    }

    private static void OnPromptedForString(object sender, PromptStringResultEventArgs e) =>
        RunEventSafely(() => HandlePromptedForString(sender));

    private static void HandlePromptedForString(object sender)
    {
        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (!options.Enabled || !options.RestoreEnglishAfterText ||
            sender is not Editor editor || !TryGetDocument(editor, out var document) ||
            !StringInputCommands.Contains(GetActiveCommand(document)))
        {
            return;
        }

        Controller.TryUseEnglish();
    }

    private static void OnPromptedForPoint(object sender, PromptPointResultEventArgs e) =>
        RunEventSafely(() => HandlePromptedForPoint(sender));

    private static void HandlePromptedForPoint(object sender)
    {
        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (!options.Enabled || !options.AutoChineseForText ||
            sender is not Editor editor || !TryGetDocument(editor, out var document) ||
            !string.Equals(GetActiveCommand(document), "MTEXT", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!AttachedDocuments.TryGetValue(document, out var state))
        {
            return;
        }

        var count = ++state.MtextPointCount;
        if (count >= 2)
        {
            ScheduleChineseSwitch(document);
        }
    }

    private static bool TryGetDocument(Editor editor, out Document document)
    {
        document = AttachedDocuments.Keys.FirstOrDefault(candidate => ReferenceEquals(candidate.Editor, editor))!;
        return document is not null;
    }

    private static string GetActiveCommand(Document document) =>
        AttachedDocuments.TryGetValue(document, out var state) ? state.ActiveCommand : string.Empty;

    private static bool IsTextCommand(string commandName) =>
        string.Equals(commandName, "MTEXT", StringComparison.OrdinalIgnoreCase) ||
        StringInputCommands.Contains(commandName) ||
        RichTextCommands.Contains(commandName);

    private static string NormalizeCommandName(string? commandName) =>
        (commandName ?? string.Empty).Trim().TrimStart('.', '_').ToUpperInvariant();

    private static void ScheduleChineseSwitch(Document document)
    {
        pendingChineseDocument = document;
        AcadApplication.Idle -= OnIdleSwitchToChinese;
        AcadApplication.Idle += OnIdleSwitchToChinese;
    }

    private static void CancelPendingChineseSwitch()
    {
        AcadApplication.Idle -= OnIdleSwitchToChinese;
        pendingChineseDocument = null;
    }

    private static void OnIdleSwitchToChinese(object? sender, EventArgs e) =>
        RunEventSafely(HandleIdleSwitchToChinese);

    private static void HandleIdleSwitchToChinese()
    {
        AcadApplication.Idle -= OnIdleSwitchToChinese;
        var document = pendingChineseDocument;
        pendingChineseDocument = null;
        var options = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        if (document is not null &&
            options.Enabled &&
            options.AutoChineseForText &&
            ReferenceEquals(AcadApplication.DocumentManager.MdiActiveDocument, document))
        {
            Controller.TryUseChinese();
        }
    }

    private static void RunEventSafely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            try
            {
                CancelPendingChineseSwitch();
            }
            catch (Exception)
            {
            }
        }
    }

    private sealed class DocumentState
    {
        public string ActiveCommand { get; set; } = string.Empty;

        public int MtextPointCount { get; set; }
    }

    private sealed class WindowsInputMethodController
    {
        private const ushort ChinesePrimaryLanguageId = 0x04;
        private const ushort EnglishUnitedStatesLanguageId = 0x0409;
        private IntPtr englishLayout;
        private IntPtr chineseLayout;

        public void Initialize()
        {
            try
            {
                var current = GetKeyboardLayout(0);
                var count = GetKeyboardLayoutList(0, null);
                var layouts = count > 0 ? new IntPtr[count] : [];
                if (layouts.Length > 0)
                {
                    GetKeyboardLayoutList(layouts.Length, layouts);
                }

                chineseLayout = IsChinese(current)
                    ? current
                    : layouts.FirstOrDefault(IsChinese);
                englishLayout = !IsChinese(current)
                    ? current
                    : layouts.FirstOrDefault(layout => GetLanguageId(layout) == EnglishUnitedStatesLanguageId);
                if (englishLayout == IntPtr.Zero)
                {
                    englishLayout = layouts.FirstOrDefault(layout => !IsChinese(layout));
                }

                if (englishLayout == IntPtr.Zero)
                {
                    englishLayout = current;
                }
            }
            catch (Exception)
            {
                englishLayout = IntPtr.Zero;
                chineseLayout = IntPtr.Zero;
            }
        }

        public bool TryUseEnglish() => TryApply(englishLayout, false);

        public bool TryUseChinese() => chineseLayout != IntPtr.Zero && TryApply(chineseLayout, true);

        private static bool TryApply(IntPtr layout, bool openInputMethod)
        {
            try
            {
                if (layout != IntPtr.Zero)
                {
                    ActivateKeyboardLayout(layout, 0);
                }

                var focusWindow = GetFocus();
                if (focusWindow == IntPtr.Zero)
                {
                    return false;
                }

                var inputContext = ImmGetContext(focusWindow);
                if (inputContext == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    return ImmSetOpenStatus(inputContext, openInputMethod);
                }
                finally
                {
                    ImmReleaseContext(focusWindow, inputContext);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsChinese(IntPtr layout) =>
            (GetLanguageId(layout) & 0x03ff) == ChinesePrimaryLanguageId;

        private static ushort GetLanguageId(IntPtr layout) => unchecked((ushort)layout.ToInt64());

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        private static extern int GetKeyboardLayoutList(int bufferLength, [Out] IntPtr[]? layouts);

        [DllImport("user32.dll")]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr keyboardLayout, uint flags);

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr windowHandle);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImmSetOpenStatus(IntPtr inputContext, bool open);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImmReleaseContext(IntPtr windowHandle, IntPtr inputContext);
    }
}
