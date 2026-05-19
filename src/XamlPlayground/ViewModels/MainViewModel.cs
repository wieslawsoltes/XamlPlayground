using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Octokit;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Styling;
using Dock.Avalonia.Themes;
using Dock.Avalonia.Themes.Fluent;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Microsoft.CodeAnalysis;
using RemotePixelFormat = Avalonia.Remote.Protocol.Viewport.PixelFormat;
using Avalonia.Remote.Protocol.Viewport;
using Avalonia.Remote.Protocol.Input;
using XamlPlayground.Services;
using XamlPlayground.Services.IntelliSense;
using XamlPlayground.Services.Preview;
using XamlPlayground.Extensions;
using XamlPlayground.ViewModels.Docking;
using XamlPlayground.ViewModels.Workspace;
using XamlPlayground.Workspace;
using Avalonia.Threading;

namespace XamlPlayground.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan AutoRunDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SolutionExplorerSearchDelay = TimeSpan.FromMilliseconds(200);
    private static readonly string[] PreferredWorkspacePreviewFrameworks =
    {
        "net10.0",
        "net9.0",
        "net8.0",
        "net7.0",
        "net6.0",
        "net5.0",
        "netcoreapp3.1",
        "netcoreapp3.0"
    };
    private static readonly TimeSpan SolutionExplorerRegexTimeout = TimeSpan.FromMilliseconds(50);
    private const string PlaygroundXamlDocument = "Main.axaml";

    [ObservableProperty] private ObservableCollection<SampleViewModel> _samples;
    [ObservableProperty] private SampleViewModel? _currentSample;
    [ObservableProperty] private Control? _control;
    [ObservableProperty] private Bitmap? _remotePreviewBitmap;
    [ObservableProperty] private bool _isRemotePreviewActive;
    [ObservableProperty] private AvaloniaObject? _diagnosticsRoot;
    [ObservableProperty] private IFactory? _dockFactory;
    [ObservableProperty] private IRootDock? _dockLayout;
    [ObservableProperty] private bool _enableAutoRun;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private string? _lastErrorMessage;
    [ObservableProperty] private int _editorFontSize;
    [ObservableProperty] private InMemorySolution? _solution;
    [ObservableProperty] private ObservableCollection<SolutionExplorerNodeViewModel> _solutionExplorerNodes = new();
    [ObservableProperty] private string _solutionExplorerSearchText = string.Empty;
    [ObservableProperty] private bool _solutionExplorerSearchUseRegex;
    [ObservableProperty] private string? _solutionExplorerSearchError;
    [ObservableProperty] private SolutionExplorerNodeViewModel? _selectedSolutionExplorerNode;
    [ObservableProperty] private NewProjectWizardViewModel _newProjectWizard = new();
    [ObservableProperty] private InMemoryProject? _activeProject;
    [ObservableProperty] private InMemoryProjectFile? _activeWorkspaceFile;
    [ObservableProperty] private InMemoryProjectFile? _activeXamlFile;
    [ObservableProperty] private InMemoryProjectFile? _activeCodeFile;
    [ObservableProperty] private string? _workspaceEditorNavigationFilePath;
    [ObservableProperty] private int _workspaceEditorNavigationStart;
    [ObservableProperty] private int _workspaceEditorNavigationLength;
    [ObservableProperty] private int _workspaceEditorNavigationVersion;
    [ObservableProperty] private string _workspaceStatus = "No solution loaded.";
    [ObservableProperty] private bool _isWorkspaceLoading;
    [ObservableProperty] private string _currentDockPerspectiveId = PlaygroundDockFactory.DefaultPerspectiveId;
    [ObservableProperty] private string _previewStatus = "Idle.";
    [ObservableProperty] private PreviewReloadStrategy _previewReloadStrategy = PreviewReloadStrategy.NoOp;
    private readonly IDockThemeManager _dockThemeManager;
    private readonly InMemorySolutionFactory _solutionFactory;
    private readonly WorkspaceRemotePreviewService _remotePreviewService = new();
    private readonly PreviewSessionManager _previewSessionManager = new();
    private IDevToolsPropertyEditHandler _diagnosticsPropertyEditHandler = null!;
    private bool _update;
    private bool _rerunRequested;
    private bool _openingSample;
    private WorkspacePreviewAssemblyScope? _previous;
    private IStorageFile? _openXamlFile;
    private IStorageFile? _openCodeFile;
    private ObservableCollection<SolutionExplorerNodeViewModel> _allSolutionExplorerNodes = new();
    private IDisposable? _solutionExplorerSearchThrottle;
    private int _solutionExplorerSearchRevision;
    private IDisposable? _timer;

    public DevToolsOptions DiagnosticsDevToolsOptions { get; private set; } = null!;

    public MainViewModel(string? initialGist)
    {
        _editorFontSize = 14;
        _samples = GetSamples(".xml");
        _enableAutoRun = true;
        _isDarkTheme = IsApplicationDarkTheme();
        _dockThemeManager = new DockFluentThemeManager();
        _solutionFactory = new InMemorySolutionFactory(OnProjectFileChanged);
        ExtensionHost = new ExtensionHostService();
        ExtensionHost.RegisterProvider(new BuiltInExtensionProvider());
        RegisterExternalExtensionPackages();
        _remotePreviewService.FrameReceived += OnRemotePreviewFrameReceived;
        _remotePreviewService.ErrorReceived += error => Dispatcher.UIThread.Post(
            () => LastErrorMessage = FormatRemotePreviewError(error));

        NewFileCommand = new RelayCommand(NewFile);
        ShowNewProjectWizardCommand = new RelayCommand(ShowNewProjectWizard);
        CreateProjectCommand = new RelayCommand(CreateProjectFromWizard);
        CancelNewProjectCommand = new RelayCommand(() => NewProjectWizard.IsOpen = false);
        AddUserControlCommand = new RelayCommand(AddUserControl, () => ActiveProject is not null);
        AddResourceDictionaryCommand = new RelayCommand(AddResourceDictionary, () => ActiveProject is not null);
        ImportSolutionCommand = new AsyncRelayCommand(ImportSolution);
        OpenMsBuildWorkspaceCommand = new AsyncRelayCommand(OpenMsBuildWorkspace, () => !IsWorkspaceLoading);
        OpenMsBuildWorkspaceFolderCommand = new AsyncRelayCommand(OpenMsBuildWorkspaceFolder, () => !IsWorkspaceLoading);
        ExportSolutionCommand = new AsyncRelayCommand(ExportSolution, CanExportSolution);
        ExportStandardSolutionFolderCommand = new AsyncRelayCommand(ExportStandardSolutionFolder, CanExportSolution);
        BuildSolutionCommand = new RelayCommand(RunActiveDocument);
        SaveWorkspaceFileCommand = new AsyncRelayCommand(SaveWorkspaceFile, CanSaveWorkspaceFile);
        SaveAllWorkspaceFilesCommand = new AsyncRelayCommand(SaveAllWorkspaceFiles, CanSaveAllWorkspaceFiles);
        OpenXamlFileCommand = new AsyncRelayCommand(async () => await OpenXamlFile());
        SaveXamlFileCommand = new AsyncRelayCommand(async () => await SaveXamlFile());
        OpenCodeFileCommand = new AsyncRelayCommand(async () => await OpenCodeFile());
        SaveCodeFileCommand = new AsyncRelayCommand(async () => await SaveCodeFile());
        RunCommand = new RelayCommand(RunActiveDocument);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        GistCommand = new AsyncRelayCommand<string?>(Gist);
        ToggleDockToolCommand = new RelayCommand<string?>(ToggleDockTool);
        ApplyDockPerspectiveCommand = new RelayCommand<string?>(ApplyDockPerspective);
        RestoreDockToolsCommand = new RelayCommand(RestoreDockTools);
        DockToolMenuItems = new ObservableCollection<DockToolMenuItemViewModel>(
            PlaygroundDockFactory.ToolDescriptors.Select(descriptor => new DockToolMenuItemViewModel(descriptor, ToggleDockToolCommand)));
        DockPerspectiveMenuItems = new ObservableCollection<DockPerspectiveMenuItemViewModel>(
            PlaygroundDockFactory.PerspectiveDescriptors.Select(descriptor => new DockPerspectiveMenuItemViewModel(descriptor, ApplyDockPerspectiveCommand)));
        InitializeVisualEditing();
        InitializeBuiltInExtensionCommands();
        InitializeExtensionCommandSurface();
        InitializeDockLayout();
        QueueExtensionActivation(ExtensionActivationEvents.OnStartupFinished);

        if (!string.IsNullOrEmpty(initialGist))
        {
            _ = Gist(initialGist);
        }
        else
        {
            CurrentSample = Samples.FirstOrDefault(x => x.Name == "Demo");
        }
    }

    public IStorageProvider? StorageProvider { get; set; }

    public ExtensionHostService ExtensionHost { get; }

    public bool IsInProcessPreviewActive => !IsRemotePreviewActive;

    public bool IsVisualEditorOverlayActive => VisualEditorDesignerMode && !IsRemotePreviewActive;

    public void Dispose()
    {
        _timer?.Dispose();
        _solutionExplorerSearchThrottle?.Dispose();
        if (DockFactory is PlaygroundDockFactory factory)
        {
            factory.DockableHidden -= DockFactoryOnDockableHidden;
            factory.DockableRestored -= DockFactoryOnDockableRestored;
        }

        _remotePreviewService.Dispose();
        DisposeBuiltInExtensionCommands();
        try
        {
            ExtensionHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            LastErrorMessage = exception.Message;
        }

        UnloadPreviousInProcessPreview();
    }

    partial void OnIsRemotePreviewActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInProcessPreviewActive));
        OnPropertyChanged(nameof(IsVisualEditorOverlayActive));
        OnPropertyChanged(nameof(VisualEditorPreviewContentHitTestVisible));
        if (value)
        {
            ClearVisualEditorPreviewDropFeedback();
            VisualEditorPreviewSelectionVisible = false;
            UpdateVisualEditorPreviewCurrentContainerBounds(null);
        }
    }
    
    public ICommand RunCommand { get; }

    public ICommand ToggleThemeCommand { get; }

    public ICommand GistCommand { get; }

    public ICommand NewFileCommand { get; }

    public ICommand ShowNewProjectWizardCommand { get; }

    public ICommand CreateProjectCommand { get; }

    public ICommand CancelNewProjectCommand { get; }

    public ICommand AddUserControlCommand { get; }

    public ICommand AddResourceDictionaryCommand { get; }

    public ICommand ImportSolutionCommand { get; }

    public ICommand OpenMsBuildWorkspaceCommand { get; }

    public ICommand OpenMsBuildWorkspaceFolderCommand { get; }

    public ICommand ExportSolutionCommand { get; }

    public ICommand ExportStandardSolutionFolderCommand { get; }

    public ICommand BuildSolutionCommand { get; }

    public ICommand SaveWorkspaceFileCommand { get; }

    public ICommand SaveAllWorkspaceFilesCommand { get; }

    public ICommand OpenXamlFileCommand { get; }

    public ICommand SaveXamlFileCommand { get; }

    public ICommand OpenCodeFileCommand { get; }

    public ICommand SaveCodeFileCommand { get; }

    public ICommand ToggleDockToolCommand { get; }

    public ICommand ApplyDockPerspectiveCommand { get; }

    public ICommand RestoreDockToolsCommand { get; }

    public ObservableCollection<DockToolMenuItemViewModel> DockToolMenuItems { get; }

    public ObservableCollection<DockPerspectiveMenuItemViewModel> DockPerspectiveMenuItems { get; }

    public bool IsLightTheme => !IsDarkTheme;

    public string ThemeToggleToolTip => IsDarkTheme ? "Switch to light theme" : "Switch to dark theme";

    public bool HasSolutionExplorerSearchError => !string.IsNullOrWhiteSpace(SolutionExplorerSearchError);

    public void ApplySolutionExplorerSearchNow()
    {
        CancelPendingSolutionExplorerSearch();
        ApplySolutionExplorerSearch();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(CurrentSample)
            && CurrentSample is { } sampleViewModel)
        {
            OpenCurrentSample(sampleViewModel);
            return;
        }

        if (e.PropertyName == nameof(ActiveProject))
        {
            (AddUserControlCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (AddResourceDictionaryCommand as RelayCommand)?.NotifyCanExecuteChanged();
            NotifyControlThemeCommandsChanged();
        }

        if (e.PropertyName == nameof(ActiveWorkspaceFile))
        {
            (SaveWorkspaceFileCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(Solution))
        {
            (ExportSolutionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (ExportStandardSolutionFolderCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (SaveAllWorkspaceFilesCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(IsWorkspaceLoading))
        {
            (OpenMsBuildWorkspaceCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (OpenMsBuildWorkspaceFolderCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(ThemeToggleToolTip));
    }

    partial void OnEnableAutoRunChanged(bool value)
    {
        if (value)
        {
            return;
        }

        _timer?.Dispose();
        _timer = null;
    }

    partial void OnLastErrorMessageChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            DockFactory is not PlaygroundDockFactory factory)
        {
            return;
        }

        factory.ActivateErrors();
    }

    partial void OnSolutionExplorerSearchTextChanged(string value)
    {
        ScheduleSolutionExplorerSearch();
    }

    partial void OnSolutionExplorerSearchUseRegexChanged(bool value)
    {
        ScheduleSolutionExplorerSearch();
    }

    partial void OnSolutionExplorerSearchErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSolutionExplorerSearchError));
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyThemeCore();
            return;
        }

        Dispatcher.UIThread.Invoke(ApplyThemeCore);
    }

    private void ApplyThemeCore()
    {
        var themeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;

        if (Avalonia.Application.Current is { } application)
        {
            application.RequestedThemeVariant = themeVariant;
        }

        if (Avalonia.Application.Current is not XamlPlayground.App)
        {
            return;
        }

        try
        {
            _dockThemeManager.Switch(IsDarkTheme ? 1 : 0);

            var presetIndex = FindDockRiderPresetIndex(IsDarkTheme);
            if (presetIndex >= 0)
            {
                _dockThemeManager.SwitchPreset(presetIndex);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    private static bool IsApplicationDarkTheme()
    {
        return Dispatcher.UIThread.CheckAccess()
               && Avalonia.Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;
    }

    private int FindDockRiderPresetIndex(bool dark)
    {
        var presetNames = _dockThemeManager.PresetNames;
        var themeName = dark ? "Dark" : "Light";

        for (var i = 0; i < presetNames.Count; i++)
        {
            var name = presetNames[i];
            if (IsRiderPreset(name)
                && name.Contains(themeName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (var i = 0; i < presetNames.Count; i++)
        {
            if (presetNames[i].Contains(themeName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsRiderPreset(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit).ToArray());
        return normalized.Contains("Rider", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string Xaml, string Code)> GetGistContent(string id)
    {
        var client = new GitHubClient(new ProductHeaderValue("XamlPlayground"));
        var gist = await client.Gist.Get(id);
        var xaml = gist.Files
            .FirstOrDefault(x => string.Compare(x.Key, "Main.axaml", StringComparison.OrdinalIgnoreCase) == 0)
            .Value;
        var code = gist.Files
            .FirstOrDefault(x => string.Compare(x.Key, "Main.axaml.cs", StringComparison.OrdinalIgnoreCase) == 0)
            .Value;
        return (xaml?.Content ?? "", code?.Content ?? "");
    }

    public async Task Gist(string? id)
    {
        if (id is null)
        {
            return;
        }
        try
        {
            var (xaml, code) = await GetGistContent(id);
            var sample = new SampleViewModel("Gist", xaml, code, Open, AutoRun);
            Samples.Insert(0, sample);
            CurrentSample = sample;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    private string? GetSampleName(string resourceName)
    {
        var parts = resourceName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[^2]}" : null;
    }

    private string? LoadResourceString(string name)
    {
        var assembly = typeof(MainViewModel).Assembly;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null)
        {
            return null;
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private ObservableCollection<SampleViewModel> GetSamples(string sampleExtension)
    {
        var samples = new ObservableCollection<SampleViewModel>();
        var assembly = typeof(MainViewModel).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        samples.Add(new SampleViewModel("Code", Templates.s_xaml, Templates.s_code, Open, AutoRun));

        foreach (var resourceName in resourceNames.OrderBy(GetSampleName, StringComparer.Ordinal))
        {
            if (!resourceName.EndsWith(sampleExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (LoadResourceString(resourceName) is { } xaml)
            {
                if (GetSampleName(resourceName) is { } name)
                {
                    samples.Add(new SampleViewModel(name, xaml, string.Empty, Open, AutoRun));
                }
            }
        }

        return samples;
    }

    private void InitializeDockLayout()
    {
        var factory = new PlaygroundDockFactory(this);
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);
        factory.DockableHidden += DockFactoryOnDockableHidden;
        factory.DockableRestored += DockFactoryOnDockableRestored;

        DockFactory = factory;
        DockLayout = layout;
        CurrentDockPerspectiveId = factory.CurrentPerspectiveId;
        UpdateDockMenuState();
    }

    private void ToggleDockTool(string? id)
    {
        if (DockFactory is not PlaygroundDockFactory factory ||
            !factory.ToggleTool(id))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            QueueExtensionActivationForDockTool(id);
        }

        UpdateDockToolMenuState(factory);
    }

    private void ApplyDockPerspective(string? id)
    {
        if (DockFactory is not PlaygroundDockFactory factory)
        {
            return;
        }

        DockLayout = factory.CreatePerspectiveLayout(id);
        CurrentDockPerspectiveId = factory.CurrentPerspectiveId;
        UpdateDockMenuState();

        if (!string.IsNullOrWhiteSpace(id) &&
            ExtensionHost.GetPerspectiveContributions()
                .FirstOrDefault(perspective => perspective.Id.Equals(id, StringComparison.Ordinal)) is { } perspective)
        {
            foreach (var viewId in perspective.ViewIds)
            {
                QueueExtensionActivation(ExtensionActivationEvents.OnView(viewId));
            }
        }
    }

    private void RestoreDockTools()
    {
        if (DockFactory is not PlaygroundDockFactory factory ||
            !factory.RestoreAllTools())
        {
            return;
        }

        UpdateDockToolMenuState(factory);
    }

    private void DockFactoryOnDockableHidden(object? sender, DockableHiddenEventArgs e)
    {
        if (sender is PlaygroundDockFactory factory)
        {
            UpdateDockToolMenuState(factory);
        }
    }

    private void DockFactoryOnDockableRestored(object? sender, DockableRestoredEventArgs e)
    {
        if (sender is PlaygroundDockFactory factory)
        {
            UpdateDockToolMenuState(factory);
        }
    }

    private void UpdateDockMenuState()
    {
        if (DockFactory is PlaygroundDockFactory factory)
        {
            UpdateDockToolMenuState(factory);
        }

        foreach (var item in DockPerspectiveMenuItems)
        {
            item.IsCurrent = item.Id.Equals(CurrentDockPerspectiveId, StringComparison.Ordinal);
        }
    }

    private void UpdateDockToolMenuState(PlaygroundDockFactory factory)
    {
        foreach (var item in DockToolMenuItems)
        {
            item.IsVisible = factory.IsToolVisible(item.Id);
        }
    }

    private void LoadSolution(InMemorySolution solution)
    {
        StopRemotePreview();
        UnloadPreviousInProcessPreview();
        Control = null;
        DiagnosticsRoot = null;
        ClearDiagnosticsPreviewXamlFile();
        EnsureSolutionDocumentsOnCurrentThread(solution);
        Solution = solution;
        ActiveProject = SelectInitialProject(solution);
        SetSolutionExplorerNodes(solution);
        WorkspaceStatus = $"{solution.Name}: {solution.Projects.Count} project(s)";

        var firstXaml = SelectInitialXamlFile(ActiveProject);
        var firstCode = firstXaml is { } ? ActiveProject?.FindCodeBehind(firstXaml) : ActiveProject?.GetCSharpFiles().FirstOrDefault();

        ActiveXamlFile = firstXaml;
        ActiveCodeFile = firstCode;
        ActiveWorkspaceFile = firstXaml ?? firstCode;
        _visualEditorSelectedSelector = null;

        if (DockFactory is PlaygroundDockFactory factory)
        {
            var files = firstXaml is { }
                ? firstCode is { }
                    ? new[] { firstXaml, firstCode }
                    : new[] { firstXaml }
                : firstCode is { }
                    ? new[] { firstCode }
                    : Array.Empty<InMemoryProjectFile>();
            factory.ResetDocuments(files);
        }

        RefreshVisualEditingModel(updateSourceSelection: false);
        RefreshControlThemes();
        QueueExtensionActivation(ExtensionActivationEvents.OnWorkspaceOpened);
        QueueExtensionActivationForWorkspaceFile(ActiveWorkspaceFile);
    }

    private static void EnsureSolutionDocumentsOnCurrentThread(InMemorySolution solution)
    {
        foreach (var file in solution.Projects.SelectMany(static project => project.Files))
        {
            file.EnsureDocumentOnCurrentThread();
        }
    }

    private static InMemoryProject? SelectInitialProject(InMemorySolution solution)
    {
        return solution.Projects.FirstOrDefault(project => SelectInitialXamlFile(project) is { }) ??
               solution.Projects.FirstOrDefault();
    }

    private static InMemoryProjectFile? SelectInitialXamlFile(InMemoryProject? project)
    {
        return project?.GetXamlFiles()
            .FirstOrDefault(CanPreviewXamlFile)
            ?? project?.GetXamlFiles().FirstOrDefault();
    }

    private bool CanExportSolution()
    {
        return Solution is not null;
    }

    private async Task ExportSolution()
    {
        if (Solution is not { } solution || StorageProvider is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export solution",
            FileTypeChoices = GetSolutionFileTypes(),
            SuggestedFileName = $"{solution.Name}.xamlsln",
            DefaultExtension = "xamlsln",
            ShowOverwritePrompt = true
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var extension = Path.GetExtension(file.Name);
            var isStandardSolutionMetadataExport = IsStandardSolutionMetadataExtension(extension);
            var text = extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                ? StandardSolutionStorage.SaveSlnx(solution)
                : extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    ? StandardSolutionStorage.SaveSln(solution)
                    : SolutionStorage.Save(solution);
            await WriteStorageFileTextAsync(file, text);
            if (!isStandardSolutionMetadataExport)
            {
                MarkSolutionClean(solution);
            }

            WorkspaceStatus = isStandardSolutionMetadataExport
                ? $"Exported {extension} solution metadata for {solution.Name}. Use folder export for project files."
                : $"Exported solution {solution.Name}.";
        }
        catch (Exception exception)
        {
            WorkspaceStatus = $"Failed to export solution: {exception.Message}";
        }
    }

    private async Task ExportStandardSolutionFolder()
    {
        if (Solution is not { } solution || StorageProvider is null || !StorageProvider.CanPickFolder)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Export standard solution folder",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        try
        {
            await StandardSolutionStorage.ExportStandardSolutionFolderAsync(solution, folder);
            MarkSolutionClean(solution);
            WorkspaceStatus = $"Exported {solution.Name}.sln, {solution.Name}.slnx, and project files.";
        }
        catch (Exception exception)
        {
            WorkspaceStatus = $"Failed to export standard solution folder: {exception.Message}";
        }
    }

    private async Task ImportSolution()
    {
        if (StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import solution",
            FileTypeFilter = GetSolutionFileTypes(),
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();
            var solution = await LoadSolutionFileAsync(file, text);
            _openXamlFile = null;
            _openCodeFile = null;
            CurrentSample = null;
            Control = null;
            DiagnosticsRoot = null;
            ClearDiagnosticsPreviewXamlFile();
            LastErrorMessage = null;
            LoadSolution(solution);
            WorkspaceStatus = $"Imported solution {solution.Name}.";

            if (EnableAutoRun && CanPreviewXamlFile(ActiveXamlFile))
            {
                RunActiveDocument();
            }
        }
        catch (Exception exception)
        {
            WorkspaceStatus = $"Failed to import solution: {exception.Message}";
        }
    }

    private async Task OpenMsBuildWorkspace()
    {
        if (Utilities.IsBrowser())
        {
            await OpenMsBuildWorkspaceFolder();
            return;
        }

        if (StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open MSBuild solution or project",
            FileTypeFilter = GetMsBuildWorkspaceFileTypes(),
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        if (!file.Path.IsFile || !File.Exists(file.Path.LocalPath))
        {
            WorkspaceStatus = "The selected workspace file is not available as a local file.";
            return;
        }

        var localPath = file.Path.LocalPath;
        await LoadMsBuildWorkspaceAsync(progress => Task.Run(async () =>
            await MsBuildWorkspaceLoader.LoadLocalWorkspaceAsync(
                localPath,
                OnProjectFileChanged,
                progress)));
    }

    private async Task OpenMsBuildWorkspaceFolder()
    {
        if (StorageProvider is null || !StorageProvider.CanPickFolder)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Utilities.IsBrowser() ? "Select workspace directory" : "Select MSBuild workspace directory",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        await LoadMsBuildWorkspaceAsync(progress =>
        {
            if (!Utilities.IsBrowser() &&
                folder.Path.IsFile &&
                Directory.Exists(folder.Path.LocalPath))
            {
                var localPath = folder.Path.LocalPath;
                return Task.Run(async () =>
                    await MsBuildWorkspaceLoader.LoadLocalWorkspaceAsync(
                        localPath,
                        OnProjectFileChanged,
                        progress));
            }

            return MsBuildWorkspaceLoader.LoadStorageFolderAsync(
                folder,
                OnProjectFileChanged,
                progress);
        });
    }

    private async Task LoadMsBuildWorkspaceAsync(Func<IProgress<string>, Task<InMemorySolution>> loadSolution)
    {
        IsWorkspaceLoading = true;
        LastErrorMessage = null;
        WorkspaceStatus = "Loading MSBuild workspace...";

        try
        {
            var progress = new Progress<string>(message =>
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    WorkspaceStatus = message;
                }
            });

            var solution = await loadSolution(progress);

            _openXamlFile = null;
            _openCodeFile = null;
            CurrentSample = null;
            Control = null;
            DiagnosticsRoot = null;
            ClearDiagnosticsPreviewXamlFile();
            LoadSolution(solution);
            WorkspaceStatus = $"Loaded workspace {solution.Name}: {solution.Projects.Count} project(s).";

            if (EnableAutoRun && CanPreviewXamlFile(ActiveXamlFile))
            {
                RunActiveDocument();
            }
        }
        catch (Exception exception)
        {
            WorkspaceStatus = $"Failed to load MSBuild workspace: {FormatException(exception)}";
            LastErrorMessage = WorkspaceStatus;
        }
        finally
        {
            IsWorkspaceLoading = false;
        }
    }

    private bool CanSaveWorkspaceFile()
    {
        return ActiveWorkspaceFile?.CanSaveToSource == true;
    }

    private bool CanSaveAllWorkspaceFiles()
    {
        return Solution?.Projects
            .SelectMany(static project => project.Files)
            .Any(static file => file.IsDirty && file.CanSaveToSource) == true;
    }

    private async Task SaveWorkspaceFile()
    {
        if (ActiveWorkspaceFile is not { CanSaveToSource: true } file)
        {
            return;
        }

        try
        {
            await file.SaveToSourceAsync();
            WorkspaceStatus = $"Saved {file.Path}.";
            (SaveWorkspaceFileCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (SaveAllWorkspaceFilesCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
        catch (Exception exception)
        {
            WorkspaceStatus = $"Failed to save {file.Path}: {exception.Message}";
        }
    }

    private async Task SaveAllWorkspaceFiles()
    {
        if (Solution is null)
        {
            return;
        }

        var dirtyFiles = Solution.Projects
            .SelectMany(static project => project.Files)
            .Where(static file => file.IsDirty && file.CanSaveToSource)
            .ToArray();
        try
        {
            foreach (var file in dirtyFiles)
            {
                await file.SaveToSourceAsync();
            }

            WorkspaceStatus = dirtyFiles.Length == 0
                ? "No workspace files needed saving."
                : $"Saved {dirtyFiles.Length} workspace file(s).";
        }
        catch (Exception exception)
        {
            WorkspaceStatus = $"Failed to save workspace files: {exception.Message}";
        }

        (SaveWorkspaceFileCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SaveAllWorkspaceFilesCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    private async Task<InMemorySolution> LoadSolutionFileAsync(
        IStorageFile file,
        string text)
    {
        var extension = Path.GetExtension(file.Name);
        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadStandardSolutionFileAsync(file, text);
        }

        return SolutionStorage.Load(text, OnProjectFileChanged);
    }

    private async Task<InMemorySolution> LoadStandardSolutionFileAsync(
        IStorageFile file,
        string text)
    {
        if (!OperatingSystem.IsBrowser() &&
            file.Path.IsFile &&
            File.Exists(file.Path.LocalPath))
        {
            try
            {
                return StandardSolutionStorage.LoadFromLocalPath(file.Path.LocalPath, text, OnProjectFileChanged);
            }
            catch
            {
                // Fall back to an explicit root-folder picker below. Some storage providers expose
                // a path URI that cannot be used to read sibling project files.
            }
        }

        if (StorageProvider is null || !StorageProvider.CanPickFolder)
        {
            throw new InvalidDataException("Standard .sln/.slnx import needs access to the solution root folder.");
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select solution root folder",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            throw new InvalidDataException("Standard solution import was canceled before selecting a solution root folder.");
        }

        return await StandardSolutionStorage.LoadFromStorageFolderAsync(file.Name, text, folder, OnProjectFileChanged);
    }

    private static void MarkSolutionClean(InMemorySolution solution)
    {
        foreach (var file in solution.Projects.SelectMany(static project => project.Files))
        {
            file.MarkClean();
        }
    }

    private static bool IsStandardSolutionMetadataExtension(string extension)
    {
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    private ObservableCollection<SolutionExplorerNodeViewModel> BuildSolutionExplorer(InMemorySolution solution)
    {
        var solutionNode = new SolutionExplorerNodeViewModel($"Solution '{solution.Name}'", ProjectFileKind.Solution);
        foreach (var project in solution.Projects)
        {
            var projectParent = GetProjectParentNode(solutionNode, project);
            var projectNode = new SolutionExplorerNodeViewModel(project.Name, ProjectFileKind.Project, project: project);
            projectParent.Children.Add(projectNode);

            foreach (var file in project.Files.OrderBy(static file => file.Path, StringComparer.OrdinalIgnoreCase))
            {
                AddFileNode(projectNode, project, file);
            }
        }

        ApplyInitialSolutionExplorerExpansion(solutionNode);
        return new ObservableCollection<SolutionExplorerNodeViewModel> { solutionNode };
    }

    private static void ApplyInitialSolutionExplorerExpansion(SolutionExplorerNodeViewModel rootNode)
    {
        ApplyInitialSolutionExplorerExpansion(rootNode, 0);
    }

    private static void ApplyInitialSolutionExplorerExpansion(SolutionExplorerNodeViewModel node, int depth)
    {
        node.IsExpanded = node.Children.Count > 0 && depth <= 1;

        foreach (var child in node.Children)
        {
            ApplyInitialSolutionExplorerExpansion(child, depth + 1);
        }
    }

    private static SolutionExplorerNodeViewModel GetProjectParentNode(
        SolutionExplorerNodeViewModel solutionNode,
        InMemoryProject project)
    {
        var parent = solutionNode;
        var folderPath = project.SolutionFolderPath;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return parent;
        }

        foreach (var segment in folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var folder = parent.Children.FirstOrDefault(child =>
                child.Kind == ProjectFileKind.Folder &&
                string.Equals(child.Title, segment, StringComparison.OrdinalIgnoreCase));
            if (folder is null)
            {
                folder = new SolutionExplorerNodeViewModel(segment, ProjectFileKind.Folder);
                parent.Children.Add(folder);
            }

            parent = folder;
        }

        return parent;
    }

    private void SetSolutionExplorerNodes(InMemorySolution? solution)
    {
        CancelPendingSolutionExplorerSearch();
        _allSolutionExplorerNodes = solution is { }
            ? BuildSolutionExplorer(solution)
            : new ObservableCollection<SolutionExplorerNodeViewModel>();
        ApplySolutionExplorerSearch();
    }

    private void ScheduleSolutionExplorerSearch()
    {
        var revision = ++_solutionExplorerSearchRevision;
        _solutionExplorerSearchThrottle?.Dispose();
        _solutionExplorerSearchThrottle = null;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            _ = ApplySolutionExplorerSearchAfterDelayAsync(revision);
            return;
        }

        StartSolutionExplorerSearchTimer(revision);
    }

    private async Task ApplySolutionExplorerSearchAfterDelayAsync(int revision)
    {
        await Task.Delay(SolutionExplorerSearchDelay);
        if (revision == _solutionExplorerSearchRevision)
        {
            ApplySolutionExplorerSearch();
        }
    }

    private void StartSolutionExplorerSearchTimer(int revision)
    {
        if (revision != _solutionExplorerSearchRevision)
        {
            return;
        }

        _solutionExplorerSearchThrottle?.Dispose();
        _solutionExplorerSearchThrottle = DispatcherTimer.RunOnce(() =>
        {
            _solutionExplorerSearchThrottle = null;
            if (revision == _solutionExplorerSearchRevision)
            {
                ApplySolutionExplorerSearch();
            }
        }, SolutionExplorerSearchDelay);
    }

    private void CancelPendingSolutionExplorerSearch()
    {
        _solutionExplorerSearchRevision++;
        _solutionExplorerSearchThrottle?.Dispose();
        _solutionExplorerSearchThrottle = null;
    }

    private void ApplySolutionExplorerSearch()
    {
        SelectedSolutionExplorerNode = null;
        SolutionExplorerSearchError = null;

        var searchText = SolutionExplorerSearchText.Trim();
        if (searchText.Length == 0)
        {
            SolutionExplorerNodes = _allSolutionExplorerNodes;
            return;
        }

        Func<SolutionExplorerNodeViewModel, bool> isMatch;
        if (SolutionExplorerSearchUseRegex)
        {
            Regex regex;
            try
            {
                regex = new Regex(
                    searchText,
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                    SolutionExplorerRegexTimeout);
            }
            catch (Exception exception) when (exception is ArgumentException or RegexParseException)
            {
                SolutionExplorerSearchError = $"Invalid regex: {exception.Message}";
                SolutionExplorerNodes = _allSolutionExplorerNodes;
                return;
            }

            isMatch = node => regex.IsMatch(node.SearchText);
        }
        else
        {
            var normalizedSearchText = searchText.ToLowerInvariant();
            isMatch = node => node.MatchesLiteralSearch(normalizedSearchText);
        }

        var filteredNodes = new ObservableCollection<SolutionExplorerNodeViewModel>();
        try
        {
            foreach (var node in _allSolutionExplorerNodes)
            {
                if (FilterSolutionExplorerNode(node, isMatch) is { } filteredNode)
                {
                    filteredNodes.Add(filteredNode);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            SolutionExplorerSearchError = "Regex search timed out.";
            SolutionExplorerNodes = _allSolutionExplorerNodes;
            return;
        }

        SolutionExplorerNodes = filteredNodes;
    }

    private static SolutionExplorerNodeViewModel? FilterSolutionExplorerNode(
        SolutionExplorerNodeViewModel node,
        Func<SolutionExplorerNodeViewModel, bool> isMatch)
    {
        if (isMatch(node))
        {
            node.IsExpanded = true;
            return node;
        }

        List<SolutionExplorerNodeViewModel>? filteredChildren = null;
        foreach (var child in node.Children)
        {
            if (FilterSolutionExplorerNode(child, isMatch) is { } filteredChild)
            {
                filteredChildren ??= new List<SolutionExplorerNodeViewModel>();
                filteredChildren.Add(filteredChild);
            }
        }

        if (filteredChildren is null)
        {
            return null;
        }

        var clone = node.CloneShallow();
        foreach (var filteredChild in filteredChildren)
        {
            clone.Children.Add(filteredChild);
        }

        clone.IsExpanded = true;
        return clone;
    }

    private void AddFileNode(SolutionExplorerNodeViewModel projectNode, InMemoryProject project, InMemoryProjectFile file)
    {
        var segments = file.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var parent = projectNode;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var folderName = segments[i];
            var folder = parent.Children.FirstOrDefault(child =>
                child.Kind == ProjectFileKind.Folder &&
                string.Equals(child.Title, folderName, StringComparison.OrdinalIgnoreCase));
            if (folder is null)
            {
                folder = new SolutionExplorerNodeViewModel(folderName, ProjectFileKind.Folder, project: project);
                parent.Children.Add(folder);
            }

            parent = folder;
        }

        parent.Children.Add(new SolutionExplorerNodeViewModel(
            file.Name,
            file.Kind,
            new RelayCommand(() => OpenWorkspaceFile(file)),
            project,
            file));
    }

    private string GetUntitledSampleName()
    {
        const string name = "Untitled";
        if (!Samples.Any(x => string.Equals(x.Name, name, StringComparison.Ordinal)))
        {
            return name;
        }

        var index = 2;
        while (Samples.Any(x => string.Equals(x.Name, $"{name} {index}", StringComparison.Ordinal)))
        {
            index++;
        }

        return $"{name} {index}";
    }

    private void NewFile()
    {
        if (ActiveProject is { })
        {
            AddUserControl();
            return;
        }

        CreateUntitledSample();
    }

    private void Open(SampleViewModel sampleViewModel)
    {
        if (ReferenceEquals(CurrentSample, sampleViewModel))
        {
            OpenCurrentSample(sampleViewModel);
            return;
        }

        CurrentSample = sampleViewModel;
    }

    private void OpenCurrentSample(SampleViewModel sampleViewModel)
    {
        if (_openingSample)
        {
            return;
        }

        _openingSample = true;
        try
        {
            Control = null;
            DiagnosticsRoot = null;
            ClearDiagnosticsPreviewXamlFile();
            LastErrorMessage = null;
            _openXamlFile = null;
            _openCodeFile = null;

            LoadSolution(_solutionFactory.CreateSampleSolution(sampleViewModel.Name, sampleViewModel.Xaml, sampleViewModel.Code));

            if (EnableAutoRun)
            {
                RunActiveDocument();
            }
        }
        finally
        {
            _openingSample = false;
        }
    }

    private void CreateUntitledSample()
    {
        _openXamlFile = null;
        _openCodeFile = null;

        var sample = new SampleViewModel(GetUntitledSampleName(), Templates.s_newXaml, Templates.s_newCode, Open, AutoRun);
        Samples.Insert(0, sample);
        CurrentSample = sample;
    }

    private void ShowNewProjectWizard()
    {
        NewProjectWizard.SolutionName = GetUniqueSolutionName();
        NewProjectWizard.SelectedTemplate ??= NewProjectWizard.Templates.FirstOrDefault();
        NewProjectWizard.IsOpen = true;
    }

    private void CreateProjectFromWizard()
    {
        var template = NewProjectWizard.SelectedTemplate ?? NewProjectWizard.Templates.FirstOrDefault();
        if (template is null)
        {
            return;
        }

        var solutionName = string.IsNullOrWhiteSpace(NewProjectWizard.SolutionName)
            ? GetUniqueSolutionName()
            : NewProjectWizard.SolutionName.Trim();

        CurrentSample = null;
        LoadSolution(_solutionFactory.CreateSolution(solutionName, template));
        NewProjectWizard.IsOpen = false;
        LastErrorMessage = null;
        RunActiveDocument();
    }

    private string GetUniqueSolutionName()
    {
        var index = 1;
        var name = $"App{index}";
        while (Solution?.Name == name || Samples.Any(sample => sample.Name == name))
        {
            index++;
            name = $"App{index}";
        }

        return name;
    }

    private void AddUserControl()
    {
        if (ActiveProject is not { } project)
        {
            return;
        }

        var file = _solutionFactory.AddUserControl(project);
        SetSolutionExplorerNodes(Solution);
        RefreshControlThemes();
        OpenWorkspaceFile(file);
    }

    private void AddResourceDictionary()
    {
        if (ActiveProject is not { } project)
        {
            return;
        }

        var file = _solutionFactory.AddResourceDictionary(project);
        SetSolutionExplorerNodes(Solution);
        RefreshControlThemes();
        OpenWorkspaceFile(file);
    }

    internal void OpenWorkspaceFile(InMemoryProjectFile file)
    {
        if (!file.CanEdit)
        {
            return;
        }

        ActiveProject = FindProjectForFile(file) ?? ActiveProject;
        ActivateWorkspaceFileFromDocument(file);
        if (DockFactory is PlaygroundDockFactory factory)
        {
            factory.OpenDocument(file);
        }
    }

    internal void OpenWorkspaceFileLocation(InMemoryProjectFile file, int startOffset, int length)
    {
        OpenWorkspaceFile(file);
        WorkspaceEditorNavigationFilePath = file.Path;
        WorkspaceEditorNavigationStart = Math.Max(0, startOffset);
        WorkspaceEditorNavigationLength = Math.Max(0, length);
        WorkspaceEditorNavigationVersion++;
    }

    internal void ActivateWorkspaceFileFromDocument(InMemoryProjectFile file)
    {
        var project = FindProjectForFile(file) ?? ActiveProject;
        if (project is null)
        {
            return;
        }

        var previousProject = ActiveProject;
        ActiveProject = project;
        ActiveWorkspaceFile = file;
        var previousPreviewFile = ActiveXamlFile;
        var projectChanged = !ReferenceEquals(previousProject, project);

        if (file.IsXaml)
        {
            ActiveXamlFile = file;
            ActiveCodeFile = project.FindCodeBehind(file) ?? ActiveCodeFile;
        }
        else if (file.IsCSharp)
        {
            ActiveCodeFile = file;
            ActiveXamlFile = project.FindXamlForCodeBehind(file) ??
                             (projectChanged && !ProjectContainsFile(project, ActiveXamlFile)
                                 ? SelectInitialXamlFile(project)
                                 : ActiveXamlFile);
        }
        else if (projectChanged && !ProjectContainsFile(project, ActiveXamlFile))
        {
            ActiveXamlFile = SelectInitialXamlFile(project);
        }

        var xamlFileChanged = !ReferenceEquals(previousPreviewFile, ActiveXamlFile);
        if (xamlFileChanged)
        {
            _visualEditorSelectedSelector = null;
        }

        WorkspaceStatus = $"{project.Name}: {file.Path}";
        RefreshVisualEditingModel(updateSourceSelection: !xamlFileChanged);
        QueueExtensionActivationForWorkspaceFile(file);

        if (!_openingSample &&
            EnableAutoRun &&
            CanPreviewXamlFile(ActiveXamlFile) &&
            !ReferenceEquals(previousPreviewFile, ActiveXamlFile))
        {
            Control = null;
            DiagnosticsRoot = null;
            ClearDiagnosticsPreviewXamlFile();
            LastErrorMessage = null;
            RunActiveDocument();
        }
    }

    private InMemoryProject? FindProjectForFile(InMemoryProjectFile file)
    {
        return Solution?.Projects.FirstOrDefault(project =>
            project.Files.Any(projectFile => ReferenceEquals(projectFile, file)));
    }

    private static bool ProjectContainsFile(InMemoryProject project, InMemoryProjectFile? file)
    {
        return file is { } && project.Files.Any(projectFile => ReferenceEquals(projectFile, file));
    }

    private static List<FilePickerFileType> GetXamlFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.Axaml,
            StorageService.Xaml,
            StorageService.All
        };
    }

    private static List<FilePickerFileType> GetThemeResourceFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.Axaml,
            StorageService.Xaml,
            StorageService.All
        };
    }

    private static List<FilePickerFileType> GetThemeProjectFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.ThemeProject,
            StorageService.Json,
            StorageService.All
        };
    }

    private static List<FilePickerFileType> GetSolutionFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.SolutionProject,
            StorageService.VisualStudioXmlSolution,
            StorageService.VisualStudioSolution,
            StorageService.Json,
            StorageService.All
        };
    }

    private static List<FilePickerFileType> GetMsBuildWorkspaceFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.VisualStudioSolution,
            StorageService.VisualStudioXmlSolution,
            StorageService.MSBuildProject,
            StorageService.All
        };
    }

    private static List<FilePickerFileType> GetCodeFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.CSharp,
            StorageService.All
        };
    }

    private void AutoRun(SampleViewModel sampleViewModel)
    {
        if (EnableAutoRun && ReferenceEquals(sampleViewModel, CurrentSample))
        {
            RunActiveDocument();
        }
    }

    private void OnProjectFileChanged(InMemoryProjectFile file)
    {
        if (ReferenceEquals(file, ActiveWorkspaceFile))
        {
            (SaveWorkspaceFileCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        (SaveAllWorkspaceFilesCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();

        if (_isApplyingDesignInspectionEdit)
        {
            return;
        }

        var resourceChanged = file.Kind == ProjectFileKind.Resource;
        if (resourceChanged)
        {
            RefreshControlThemes();
        }

        if (file.IsXaml)
        {
            RefreshThemeResourceAnalysis();
            RefreshDesignInspection();
        }

        if (ReferenceEquals(file, ActiveXamlFile))
        {
            RefreshVisualEditingModel(updateSourceSelection: false);
        }

        // Diagnostics edits already changed the live control; avoid reloading the preview and DevTools host.
        if (ReferenceEquals(file, ActiveXamlFile) &&
            ShouldSuppressPreviewReloadForDiagnosticsMutation(file))
        {
            return;
        }

        if (!EnableAutoRun)
        {
            return;
        }

        if (ReferenceEquals(file, ActiveXamlFile) && !CanPreviewXamlFile(ActiveXamlFile))
        {
            return;
        }

        if (resourceChanged &&
            file.IncludeInRuntimePreview &&
            !ReferenceEquals(file, ActiveXamlFile) &&
            CanPreviewXamlFile(ActiveXamlFile))
        {
            RunActiveDocument();
            return;
        }

        if (!ReferenceEquals(file, ActiveXamlFile) && !ReferenceEquals(file, ActiveCodeFile))
        {
            return;
        }

        RunActiveDocument();
    }

    private void RunActiveDocument()
    {
        if (ActiveXamlFile is { } activeXamlFile &&
            ShouldSuppressPreviewReloadForDiagnosticsMutation(activeXamlFile))
        {
            return;
        }

        _timer?.Dispose();
        _timer = DispatcherTimer.RunOnce(() => _ = RunInternal(), AutoRunDelay);
    }

    private async Task RunInternal()
    {
        if (_update)
        {
            _rerunRequested = true;
            return;
        }

        _update = true;
        var diagnosticsMessage = default(string);
        var xamlDiagnostics = new List<RuntimeXamlDiagnostic>();
        WorkspacePreviewAssemblyScope? previewAssemblyScope = null;
        var xamlFile = ActiveXamlFile;
        var project = ActiveProject;
        ClearDiagnosticsPreviewXamlFile();

        try
        {
            // Control = null;
#if false
            if (!Utilities.IsBrowser())
            {
                // TODO: Unload previously loaded assembly.
                if (_previous is { })
                {
                    _previous?.Context?.Unload();
                    _previous = null;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
#endif
            Assembly? scriptAssembly = null;
            if (xamlFile is null)
            {
                LastErrorMessage = "No XAML document is active.";
                return;
            }

            if (!CanPreviewXamlFile(xamlFile))
            {
                LastErrorMessage = $"{xamlFile.Path} cannot be previewed.";
                return;
            }

            var xamlProject = FindProjectForFile(xamlFile);
            if (project is { } && xamlProject is { } && !ReferenceEquals(project, xamlProject))
            {
                project = xamlProject;
                ActiveProject = xamlProject;
            }

            if (!TryValidateXml(xamlFile.Text, out var xmlErrorMessage))
            {
                LastErrorMessage = xmlErrorMessage;
                return;
            }
            var xamlText = xamlFile.Text;

            var codeFiles = project?.GetCSharpFileSnapshot() ?? Array.Empty<(string Path, string Text)>();
            var workspaceReferences = project?.AssemblyReferences.ToArray() ?? Array.Empty<WorkspaceAssemblyReference>();

            if (ShouldUseRemoteWorkspacePreview(project) &&
                await TryRunRemoteWorkspacePreviewAsync(project!, xamlFile, xamlText))
            {
                return;
            }

            if (project is { } && codeFiles.Length > 0)
            {
                try
                {
                    var scriptResult = await Task.Run(async () => await CompilerService.GetProjectAssembly(
                        string.IsNullOrWhiteSpace(project.AssemblyName) ? project.Name : project.AssemblyName,
                        codeFiles,
                        workspaceReferences,
                        project.CSharpParseOptions,
                        project.CSharpCompilationOptions));
                    diagnosticsMessage = FormatCompilerDiagnostics(scriptResult.Diagnostics);

                    if (scriptResult.Success && scriptResult.Assembly is { })
                    {
                        scriptAssembly = scriptResult.Assembly;
                        previewAssemblyScope = new WorkspacePreviewAssemblyScope(
                            scriptResult.Assembly,
                            scriptResult.Context,
                            scriptResult.LoadedAssemblies);
                        XamlIntelliSenseService.RegisterWorkspaceAssemblies(
                            scriptResult.LoadedAssemblies.Concat(new[] { scriptAssembly }));
                        Console.WriteLine($"Compiled assembly: {scriptAssembly?.GetName().Name}");
                    }
                    else
                    {
                        var fallback = await TryUseWorkspaceOutputAssemblyFallbackAsync(
                            project,
                            workspaceReferences,
                            diagnosticsMessage,
                            xamlFile);
                        if (fallback.Success)
                        {
                            scriptAssembly = fallback.Assembly;
                            previewAssemblyScope = fallback.AssemblyScope;
                            diagnosticsMessage = fallback.DiagnosticsMessage;
                        }
                        else
                        {
                            LastErrorMessage = fallback.DiagnosticsMessage ?? diagnosticsMessage ?? "Failed to compile code.";
                            return;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    var exceptionMessage = FormatException(exception);
                    var fallback = await TryUseWorkspaceOutputAssemblyFallbackAsync(
                        project,
                        workspaceReferences,
                        exceptionMessage,
                        xamlFile);
                    if (fallback.Success)
                    {
                        scriptAssembly = fallback.Assembly;
                        previewAssemblyScope = fallback.AssemblyScope;
                        diagnosticsMessage = fallback.DiagnosticsMessage;
                    }
                    else
                    {
                        LastErrorMessage = fallback.DiagnosticsMessage ?? exceptionMessage;
                        return;
                    }
                }
            }
            else if (project is { IsMsBuildWorkspace: true })
            {
                if (TryGetDirtyWorkspaceBuildInput(project, xamlFile, out var dirtyFile))
                {
                    LastErrorMessage = GetUnsavedWorkspaceBuildInputMessage(dirtyFile);
                    return;
                }

                var buildRequired = ShouldBuildWorkspaceProjectBeforeUsingOutput(project);
                if (buildRequired && !await TryBuildWorkspaceProjectAsync(project))
                {
                    LastErrorMessage = "Project output assembly is stale or missing and rebuild failed.";
                    return;
                }

                if (buildRequired)
                {
                    workspaceReferences = project.AssemblyReferences.ToArray();
                }

                if (TryLoadWorkspaceOutputAssembly(project, workspaceReferences, out var outputAssembly, out var outputContext, out var loadedAssemblies))
                {
                    scriptAssembly = outputAssembly;
                    previewAssemblyScope = new WorkspacePreviewAssemblyScope(outputAssembly, outputContext, loadedAssemblies);
                    XamlIntelliSenseService.RegisterWorkspaceAssemblies(new[] { outputAssembly }.Concat(loadedAssemblies));
                    Console.WriteLine($"Loaded workspace output assembly: {outputAssembly.GetName().Name}");
                }
            }

            var projectResourceFiles = project?.GetXamlFiles()
                .Where(static file => file.Kind == ProjectFileKind.Resource && file.IncludeInRuntimePreview)
                .Select(static file => (file.Path, file.Text))
                .ToArray() ?? Array.Empty<(string Path, string Text)>();
            var documentAssemblyName = project is { } && !string.IsNullOrWhiteSpace(project.AssemblyName)
                ? project.AssemblyName
                : null;

            RuntimeXamlPreviewLoader.ApplyProjectResources(
                projectResourceFiles,
                scriptAssembly,
                xamlDiagnostics,
                documentAssemblyName);

            if (scriptAssembly is { })
            {
                var control = xamlFile.Kind == ProjectFileKind.Resource
                    ? RuntimeXamlPreviewLoader.LoadResourceDictionaryPreview(
                        xamlText,
                        scriptAssembly,
                        xamlFile.Path,
                        xamlDiagnostics,
                        projectResourceFiles,
                        documentAssemblyName)
                    : RuntimeXamlPreviewLoader.LoadControl(
                        xamlText,
                        scriptAssembly,
                        Path.GetFileNameWithoutExtension(xamlFile.Name),
                        xamlFile.Path,
                        xamlDiagnostics,
                        projectResourceFiles,
                        documentAssemblyName);
                if (control is { })
                {
                    ShowControl(
                        xamlFile,
                        control,
                        xamlText,
                        CombineDiagnostics(diagnosticsMessage, FormatXamlDiagnostics(xamlDiagnostics)),
                        previewAssemblyScope);
                    MarkPreviewLoaded(project, xamlFile, PreviewHostCapabilities.InlineDesign);
                    previewAssemblyScope = null;
                }
            }
            else
            {
                var control = xamlFile.Kind == ProjectFileKind.Resource
                    ? RuntimeXamlPreviewLoader.LoadResourceDictionaryPreview(
                        xamlText,
                        null,
                        xamlFile.Path,
                        xamlDiagnostics,
                        projectResourceFiles,
                        documentAssemblyName)
                    : RuntimeXamlPreviewLoader.LoadControl(
                        xamlText,
                        null,
                        null,
                        xamlFile.Path,
                        xamlDiagnostics,
                        projectResourceFiles,
                        documentAssemblyName);
                if (control is { })
                {
                    ShowControl(
                        xamlFile,
                        control,
                        xamlText,
                        CombineDiagnostics(diagnosticsMessage, FormatXamlDiagnostics(xamlDiagnostics)),
                        previewAssemblyScope);
                    MarkPreviewLoaded(project, xamlFile, PreviewHostCapabilities.InlineDesign);
                    previewAssemblyScope = null;
                }
            }
        }
        catch (Exception exception)
        {
            LastErrorMessage = CombineDiagnostics(
                diagnosticsMessage,
                FormatXamlDiagnostics(xamlDiagnostics),
                FormatException(exception));
            Console.WriteLine(LastErrorMessage);
        }
        finally
        {
            previewAssemblyScope?.Unload();
            _update = false;
            if (_rerunRequested)
            {
                _rerunRequested = false;
                RunActiveDocument();
            }
        }
    }

    private void MarkPreviewLoaded(
        InMemoryProject? project,
        InMemoryProjectFile xamlFile,
        PreviewHostCapabilities capabilities)
    {
        QueueExtensionActivationForPreview(capabilities);
        if (project is null)
        {
            PreviewReloadStrategy = PreviewReloadStrategy.InlineReload;
            PreviewStatus = "Reloading inline design preview.";
            return;
        }

        var snapshot = PreviewSnapshotFactory.Create(
            Solution,
            project,
            xamlFile,
            project.OutputAssemblyPath);
        var plan = _previewSessionManager.PlanNext(snapshot, capabilities);
        _previewSessionManager.MarkLoaded(snapshot);
        PreviewReloadStrategy = plan.Strategy;
        PreviewStatus = plan.Description;
    }

    private bool TryUseWorkspaceOutputAssemblyFallback(
        InMemoryProject project,
        IReadOnlyList<WorkspaceAssemblyReference> workspaceReferences,
        string? failureMessage,
        out WorkspacePreviewAssemblyScope? assemblyScope,
        out string? diagnosticsMessage)
    {
        assemblyScope = null;
        diagnosticsMessage = failureMessage;
        if (!project.IsMsBuildWorkspace ||
            !TryLoadWorkspaceOutputAssembly(project, workspaceReferences, out var outputAssembly, out var outputContext, out var loadedAssemblies))
        {
            return false;
        }

        assemblyScope = new WorkspacePreviewAssemblyScope(outputAssembly, outputContext, loadedAssemblies);
        diagnosticsMessage = "Using built workspace assembly for MSBuild project references and generated code.";
        XamlIntelliSenseService.RegisterWorkspaceAssemblies(new[] { outputAssembly }.Concat(loadedAssemblies));
        Console.WriteLine($"Loaded workspace output assembly after live compile failure: {outputAssembly.GetName().Name}");
        return true;
    }

    private async Task<bool> TryRunRemoteWorkspacePreviewAsync(
        InMemoryProject project,
        InMemoryProjectFile xamlFile,
        string xamlText)
    {
        if (TryGetDirtyWorkspaceBuildInput(project, xamlFile, out var dirtyFile))
        {
            StopRemotePreview();
            LastErrorMessage = GetUnsavedWorkspaceBuildInputMessage(dirtyFile);
            return true;
        }

        project.OutputAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project) ?? project.OutputAssemblyPath;
        if (ShouldBuildWorkspaceProjectBeforeRemotePreview(project, xamlFile))
        {
            StopRemotePreview();
            if (!await TryBuildWorkspaceProjectAsync(project) ||
                string.IsNullOrWhiteSpace(project.OutputAssemblyPath) ||
                !File.Exists(project.OutputAssemblyPath))
            {
                LastErrorMessage = "Project output assembly not found. Build the project first.";
                return true;
            }
        }

        var outputAssemblyPath = project.OutputAssemblyPath;
        if (string.IsNullOrWhiteSpace(outputAssemblyPath) ||
            !File.Exists(outputAssemblyPath))
        {
            LastErrorMessage = "Project output assembly not found. Build the project first.";
            return true;
        }

        var previousScope = _previous;
        var projectDirectory = ResolveWorkspaceDirectory(Path.GetDirectoryName(project.ProjectFilePath)) ??
                               ResolveWorkspaceDirectory(Path.GetDirectoryName(outputAssemblyPath));
        var projectPath = BuildRemotePreviewXamlProjectPath(xamlFile.Path, projectDirectory);
        var snapshot = PreviewSnapshotFactory.Create(
            Solution,
            project,
            xamlFile,
            outputAssemblyPath);
        var capabilities = PreviewHostCapabilities.RemoteDesigner;
        QueueExtensionActivationForPreview(capabilities);
        var plan = _previewSessionManager.PlanNext(snapshot, capabilities);
        PreviewReloadStrategy = plan.Strategy;
        PreviewStatus = plan.Description;
        var result = await _remotePreviewService.StartOrUpdateAsync(
            xamlText,
            outputAssemblyPath,
            projectPath,
            projectDirectory);
        if (!result.IsSuccess)
        {
            LastErrorMessage = result.ErrorMessage;
            return true;
        }

        Control = null;
        DiagnosticsRoot = null;
        ClearDiagnosticsPreviewXamlFile();
        IsRemotePreviewActive = true;
        LastErrorMessage = "Using isolated workspace preview host.";
        _previewSessionManager.MarkLoaded(snapshot);
        _previous = null;
        previousScope?.Unload();
        return true;
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "Workspace preview compares host assembly files when running from a normal desktop build output.")]
    private static bool ShouldUseRemoteWorkspacePreview(InMemoryProject? project)
    {
        if (Utilities.IsBrowser() ||
            project is not { IsMsBuildWorkspace: true })
        {
            return false;
        }

        var targetAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project);
        var outputDirectory = string.IsNullOrWhiteSpace(targetAssemblyPath)
            ? null
            : Path.GetDirectoryName(targetAssemblyPath);
        return WorkspaceAssemblyDiffersFromHost(project, outputDirectory, "Avalonia.Base", typeof(AvaloniaObject).Assembly.Location) ||
               WorkspaceAssemblyDiffersFromHost(project, outputDirectory, "Avalonia.Controls", typeof(Control).Assembly.Location) ||
               WorkspaceAssemblyDiffersFromHost(project, outputDirectory, "Avalonia.Markup.Xaml", typeof(AvaloniaXamlLoader).Assembly.Location);
    }

    private static bool WorkspaceAssemblyDiffersFromHost(
        InMemoryProject project,
        string? outputDirectory,
        string assemblyName,
        string hostAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(hostAssemblyPath) ||
            !File.Exists(hostAssemblyPath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory))
        {
            var candidate = Path.Combine(outputDirectory, assemblyName + ".dll");
            if (File.Exists(candidate) && !FilesHaveSameContent(candidate, hostAssemblyPath))
            {
                return true;
            }
        }

        return project.AssemblyReferences
            .Where(reference =>
                reference.IsRuntimeAssembly &&
                string.Equals(reference.Name, assemblyName, StringComparison.OrdinalIgnoreCase) &&
                reference.FilePath is { } &&
                File.Exists(reference.FilePath))
            .Any(reference => !FilesHaveSameContent(reference.FilePath!, hostAssemblyPath));
    }

    private static bool FilesHaveSameContent(string firstPath, string secondPath)
    {
        try
        {
            using var first = File.OpenRead(firstPath);
            using var second = File.OpenRead(secondPath);
            if (first.Length != second.Length)
            {
                return false;
            }

            var firstBuffer = new byte[8192];
            var secondBuffer = new byte[8192];
            while (true)
            {
                var firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
                var secondRead = second.Read(secondBuffer, 0, secondBuffer.Length);
                if (firstRead != secondRead)
                {
                    return false;
                }

                if (firstRead == 0)
                {
                    return true;
                }

                for (var i = 0; i < firstRead; i++)
                {
                    if (firstBuffer[i] != secondBuffer[i])
                    {
                        return false;
                    }
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static string BuildRemotePreviewXamlProjectPath(string xamlFilePath, string? projectDirectory)
    {
        if (!Path.IsPathRooted(xamlFilePath))
        {
            return EnsureRemotePreviewProjectPath(xamlFilePath);
        }

        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return EnsureRemotePreviewProjectPath(xamlFilePath);
        }

        try
        {
            var relative = Path.GetRelativePath(projectDirectory, xamlFilePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            return EnsureRemotePreviewProjectPath(relative);
        }
        catch
        {
            return EnsureRemotePreviewProjectPath(xamlFilePath);
        }
    }

    private static string EnsureRemotePreviewProjectPath(string path)
    {
        path = path.Replace('\\', '/');
        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }

    private async Task<WorkspaceOutputFallbackResult> TryUseWorkspaceOutputAssemblyFallbackAsync(
        InMemoryProject project,
        IReadOnlyList<WorkspaceAssemblyReference> workspaceReferences,
        string? failureMessage,
        InMemoryProjectFile? activeXamlFile)
    {
        if (TryGetDirtyWorkspaceBuildInput(project, activeXamlFile, out var dirtyFile))
        {
            return new WorkspaceOutputFallbackResult(
                false,
                null,
                GetUnsavedWorkspaceBuildInputMessage(dirtyFile));
        }

        var buildRequired = ShouldBuildWorkspaceProjectBeforeUsingOutput(project);
        if (buildRequired)
        {
            if (!await TryBuildWorkspaceProjectAsync(project))
            {
                return new WorkspaceOutputFallbackResult(false, null, failureMessage);
            }

            workspaceReferences = project.AssemblyReferences.ToArray();
        }

        if (TryUseWorkspaceOutputAssemblyFallback(
                project,
                workspaceReferences,
                failureMessage,
                out var assemblyScope,
                out var diagnosticsMessage))
        {
            return new WorkspaceOutputFallbackResult(true, assemblyScope, diagnosticsMessage);
        }

        if (buildRequired || !await TryBuildWorkspaceProjectAsync(project))
        {
            return new WorkspaceOutputFallbackResult(false, null, failureMessage);
        }

        var refreshedWorkspaceReferences = project.AssemblyReferences.ToArray();

        return TryUseWorkspaceOutputAssemblyFallback(
            project,
            refreshedWorkspaceReferences,
            failureMessage,
            out assemblyScope,
            out diagnosticsMessage)
            ? new WorkspaceOutputFallbackResult(true, assemblyScope, diagnosticsMessage)
            : new WorkspaceOutputFallbackResult(false, null, failureMessage);
    }

    private async Task<bool> TryBuildWorkspaceProjectAsync(InMemoryProject project)
    {
        var projectFilePath = ResolveWorkspaceFilePath(project.ProjectFilePath);
        if (!project.IsMsBuildWorkspace ||
            string.IsNullOrWhiteSpace(projectFilePath) ||
            !File.Exists(projectFilePath))
        {
            return false;
        }

        try
        {
            WorkspaceStatus = $"Building {project.Name}...";
            var result = await RunDotNetBuildAsync(
                projectFilePath,
                noRestore: !ShouldRestoreWorkspaceProjectBeforeBuild(project));
            if (result.ExitCode == 0)
            {
                project.OutputAssemblyPath = ResolveWorkspaceTargetAssemblyPath(
                    project,
                    preferCached: false,
                    preferNewest: true) ?? project.OutputAssemblyPath;
                AddWorkspaceOutputDirectoryReferences(project);
                WorkspaceStatus = $"{project.Name}: build refreshed.";
                return true;
            }

            Console.WriteLine($"dotnet build failed for {projectFilePath}:{Environment.NewLine}{string.Join(Environment.NewLine, result.OutputLines)}");
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }

        return false;
    }

    private static bool ShouldRestoreWorkspaceProjectBeforeBuild(InMemoryProject project)
    {
        var targetAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project) ?? project.OutputAssemblyPath;
        if (string.IsNullOrWhiteSpace(targetAssemblyPath) || !File.Exists(targetAssemblyPath))
        {
            return true;
        }

        DateTime outputWriteTime;
        try
        {
            outputWriteTime = File.GetLastWriteTimeUtc(targetAssemblyPath);
        }
        catch
        {
            return true;
        }

        foreach (var file in project.Files.Where(static file => file.Kind == ProjectFileKind.ProjectFile))
        {
            var projectInputPath = ResolveWorkspaceProjectInputPath(project, file);
            if (string.IsNullOrWhiteSpace(projectInputPath) || !File.Exists(projectInputPath))
            {
                continue;
            }

            try
            {
                if (File.GetLastWriteTimeUtc(projectInputPath) > outputWriteTime)
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }
        }

        return false;
    }

    private static void AddWorkspaceOutputDirectoryReferences(InMemoryProject project)
    {
        var targetAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project);
        var outputDirectory = Path.GetDirectoryName(targetAssemblyPath);
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(outputDirectory)
                     .Where(WorkspaceAssemblyReference.IsAssemblyFile)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (WorkspaceAssemblyReference.FromPath(file, isRuntimeAssembly: true) is not { } reference ||
                project.AssemblyReferences.Any(existing =>
                    string.Equals(existing.Name, reference.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.FilePath, reference.FilePath, StringComparison.OrdinalIgnoreCase) &&
                    existing.IsRuntimeAssembly == reference.IsRuntimeAssembly))
            {
                continue;
            }

            project.AssemblyReferences.Add(reference);
        }
    }

    private static bool TryLoadWorkspaceOutputAssembly(
        InMemoryProject project,
        IReadOnlyList<WorkspaceAssemblyReference> workspaceReferences,
        out Assembly assembly,
        out WorkspaceAssemblyLoadContext context,
        out IReadOnlyList<Assembly> loadedAssemblies)
    {
        var targetAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project);
        project.OutputAssemblyPath = targetAssemblyPath ?? project.OutputAssemblyPath;
        var outputDirectory = Path.GetDirectoryName(project.OutputAssemblyPath);
        foreach (var reference in GetWorkspaceOutputAssemblyCandidates(project, workspaceReferences))
        {
            var mainAssemblyPath = reference.FilePath is { } && File.Exists(reference.FilePath)
                ? reference.FilePath
                : project.OutputAssemblyPath;
            var candidateContext = new WorkspaceAssemblyLoadContext(
                Path.GetRandomFileName(),
                workspaceReferences,
                outputDirectory,
                new[] { project.AssemblyName, project.Name, reference.Name },
                mainAssemblyPath);

            if (candidateContext.LoadAssemblyReference(reference) is { } loadedAssembly)
            {
                assembly = loadedAssembly;
                context = candidateContext;
                loadedAssemblies = candidateContext.LoadRuntimeAssemblies(loadedAssembly.GetName().Name);
                return true;
            }

            candidateContext.Unload();
        }

        assembly = null!;
        context = null!;
        loadedAssemblies = Array.Empty<Assembly>();
        return false;
    }

    private static IEnumerable<WorkspaceAssemblyReference> GetWorkspaceOutputAssemblyCandidates(
        InMemoryProject project,
        IReadOnlyList<WorkspaceAssemblyReference> workspaceReferences)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project);
        if (WorkspaceAssemblyReference.FromPath(targetAssemblyPath, isRuntimeAssembly: true) is { } outputReference &&
            seen.Add(GetWorkspaceAssemblyReferenceKey(outputReference)))
        {
            yield return outputReference;
        }

        var assemblyNames = new[] { project.AssemblyName, project.Name }
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var reference in workspaceReferences)
        {
            if (!reference.IsRuntimeAssembly ||
                !assemblyNames.Contains(reference.Name, StringComparer.OrdinalIgnoreCase) ||
                !seen.Add(GetWorkspaceAssemblyReferenceKey(reference)))
            {
                continue;
            }

            yield return reference;
        }
    }

    private static string GetWorkspaceAssemblyReferenceKey(WorkspaceAssemblyReference reference)
    {
        return string.IsNullOrWhiteSpace(reference.FilePath)
            ? reference.Name
            : reference.FilePath;
    }

    private static bool ShouldBuildWorkspaceProjectBeforeUsingOutput(InMemoryProject project)
    {
        var targetAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project) ?? project.OutputAssemblyPath;
        if (string.IsNullOrWhiteSpace(targetAssemblyPath) || !File.Exists(targetAssemblyPath))
        {
            return true;
        }

        return IsWorkspaceOutputAssemblyStale(project, targetAssemblyPath);
    }

    private static bool ShouldBuildWorkspaceProjectBeforeRemotePreview(
        InMemoryProject project,
        InMemoryProjectFile? liveXamlFile)
    {
        var targetAssemblyPath = ResolveWorkspaceTargetAssemblyPath(project) ?? project.OutputAssemblyPath;
        if (string.IsNullOrWhiteSpace(targetAssemblyPath) || !File.Exists(targetAssemblyPath))
        {
            return true;
        }

        return IsWorkspaceOutputAssemblyStale(project, targetAssemblyPath, liveXamlFile);
    }

    private static bool IsWorkspaceOutputAssemblyStale(
        InMemoryProject project,
        string targetAssemblyPath,
        InMemoryProjectFile? liveXamlFile = null)
    {
        DateTime outputWriteTime;
        try
        {
            outputWriteTime = File.GetLastWriteTimeUtc(targetAssemblyPath);
        }
        catch
        {
            return true;
        }

        foreach (var file in project.Files.Where(IsWorkspaceBuildInputFile))
        {
            if (liveXamlFile is { } &&
                ReferenceEquals(file, liveXamlFile) &&
                file.IsXaml)
            {
                continue;
            }

            var sourcePath = ResolveWorkspaceProjectInputPath(project, file);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                continue;
            }

            try
            {
                if (File.GetLastWriteTimeUtc(sourcePath) > outputWriteTime)
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWorkspaceBuildInputFile(InMemoryProjectFile file)
    {
        return file.Kind is ProjectFileKind.CSharp or ProjectFileKind.Xaml or ProjectFileKind.Resource or ProjectFileKind.ProjectFile;
    }

    private static bool TryGetDirtyWorkspaceBuildInput(
        InMemoryProject project,
        InMemoryProjectFile? activeXamlFile,
        [NotNullWhen(true)] out InMemoryProjectFile? dirtyFile)
    {
        foreach (var file in project.Files.Where(IsWorkspaceBuildInputFile))
        {
            if (!file.IsDirty)
            {
                continue;
            }

            if (activeXamlFile is { } &&
                ReferenceEquals(file, activeXamlFile) &&
                file.IsXaml)
            {
                continue;
            }

            dirtyFile = file;
            return true;
        }

        dirtyFile = null;
        return false;
    }

    private static string GetUnsavedWorkspaceBuildInputMessage(InMemoryProjectFile file)
    {
        return $"Save {file.Path} before using isolated workspace preview. Unsaved code, project, or resource changes cannot be included in the built workspace assembly.";
    }

    private static string? ResolveWorkspaceProjectInputPath(InMemoryProject project, InMemoryProjectFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.SourcePath))
        {
            return ResolveWorkspaceFilePath(file.SourcePath);
        }

        if (Path.IsPathRooted(file.Path))
        {
            return ResolveWorkspaceFilePath(file.Path);
        }

        var projectFilePath = ResolveWorkspaceFilePath(project.ProjectFilePath);
        var projectDirectory = string.IsNullOrWhiteSpace(projectFilePath)
            ? null
            : Path.GetDirectoryName(projectFilePath);
        return string.IsNullOrWhiteSpace(projectDirectory)
            ? null
            : ResolveWorkspaceFilePath(Path.Combine(projectDirectory, file.Path));
    }

    private static string? ResolveWorkspaceTargetAssemblyPath(
        InMemoryProject project,
        bool preferCached = true,
        bool preferNewest = false)
    {
        if (preferCached &&
            !string.IsNullOrWhiteSpace(project.OutputAssemblyPath) &&
            File.Exists(project.OutputAssemblyPath))
        {
            return project.OutputAssemblyPath;
        }

        var projectFilePath = ResolveWorkspaceFilePath(project.ProjectFilePath);
        var projectDirectory = string.IsNullOrWhiteSpace(projectFilePath)
            ? null
            : Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        var targetName = (string.IsNullOrWhiteSpace(project.AssemblyName)
            ? project.Name
            : project.AssemblyName) + ".dll";
        var candidates = EnumerateWorkspaceTargetAssemblyCandidates(projectDirectory, project.TargetFramework, targetName)
            .Where(static candidate => File.Exists(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!preferNewest)
        {
            return candidates.FirstOrDefault();
        }

        return candidates
            .OrderByDescending(GetFileLastWriteTimeUtc)
            .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateWorkspaceTargetAssemblyCandidates(
        string projectDirectory,
        string? targetFramework,
        string targetName)
    {
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            if (!string.IsNullOrWhiteSpace(targetFramework))
            {
                yield return Path.Combine(projectDirectory, "bin", configuration, targetFramework, targetName);
            }

            var configurationRoot = Path.Combine(projectDirectory, "bin", configuration);
            if (!Directory.Exists(configurationRoot))
            {
                continue;
            }

            foreach (var framework in PreferredWorkspacePreviewFrameworks)
            {
                yield return Path.Combine(configurationRoot, framework, targetName);
            }
        }

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var configurationRoot = Path.Combine(projectDirectory, "bin", configuration);
            if (!Directory.Exists(configurationRoot))
            {
                continue;
            }

            string[] discoveredCandidates;
            try
            {
                discoveredCandidates = Directory
                    .EnumerateFiles(configurationRoot, targetName, SearchOption.AllDirectories)
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var candidate in discoveredCandidates)
            {
                yield return candidate;
            }
        }
    }

    private static DateTime GetFileLastWriteTimeUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static string? ResolveWorkspaceFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Replace('\\', '/').Trim();
        if (File.Exists(normalizedPath))
        {
            return normalizedPath;
        }

        var rootedPath = "/" + normalizedPath.TrimStart('/');
        return Path.DirectorySeparatorChar == '/' && File.Exists(rootedPath)
            ? rootedPath
            : normalizedPath;
    }

    private static string? ResolveWorkspaceDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var normalizedDirectory = directory.Replace('\\', '/').Trim();
        if (Directory.Exists(normalizedDirectory))
        {
            return normalizedDirectory;
        }

        var rootedDirectory = "/" + normalizedDirectory.TrimStart('/');
        return Path.DirectorySeparatorChar == '/' && Directory.Exists(rootedDirectory)
            ? rootedDirectory
            : null;
    }

    private static async Task<DotNetProcessResult> RunDotNetBuildAsync(string projectPath, bool noRestore)
    {
        using var process = new Process
        {
            StartInfo = CreateDotNetBuildStartInfo(projectPath, noRestore)
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start dotnet.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var lines = new[] { await outputTask, await errorTask }
            .Where(static output => !string.IsNullOrWhiteSpace(output))
            .SelectMany(static output => output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Distinct()
            .ToArray();
        return new DotNetProcessResult(process.ExitCode, lines);
    }

    private static ProcessStartInfo CreateDotNetBuildStartInfo(string projectPath, bool noRestore)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        if (noRestore)
        {
            startInfo.ArgumentList.Add("--no-restore");
        }

        startInfo.ArgumentList.Add("--nologo");
        return startInfo;
    }

    private static bool CanPreviewXamlFile(InMemoryProjectFile? file)
    {
        if (file is null || file.Path.Equals("App.axaml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return file.Kind == ProjectFileKind.Xaml ||
               file.Kind == ProjectFileKind.Resource && file.IncludeInRuntimePreview;
    }

    private static bool TryValidateXml(string? xaml, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(xaml))
        {
            errorMessage = "XAML is empty.";
            return false;
        }

        try
        {
            XDocument.Parse(xaml, LoadOptions.SetLineInfo);
            return true;
        }
        catch (XmlException exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

    private static string? FormatCompilerDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var lines = diagnostics
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(FormatCompilerDiagnostic)
            .Distinct()
            .ToArray();

        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string FormatCompilerDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.GetLineSpan();
        var line = location.IsValid ? location.StartLinePosition.Line + 1 : (int?)null;
        var column = location.IsValid ? location.StartLinePosition.Character + 1 : (int?)null;
        var position = line is null
            ? string.Empty
            : $" Line {line}, position {column}";

        return $"C# {diagnostic.Severity}{position}: {diagnostic.Id}: {diagnostic.GetMessage()}";
    }

    private static string? FormatXamlDiagnostics(IEnumerable<RuntimeXamlDiagnostic> diagnostics)
    {
        var lines = diagnostics
            .Select(FormatXamlDiagnostic)
            .Distinct()
            .ToArray();

        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string FormatXamlDiagnostic(RuntimeXamlDiagnostic diagnostic)
    {
        var document = string.IsNullOrWhiteSpace(diagnostic.Document)
            ? PlaygroundXamlDocument
            : diagnostic.Document;
        var position = diagnostic.LineNumber is { } lineNumber
            ? $" Line {lineNumber}, position {diagnostic.LinePosition ?? 1}"
            : string.Empty;

        return $"XAML {diagnostic.Severity} {document}{position}: {diagnostic.Id}: {diagnostic.Title}";
    }

    private static string FormatRemotePreviewError(WorkspaceRemotePreviewError error)
    {
        var document = string.IsNullOrWhiteSpace(error.FilePath)
            ? "remote preview"
            : error.FilePath;
        var position = error.Line is { } line
            ? $" Line {line}, position {error.Column ?? 1}"
            : string.Empty;

        return $"XAML Error {document}{position}: {error.Message}";
    }

    private static string? CombineDiagnostics(params string?[] messages)
    {
        var lines = messages
            .Where(static message => !string.IsNullOrWhiteSpace(message))
            .Select(static message => message!.Trim())
            .Distinct()
            .ToArray();

        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static string FormatException(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            var messages = aggregateException.Flatten()
                .InnerExceptions
                .Select(static innerException => UnwrapException(innerException).Message)
                .Where(static message => !string.IsNullOrWhiteSpace(message))
                .Distinct()
                .ToArray();

            if (messages.Length > 0)
            {
                return string.Join(Environment.NewLine, messages);
            }
        }

        exception = UnwrapException(exception);
        return exception.Message;
    }

    private static Exception UnwrapException(Exception exception)
    {
        while (true)
        {
            if (exception is TargetInvocationException { InnerException: { } targetInvocationInnerException })
            {
                exception = targetInvocationInnerException;
                continue;
            }

            if (exception is TypeInitializationException { InnerException: { } typeInitializationInnerException })
            {
                exception = typeInitializationInnerException;
                continue;
            }

            return exception;
        }
    }

    private void ShowControl(
        InMemoryProjectFile xamlFile,
        Control control,
        string previewSourceXaml,
        string? diagnosticsMessage,
        WorkspacePreviewAssemblyScope? assemblyScope = null)
    {
        var previousScope = _previous;
        var scope = new Border
        {
            Name = "GeneratedSampleScope",
            Child = control
        };

        StopRemotePreview(resetPreviewSession: IsRemotePreviewActive);
        Control = scope;
        DiagnosticsRoot = scope;
        SetDiagnosticsPreviewXamlFile(xamlFile, previewSourceXaml, xamlFile.Kind != ProjectFileKind.Resource);
        LastErrorMessage = diagnosticsMessage;
        _previous = assemblyScope;
        previousScope?.Unload();
    }

    private void StopRemotePreview(bool resetPreviewSession = true)
    {
        _remotePreviewService.Stop();
        if (resetPreviewSession)
        {
            _previewSessionManager.Reset();
            PreviewReloadStrategy = PreviewReloadStrategy.NoOp;
            PreviewStatus = "Idle.";
        }

        IsRemotePreviewActive = false;
        RemotePreviewBitmap = null;
    }

    private void UnloadPreviousInProcessPreview()
    {
        var previousScope = _previous;
        _previous = null;
        previousScope?.Unload();
    }

    private void OnRemotePreviewFrameReceived(FrameMessage frame)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (TryCreateRemotePreviewBitmap(frame) is { } bitmap)
            {
                RemotePreviewBitmap = bitmap;
            }
        });
    }

    public void UpdateRemotePreviewViewport(double width, double height, double dpiX, double dpiY)
    {
        if (!IsRemotePreviewActive)
        {
            return;
        }

        _remotePreviewService.UpdateViewport(width, height, dpiX, dpiY);
    }

    public void SendRemotePreviewPointerMoved(double x, double y, InputModifiers[] modifiers)
    {
        if (IsRemotePreviewActive)
        {
            _ = _remotePreviewService.SendPointerMovedAsync(x, y, modifiers);
        }
    }

    public void SendRemotePreviewPointerPressed(double x, double y, MouseButton button, InputModifiers[] modifiers)
    {
        if (IsRemotePreviewActive)
        {
            _ = _remotePreviewService.SendPointerPressedAsync(x, y, button, modifiers);
        }
    }

    public void SendRemotePreviewPointerReleased(double x, double y, MouseButton button, InputModifiers[] modifiers)
    {
        if (IsRemotePreviewActive)
        {
            _ = _remotePreviewService.SendPointerReleasedAsync(x, y, button, modifiers);
        }
    }

    public void SendRemotePreviewScroll(double x, double y, double deltaX, double deltaY, InputModifiers[] modifiers)
    {
        if (IsRemotePreviewActive)
        {
            _ = _remotePreviewService.SendScrollAsync(x, y, deltaX, deltaY, modifiers);
        }
    }

    public void SendRemotePreviewKeyEvent(bool isDown, Avalonia.Input.Key key, Avalonia.Input.PhysicalKey physicalKey, string? keySymbol, InputModifiers[] modifiers)
    {
        if (IsRemotePreviewActive)
        {
            _ = _remotePreviewService.SendKeyEventAsync(isDown, key, physicalKey, keySymbol, modifiers);
        }
    }

    public void SendRemotePreviewTextInput(string text)
    {
        if (IsRemotePreviewActive)
        {
            _ = _remotePreviewService.SendTextInputAsync(text);
        }
    }

    private static Bitmap? TryCreateRemotePreviewBitmap(FrameMessage frame)
    {
        if (frame.Width <= 0 ||
            frame.Height <= 0 ||
            frame.Data.Length == 0 ||
            frame.Format != RemotePixelFormat.Bgra8888)
        {
            return null;
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(frame.Width, frame.Height),
            new Vector(frame.DpiX <= 0 ? 96 : frame.DpiX, frame.DpiY <= 0 ? 96 : frame.DpiY),
            Avalonia.Platform.PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using var locked = bitmap.Lock();
        var copyWidth = Math.Min(frame.Stride, locked.RowBytes);
        for (var row = 0; row < frame.Height; row++)
        {
            var sourceOffset = row * frame.Stride;
            if (sourceOffset + copyWidth > frame.Data.Length)
            {
                break;
            }

            Marshal.Copy(frame.Data, sourceOffset, locked.Address + row * locked.RowBytes, copyWidth);
        }

        return bitmap;
    }

    private async Task OpenXamlFile()
    {
        if (ActiveXamlFile is null)
        {
            return;
        }

        if (StorageProvider is null)
        {
            return;
        }

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open xaml",
            FileTypeFilter = GetXamlFileTypes(),
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();
        if (file is not null)
        {
            try
            {
                _openXamlFile = file;
                await using var stream = await _openXamlFile.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var fileContent = await reader.ReadToEndAsync();
                ActiveXamlFile.Text = fileContent;
                RunActiveDocument();
                reader.Dispose();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }

    private async Task SaveXamlFile()
    {
        if (ActiveXamlFile is null)
        {
            return;
        }

        if (_openXamlFile is null)
        {
            if (StorageProvider is null)
            {
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save xaml",
                FileTypeChoices = GetXamlFileTypes(),
                SuggestedFileName = Path.GetFileNameWithoutExtension("playground"),
                DefaultExtension = "axaml",
                ShowOverwritePrompt = true
            });

            if (file is not null)
            {
                try
                {
                    _openXamlFile = file;
                    await WriteStorageFileTextAsync(_openXamlFile, ActiveXamlFile.Text);
                    ActiveXamlFile.MarkClean();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
            }
        }
        else
        {
            await WriteStorageFileTextAsync(_openXamlFile, ActiveXamlFile.Text);
            ActiveXamlFile.MarkClean();
        }
    }

    private async Task OpenCodeFile()
    {
        if (ActiveCodeFile is null)
        {
            return;
        }

        if (StorageProvider is null)
        {
            return;
        }

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open code",
            FileTypeFilter = GetCodeFileTypes(),
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();
        if (file is not null)
        {
            try
            {
                _openCodeFile = file;
                await using var stream = await _openCodeFile.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var fileContent = await reader.ReadToEndAsync();
                ActiveCodeFile.Text = fileContent;
                RunActiveDocument();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }

    private async Task SaveCodeFile()
    {
        if (ActiveCodeFile is null)
        {
            return;
        }

        if (_openCodeFile is null)
        {
            if (StorageProvider is null)
            {
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save code",
                FileTypeChoices = GetCodeFileTypes(),
                SuggestedFileName = Path.GetFileNameWithoutExtension("playground"),
                DefaultExtension = "cs",
                ShowOverwritePrompt = true
            });

            if (file is not null)
            {
                try
                {
                    _openCodeFile = file;
                    await WriteStorageFileTextAsync(_openCodeFile, ActiveCodeFile.Text);
                    ActiveCodeFile.MarkClean();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
            }
        }
        else
        {
            await WriteStorageFileTextAsync(_openCodeFile, ActiveCodeFile.Text);
            ActiveCodeFile.MarkClean();
        }
    }

    private static async Task WriteStorageFileTextAsync(IStorageFile file, string text)
    {
        await using var stream = await file.OpenWriteAsync();
        try
        {
            stream.SetLength(0);
        }
        catch (NotSupportedException)
        {
        }

        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
    }

    private sealed record WorkspaceOutputFallbackResult(
        bool Success,
        WorkspacePreviewAssemblyScope? AssemblyScope,
        string? DiagnosticsMessage)
    {
        public Assembly? Assembly => AssemblyScope?.Assembly;
    }

    private sealed record WorkspacePreviewAssemblyScope(
        Assembly? Assembly,
        AssemblyLoadContext? Context,
        IReadOnlyList<Assembly> LoadedAssemblies)
    {
        public void Unload()
        {
            Context?.Unload();
        }
    }

    private sealed record DotNetProcessResult(int ExitCode, IReadOnlyList<string> OutputLines);
}
