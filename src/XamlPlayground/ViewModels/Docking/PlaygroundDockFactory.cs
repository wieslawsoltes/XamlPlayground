using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Diagnostics;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Settings;
using XamlPlayground;
using XamlPlayground.Extensions;

namespace XamlPlayground.ViewModels.Docking;

public sealed class PlaygroundDockFactory : Factory
{
    public const string DefaultPerspectiveId = "Professional";

    public static IReadOnlyList<DockPerspectiveDescriptor> PerspectiveDescriptors { get; } =
        BuiltInExtensionProvider.Manifest.Contributions.Perspectives
            .Select(static perspective => new DockPerspectiveDescriptor(perspective.Id, perspective.Title))
            .ToArray();

    public static IReadOnlyList<DockToolDescriptor> ToolDescriptors { get; } =
        BuiltInExtensionProvider.Manifest.Contributions.Views
            .Where(static view => view.IsTool)
            .Select(static view => new DockToolDescriptor(view.Id, view.Title, ParseDockToolRegion(view.Location)))
            .ToArray();

    private readonly MainViewModel _shell;
    private readonly Dictionary<string, IDockable> _toolCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IToolDock> _restoreDockByToolId = new(StringComparer.Ordinal);
    private IRootDock? _rootDock;
    private IDocumentDock? _editorDock;
    private IToolDock? _leftDock;
    private IToolDock? _previewDock;
    private IToolDock? _rightDock;
    private IToolDock? _bottomDock;
    private SolutionExplorerDockViewModel? _solutionExplorer;
    private MsBuildWorkspaceDockViewModel? _msBuildWorkspace;
    private VisualStructureDockViewModel? _visualStructure;
    private VisualPropertiesDockViewModel? _visualProperties;
    private VisualToolboxDockViewModel? _visualToolbox;
    private VisualAnimationsDockViewModel? _visualAnimations;
    private StylesInspectorDockViewModel? _stylesInspector;
    private BindingsInspectorDockViewModel? _bindingsInspector;
    private ResourcesInspectorDockViewModel? _resourcesInspector;
    private ControlThemesDockViewModel? _controlThemes;
    private PreviewDockViewModel? _preview;
    private AnimationTimelineSheetDockViewModel? _animationTimelineSheet;
    private StyleEditorDockViewModel? _styleEditor;
    private BindingEditorDockViewModel? _bindingEditor;
    private ResourceEditorDockViewModel? _resourceEditor;
    private DiagnosticTreeDockViewModel? _combinedTree;
    private DiagnosticTreeDockViewModel? _logicalTree;
    private DiagnosticTreeDockViewModel? _visualTree;
    private DiagnosticToolDockViewModel? _events;
    private DiagnosticToolDockViewModel? _resources;
    private DiagnosticToolDockViewModel? _assets;
    private ErrorsDockViewModel? _errors;

    public PlaygroundDockFactory(MainViewModel shell)
    {
        _shell = shell;
        HideToolsOnClose = true;
    }

    public string CurrentPerspectiveId { get; private set; } = DefaultPerspectiveId;

    private static DockToolRegion ParseDockToolRegion(string location)
    {
        return location.ToLowerInvariant() switch
        {
            "left" => DockToolRegion.Left,
            "preview" => DockToolRegion.Preview,
            "right" => DockToolRegion.Right,
            "bottom" => DockToolRegion.Bottom,
            _ => DockToolRegion.Right
        };
    }

    public override IRootDock CreateLayout()
    {
        EnsureDockables();
        CurrentPerspectiveId = DefaultPerspectiveId;
        return CreatePerspectiveLayoutCore(DefaultPerspectiveId);
    }

    public IRootDock CreatePerspectiveLayout(string? perspectiveId)
    {
        EnsureDockables();
        var id = NormalizePerspectiveId(perspectiveId);
        CurrentPerspectiveId = id;
        var layout = CreatePerspectiveLayoutCore(id);
        InitLayout(layout);
        return layout;
    }

    public bool ToggleTool(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return IsToolVisible(id) ? HideTool(id) : ShowTool(id);
    }

    public bool ShowTool(string? id)
    {
        return ShowTool(id, activate: true);
    }

    private bool ShowTool(string? id, bool activate)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            _rootDock is null ||
            !_toolCache.TryGetValue(id, out var dockable))
        {
            return false;
        }

        if (IsToolVisible(id))
        {
            if (activate)
            {
                ActivateTool(dockable);
            }

            return true;
        }

        if (FindHiddenRoot(dockable) is { HiddenDockables: { } hiddenDockables })
        {
            if (dockable.OriginalOwner is IDock originalOwner && IsDockInCurrentLayout(originalOwner))
            {
                RestoreDockable(dockable);
            }
            else
            {
                if (ResolveRestoreDock(id) is not { } dock)
                {
                    return false;
                }

                hiddenDockables.Remove(dockable);
                AddToolToDock(dock, dockable);
                OnDockableRestored(dockable);
            }
        }
        else if (!AddToolToCurrentLayout(id, dockable))
        {
            return false;
        }

        if (activate)
        {
            ActivateTool(dockable);
        }

        return true;
    }

    public bool RestoreAllTools()
    {
        var restored = false;
        foreach (var descriptor in ToolDescriptors)
        {
            if (!IsToolVisible(descriptor.Id))
            {
                restored |= ShowTool(descriptor.Id, activate: false);
            }
        }

        return restored;
    }

    public bool IsToolVisible(string id)
    {
        return _rootDock is not null &&
               _toolCache.TryGetValue(id, out var dockable) &&
               EnumerateVisibleDockables(_rootDock).Any(candidate => ReferenceEquals(candidate, dockable));
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);
        if (window is not null)
        {
            window.Title = "Xaml Playground";
        }

        return window;
    }

    public override void InitLayout(IDockable layout)
    {
        EnsureDockables();

        if (layout is IRootDock rootDock)
        {
            _rootDock = rootDock;
        }

        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["SolutionExplorer"] = () => _solutionExplorer,
            ["MsBuildWorkspace"] = () => _msBuildWorkspace,
            ["VisualStructure"] = () => _visualStructure,
            ["VisualProperties"] = () => _visualProperties,
            ["VisualToolbox"] = () => _visualToolbox,
            ["VisualAnimations"] = () => _visualAnimations,
            ["StylesInspector"] = () => _stylesInspector,
            ["BindingsInspector"] = () => _bindingsInspector,
            ["ResourcesInspector"] = () => _resourcesInspector,
            ["ControlThemes"] = () => _controlThemes,
            ["Preview"] = () => _preview,
            ["AnimationTimelineSheet"] = () => _animationTimelineSheet,
            ["StyleEditor"] = () => _styleEditor,
            ["BindingEditor"] = () => _bindingEditor,
            ["ResourceEditor"] = () => _resourceEditor,
            ["DiagnosticsCombinedTree"] = () => _combinedTree,
            ["DiagnosticsLogicalTree"] = () => _logicalTree,
            ["DiagnosticsVisualTree"] = () => _visualTree,
            ["DiagnosticsEvents"] = () => _events,
            ["DiagnosticsResources"] = () => _resources,
            ["DiagnosticsAssets"] = () => _assets,
            ["Errors"] = () => _errors
        };

        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock
        };
        foreach (var descriptor in ToolDescriptors)
        {
            DockableLocator[descriptor.Id] = () =>
                _toolCache.TryGetValue(descriptor.Id, out var dockable) ? dockable : null;
        }

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = CreateHostWindow
        };

        base.InitLayout(layout);
    }

    public void ResetDocuments(IEnumerable<XamlPlayground.Workspace.InMemoryProjectFile> files)
    {
        var editorDock = EnsureEditorDock();

        var documents = files
            .Where(static file => file.CanEdit)
            .Take(2)
            .Select(CreateWorkspaceDocument)
            .Cast<IDockable>()
            .ToArray();

        editorDock.VisibleDockables = CreateList<IDockable>(documents);
        editorDock.ActiveDockable = documents.FirstOrDefault();

        if (editorDock.ActiveDockable is WorkspaceFileDocumentDockViewModel document)
        {
            _shell.ActivateWorkspaceFileFromDocument(document.File);
        }
    }

    public void OpenDocument(XamlPlayground.Workspace.InMemoryProjectFile file)
    {
        var editorDock = EnsureEditorDock();
        var visibleDockables = editorDock.VisibleDockables ?? CreateList<IDockable>();
        editorDock.VisibleDockables = visibleDockables;

        var existing = visibleDockables
            .OfType<WorkspaceFileDocumentDockViewModel>()
            .FirstOrDefault(document => ReferenceEquals(document.File, file));

        if (existing is null)
        {
            existing = CreateWorkspaceDocument(file);
            visibleDockables.Add(existing);
        }

        editorDock.ActiveDockable = existing;
        _shell.ActivateWorkspaceFileFromDocument(file);
    }

    public void ActivateErrors()
    {
        if (_errors is null)
        {
            return;
        }

        ShowTool("Errors");
        _errors.NotifyLastErrorMessageChanged();
        Dispatcher.UIThread.Post(_errors.NotifyLastErrorMessageChanged, DispatcherPriority.Loaded);
    }

    private static string NormalizePerspectiveId(string? perspectiveId)
    {
        if (string.IsNullOrWhiteSpace(perspectiveId))
        {
            return DefaultPerspectiveId;
        }

        return PerspectiveDescriptors.Any(descriptor => descriptor.Id.Equals(perspectiveId, StringComparison.Ordinal))
            ? perspectiveId
            : DefaultPerspectiveId;
    }

    private void EnsureDockables()
    {
        if (_solutionExplorer is not null)
        {
            return;
        }

        EnsureEditorDock();
        _solutionExplorer = RegisterTool(new SolutionExplorerDockViewModel(_shell));
        _msBuildWorkspace = RegisterTool(new MsBuildWorkspaceDockViewModel(_shell));
        _visualStructure = RegisterTool(new VisualStructureDockViewModel(_shell));
        _visualProperties = RegisterTool(new VisualPropertiesDockViewModel(_shell));
        _visualToolbox = RegisterTool(new VisualToolboxDockViewModel(_shell));
        _visualAnimations = RegisterTool(new VisualAnimationsDockViewModel(_shell));
        _stylesInspector = RegisterTool(new StylesInspectorDockViewModel(_shell));
        _bindingsInspector = RegisterTool(new BindingsInspectorDockViewModel(_shell));
        _resourcesInspector = RegisterTool(new ResourcesInspectorDockViewModel(_shell));
        _controlThemes = RegisterTool(new ControlThemesDockViewModel(_shell));
        _preview = RegisterTool(new PreviewDockViewModel(_shell));
        _animationTimelineSheet = RegisterTool(new AnimationTimelineSheetDockViewModel(_shell));
        _styleEditor = RegisterTool(new StyleEditorDockViewModel(_shell));
        _bindingEditor = RegisterTool(new BindingEditorDockViewModel(_shell));
        _resourceEditor = RegisterTool(new ResourceEditorDockViewModel(_shell));
        _combinedTree = RegisterTool(CreateDiagnosticsTreeTool("DiagnosticsCombinedTree", "Combined Tree", DevToolsViewKind.CombinedTree));
        _logicalTree = RegisterTool(CreateDiagnosticsTreeTool("DiagnosticsLogicalTree", "Logical Tree", DevToolsViewKind.LogicalTree));
        _visualTree = RegisterTool(CreateDiagnosticsTreeTool("DiagnosticsVisualTree", "Visual Tree", DevToolsViewKind.VisualTree));
        _events = RegisterTool(CreateDiagnosticsTool("DiagnosticsEvents", "Events", DevToolsViewKind.Events));
        _resources = RegisterTool(CreateDiagnosticsTool("DiagnosticsResources", "Resources", DevToolsViewKind.Resources));
        _assets = RegisterTool(CreateDiagnosticsTool("DiagnosticsAssets", "Assets", DevToolsViewKind.Assets));
        _errors = RegisterTool(new ErrorsDockViewModel(_shell));
    }

    private TTool RegisterTool<TTool>(TTool tool)
        where TTool : IDockable
    {
        if (tool.Id is null)
        {
            throw new InvalidOperationException("Dock tools must have stable identifiers.");
        }

        tool.CanClose = true;
        _toolCache[tool.Id] = tool;
        return tool;
    }

    private IDocumentDock EnsureEditorDock()
    {
        if (_editorDock is not null)
        {
            return _editorDock;
        }

        var editorDock = CreateDocumentDock();
        editorDock.Id = "Editors";
        editorDock.Title = "Editors";
        editorDock.IsCollapsable = false;
        editorDock.CanCloseLastDockable = false;
        editorDock.EnableWindowDrag = true;
        editorDock.VisibleDockables = CreateList<IDockable>();
        if (editorDock is INotifyPropertyChanged notifyEditorDock)
        {
            notifyEditorDock.PropertyChanged += EditorDockOnPropertyChanged;
        }

        _editorDock = editorDock;
        return editorDock;
    }

    private IRootDock CreatePerspectiveLayoutCore(string perspectiveId)
    {
        return perspectiveId switch
        {
            "Wysiwyg" => CreateWorkspacePerspective(
                "WYSIWYG Editor",
                ["VisualToolbox", "SolutionExplorer", "VisualStructure"],
                "VisualToolbox",
                ["Preview"],
                "Preview",
                ["VisualProperties", "StylesInspector", "ResourcesInspector", "ControlThemes", "VisualAnimations"],
                "VisualProperties",
                ["AnimationTimelineSheet", "StyleEditor", "Errors"],
                Utilities.IsBrowser() ? "Errors" : "AnimationTimelineSheet",
                leftProportion: 0.22,
                editorProportion: 0.72,
                previewProportion: 0.72,
                rightProportion: 0.28,
                bottomProportion: 0.26),
            "Structure" => CreateWorkspacePerspective(
                "Structure Focus",
                ["VisualStructure", "SolutionExplorer"],
                "VisualStructure",
                ["Preview"],
                "Preview",
                ["VisualProperties", "BindingsInspector", "ResourcesInspector", "StylesInspector"],
                "VisualProperties",
                ["DiagnosticsCombinedTree", "DiagnosticsLogicalTree", "DiagnosticsVisualTree", "Errors"],
                "DiagnosticsCombinedTree",
                leftProportion: 0.28,
                editorProportion: 0.78,
                previewProportion: 0.46,
                rightProportion: 0.24,
                bottomProportion: 0.26),
            "Diagnostics" => CreateWorkspacePerspective(
                "Dev Tools Diagnostics",
                ["DiagnosticsCombinedTree", "DiagnosticsLogicalTree", "DiagnosticsVisualTree"],
                "DiagnosticsCombinedTree",
                ["Preview"],
                "Preview",
                ["DiagnosticsEvents", "DiagnosticsResources", "DiagnosticsAssets"],
                "DiagnosticsEvents",
                ["Errors", "MsBuildWorkspace"],
                "Errors",
                leftProportion: 0.30,
                editorProportion: 0.70,
                previewProportion: 0.44,
                rightProportion: 0.28,
                bottomProportion: 0.24),
            "Animation" => CreateWorkspacePerspective(
                "Animation Editing",
                ["VisualToolbox", "VisualStructure", "SolutionExplorer"],
                "VisualToolbox",
                ["Preview"],
                "Preview",
                ["VisualAnimations", "VisualProperties", "StylesInspector"],
                "VisualAnimations",
                ["AnimationTimelineSheet", "StyleEditor", "Errors"],
                "AnimationTimelineSheet",
                leftProportion: 0.22,
                editorProportion: 0.62,
                previewProportion: 0.60,
                rightProportion: 0.26,
                bottomProportion: 0.34),
            "Theme" => CreateWorkspacePerspective(
                "Theme Editing",
                ["ControlThemes", "SolutionExplorer"],
                "ControlThemes",
                ["Preview"],
                "Preview",
                ["StylesInspector", "ResourcesInspector", "VisualProperties", "VisualStructure"],
                "StylesInspector",
                ["StyleEditor", "ResourceEditor", "Errors"],
                "StyleEditor",
                leftProportion: 0.30,
                editorProportion: 0.66,
                previewProportion: 0.46,
                rightProportion: 0.30,
                bottomProportion: 0.32),
            "Bindings" => CreateWorkspacePerspective(
                "Bindings and Resources",
                ["VisualStructure", "SolutionExplorer"],
                "VisualStructure",
                ["Preview"],
                "Preview",
                ["BindingsInspector", "ResourcesInspector", "VisualProperties", "StylesInspector"],
                "BindingsInspector",
                ["BindingEditor", "ResourceEditor", "DiagnosticsEvents", "Errors"],
                "BindingEditor",
                leftProportion: 0.24,
                editorProportion: 0.72,
                previewProportion: 0.44,
                rightProportion: 0.30,
                bottomProportion: 0.30),
            "Code" => CreateWorkspacePerspective(
                "Code and Errors",
                ["SolutionExplorer", "MsBuildWorkspace"],
                "SolutionExplorer",
                ["Preview"],
                "Preview",
                ["VisualStructure", "BindingsInspector", "ResourcesInspector"],
                "VisualStructure",
                ["Errors", "DiagnosticsEvents", "DiagnosticsResources"],
                "Errors",
                leftProportion: 0.24,
                editorProportion: 1.15,
                previewProportion: 0.26,
                rightProportion: 0.26,
                bottomProportion: 0.30),
            "Preview" => CreateWorkspacePerspective(
                "Preview Review",
                ["SolutionExplorer"],
                "SolutionExplorer",
                ["Preview"],
                "Preview",
                ["VisualProperties", "VisualStructure", "StylesInspector", "ResourcesInspector"],
                "VisualProperties",
                ["Errors"],
                "Errors",
                leftProportion: 0.16,
                editorProportion: 0.40,
                previewProportion: 1.10,
                rightProportion: 0.26,
                bottomProportion: 0.18),
            "Professional" => CreateProfessionalPerspective(
                "Professional Designer",
                ["SolutionExplorer", "VisualStructure", "VisualToolbox"],
                "SolutionExplorer",
                ["VisualProperties", "StylesInspector", "BindingsInspector", "ResourcesInspector", "ControlThemes", "VisualAnimations"],
                "VisualProperties",
                ["Errors", "AnimationTimelineSheet", "StyleEditor", "BindingEditor", "ResourceEditor", "DiagnosticsCombinedTree", "DiagnosticsLogicalTree", "DiagnosticsVisualTree", "DiagnosticsEvents", "DiagnosticsResources", "DiagnosticsAssets"]),
            _ => CreateWorkspacePerspective(
                "Default Workspace",
                [],
                null,
                ["Preview"],
                "Preview",
                [
                    "SolutionExplorer",
                    "MsBuildWorkspace",
                    "VisualStructure",
                    "VisualProperties",
                    "VisualToolbox",
                    "VisualAnimations",
                    "StylesInspector",
                    "BindingsInspector",
                    "ResourcesInspector",
                    "ControlThemes"
                ],
                "SolutionExplorer",
                [
                    "AnimationTimelineSheet",
                    "StyleEditor",
                    "BindingEditor",
                    "ResourceEditor",
                    "DiagnosticsCombinedTree",
                    "DiagnosticsLogicalTree",
                    "DiagnosticsVisualTree",
                    "DiagnosticsEvents",
                    "DiagnosticsResources",
                    "DiagnosticsAssets",
                    "Errors"
                ],
                Utilities.IsBrowser() ? "Errors" : "AnimationTimelineSheet")
        };
    }

    private IRootDock CreateWorkspacePerspective(
        string title,
        IReadOnlyList<string> leftToolIds,
        string? leftActiveToolId,
        IReadOnlyList<string> previewToolIds,
        string? previewActiveToolId,
        IReadOnlyList<string> rightToolIds,
        string? rightActiveToolId,
        IReadOnlyList<string> bottomToolIds,
        string? bottomActiveToolId,
        double leftProportion = 0.22,
        double? editorProportion = null,
        double previewProportion = 0.36,
        double rightProportion = 0.24,
        double bottomProportion = 0.28)
    {
        var editorDock = EnsureEditorDock();
        var visibleToolIds = new HashSet<string>(
            leftToolIds
                .Concat(previewToolIds)
                .Concat(rightToolIds)
                .Concat(bottomToolIds),
            StringComparer.Ordinal);

        _leftDock = CreateToolDockFromIds("LeftTools", "Workspace", Alignment.Left, leftProportion, leftToolIds, leftActiveToolId);
        _previewDock = CreateToolDockFromIds("PreviewDock", "Preview", Alignment.Right, previewProportion, previewToolIds, previewActiveToolId);
        _rightDock = CreateToolDockFromIds("RightTools", "Inspectors", Alignment.Right, rightProportion, rightToolIds, rightActiveToolId);
        _bottomDock = CreateToolDockFromIds("Bottom", "Bottom", Alignment.Bottom, bottomProportion, bottomToolIds, bottomActiveToolId);
        UpdateRestoreDockMap();

        var centerDock = CreateProportionalDock();
        centerDock.Id = "Center";
        centerDock.Title = "Center";
        centerDock.Orientation = Orientation.Vertical;
        if (editorProportion is { } editorProportionValue)
        {
            centerDock.Proportion = editorProportionValue;
        }

        centerDock.IsCollapsable = false;
        var centerDockables = CreateList<IDockable>(editorDock);
        AddProportionalChild(centerDockables, _bottomDock);
        centerDock.VisibleDockables = centerDockables;
        centerDock.ActiveDockable = editorDock;

        var mainDockables = CreateList<IDockable>();
        AddProportionalChild(mainDockables, _leftDock);
        AddProportionalChild(mainDockables, centerDock);
        AddProportionalChild(mainDockables, _previewDock);
        AddProportionalChild(mainDockables, _rightDock);

        var mainDock = CreateProportionalDock();
        mainDock.Id = "Workspace";
        mainDock.Title = title;
        mainDock.Orientation = Orientation.Horizontal;
        mainDock.IsCollapsable = false;
        mainDock.VisibleDockables = mainDockables;
        mainDock.ActiveDockable = centerDock;

        var hiddenDockables = ToolDescriptors
            .Where(descriptor => !visibleToolIds.Contains(descriptor.Id))
            .Select(descriptor => PrepareHiddenTool(descriptor.Id))
            .ToArray();

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "Xaml Playground";
        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = mainDock;
        rootDock.DefaultDockable = mainDock;
        rootDock.VisibleDockables = CreateList<IDockable>(mainDock);
        rootDock.HiddenDockables = CreateList<IDockable>(hiddenDockables);
        rootDock.LeftPinnedDockables = CreateList<IDockable>();
        rootDock.RightPinnedDockables = CreateList<IDockable>();
        rootDock.TopPinnedDockables = CreateList<IDockable>();
        rootDock.BottomPinnedDockables = CreateList<IDockable>();
        rootDock.FloatingWindowHostMode = DockFloatingWindowHostMode.Default;

        _rootDock = rootDock;
        return rootDock;
    }

    private IRootDock CreateProfessionalPerspective(
        string title,
        IReadOnlyList<string> leftToolIds,
        string? leftActiveToolId,
        IReadOnlyList<string> rightToolIds,
        string? rightActiveToolId,
        IReadOnlyList<string> bottomPinnedToolIds,
        double leftProportion = 0.20,
        double previewProportion = 0.55,
        double rightProportion = 0.25)
    {
        var editorDock = EnsureEditorDock();
        var visibleToolIds = new HashSet<string>(
            leftToolIds.Concat(rightToolIds).Concat(["Preview"]),
            StringComparer.Ordinal);

        _leftDock = CreateToolDockFromIds("LeftTools", "Workspace", Alignment.Left, leftProportion, leftToolIds, leftActiveToolId);
        _rightDock = CreateToolDockFromIds("RightTools", "Inspectors", Alignment.Right, rightProportion, rightToolIds, rightActiveToolId);
        _previewDock = CreateToolDockFromIds("PreviewDock", "Preview", Alignment.Top, previewProportion, ["Preview"], "Preview");
        
        // We create a hidden bottom dock so that if a pinned tool is unpinned/restored, it goes here instead of to the right/left dock.
        _bottomDock = CreateToolDockFromIds("Bottom", "Bottom", Alignment.Bottom, 0.25, [], null);
        UpdateRestoreDockMap();

        var centerDock = CreateProportionalDock();
        centerDock.Id = "Center";
        centerDock.Title = "Center";
        centerDock.Orientation = Orientation.Vertical;
        centerDock.IsCollapsable = false;
        
        var centerDockables = CreateList<IDockable>();
        AddProportionalChild(centerDockables, _previewDock);
        AddProportionalChild(centerDockables, editorDock);
        centerDock.VisibleDockables = centerDockables;
        centerDock.ActiveDockable = _previewDock;

        var mainDockables = CreateList<IDockable>();
        AddProportionalChild(mainDockables, _leftDock);
        AddProportionalChild(mainDockables, centerDock);
        AddProportionalChild(mainDockables, _rightDock);

        var mainDock = CreateProportionalDock();
        mainDock.Id = "Workspace";
        mainDock.Title = title;
        mainDock.Orientation = Orientation.Horizontal;
        mainDock.IsCollapsable = false;
        mainDock.VisibleDockables = mainDockables;
        mainDock.ActiveDockable = centerDock;

        var bottomPinned = bottomPinnedToolIds
            .Select(id => PrepareHiddenTool(id))
            .ToArray();

        var hiddenDockables = ToolDescriptors
            .Where(descriptor => !visibleToolIds.Contains(descriptor.Id) && !bottomPinnedToolIds.Contains(descriptor.Id))
            .Select(descriptor => PrepareHiddenTool(descriptor.Id))
            .ToArray();

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "Xaml Playground";
        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = mainDock;
        rootDock.DefaultDockable = mainDock;
        rootDock.VisibleDockables = CreateList<IDockable>(mainDock);
        rootDock.HiddenDockables = CreateList<IDockable>(hiddenDockables);
        rootDock.LeftPinnedDockables = CreateList<IDockable>();
        rootDock.RightPinnedDockables = CreateList<IDockable>();
        rootDock.TopPinnedDockables = CreateList<IDockable>();
        rootDock.BottomPinnedDockables = CreateList<IDockable>(bottomPinned);
        rootDock.FloatingWindowHostMode = DockFloatingWindowHostMode.Default;

        _rootDock = rootDock;
        return rootDock;
    }

    private IToolDock? CreateToolDockFromIds(
        string id,
        string title,
        Alignment alignment,
        double proportion,
        IReadOnlyList<string> toolIds,
        string? activeToolId)
    {
        var dockables = toolIds
            .Select(GetTool)
            .Distinct()
            .ToArray();
        if (dockables.Length == 0)
        {
            return null;
        }

        foreach (var dockable in dockables)
        {
            dockable.OriginalOwner = null;
        }

        var toolDock = CreateToolDock();
        toolDock.Id = id;
        toolDock.Title = title;
        toolDock.Alignment = alignment;
        toolDock.Proportion = proportion;
        toolDock.CanCloseLastDockable = true;
        toolDock.VisibleDockables = CreateList<IDockable>(dockables);
        toolDock.ActiveDockable = activeToolId is not null &&
                                  dockables.FirstOrDefault(dockable => dockable.Id == activeToolId) is { } active
            ? active
            : dockables.FirstOrDefault();
        return toolDock;
    }

    private IDockable GetTool(string id)
    {
        if (_toolCache.TryGetValue(id, out var dockable))
        {
            return dockable;
        }

        throw new InvalidOperationException($"Unknown dock tool '{id}'.");
    }

    private IDockable PrepareHiddenTool(string id)
    {
        var dockable = GetTool(id);
        dockable.OriginalOwner = ResolveRestoreDock(id);
        return dockable;
    }

    private void UpdateRestoreDockMap()
    {
        _restoreDockByToolId.Clear();
        foreach (var descriptor in ToolDescriptors)
        {
            if (ResolveRegionDock(descriptor.Region) is { } dock)
            {
                _restoreDockByToolId[descriptor.Id] = dock;
            }
        }
    }

    private IToolDock? ResolveRegionDock(DockToolRegion region)
    {
        return region switch
        {
            DockToolRegion.Left => _leftDock ?? _rightDock ?? _bottomDock ?? _previewDock,
            DockToolRegion.Preview => _previewDock ?? _rightDock ?? _leftDock ?? _bottomDock,
            DockToolRegion.Right => _rightDock ?? _leftDock ?? _bottomDock ?? _previewDock,
            DockToolRegion.Bottom => _bottomDock ?? _rightDock ?? _leftDock ?? _previewDock,
            _ => _rightDock ?? _leftDock ?? _bottomDock ?? _previewDock
        };
    }

    private IToolDock? ResolveRestoreDock(string id)
    {
        return _restoreDockByToolId.TryGetValue(id, out var dock) ? dock : _rightDock ?? _leftDock ?? _bottomDock ?? _previewDock;
    }

    private static void AddProportionalChild(IList<IDockable> dockables, IDockable? dockable)
    {
        if (dockable is null)
        {
            return;
        }

        if (dockables.Count > 0)
        {
            dockables.Add(new ProportionalDockSplitter { CanResize = true, ResizePreview = true });
        }

        dockables.Add(dockable);
    }

    private bool HideTool(string id)
    {
        if (_rootDock is null ||
            !_toolCache.TryGetValue(id, out var dockable) ||
            !IsToolVisible(id) ||
            dockable.Owner is not IDock)
        {
            return false;
        }

        HideDockable(dockable);
        return true;
    }

    private bool AddToolToCurrentLayout(string id, IDockable dockable)
    {
        if (ResolveRestoreDock(id) is not { } dock)
        {
            return false;
        }

        AddToolToDock(dock, dockable);
        return true;
    }

    private void AddToolToDock(IToolDock dock, IDockable dockable)
    {
        dockable.OriginalOwner = null;
        if (dock.VisibleDockables?.Contains(dockable) != true)
        {
            AddDockable(dock, dockable);
        }

        dock.ActiveDockable = dockable;
    }

    private bool IsDockInCurrentLayout(IDock dock)
    {
        return _rootDock is not null &&
               EnumerateVisibleDockables(_rootDock).Any(candidate => ReferenceEquals(candidate, dock));
    }

    private IRootDock? FindHiddenRoot(IDockable dockable)
    {
        return _rootDock is null
            ? null
            : EnumerateRoots(_rootDock)
                .FirstOrDefault(root => root.HiddenDockables?.Contains(dockable) == true);
    }

    private void ActivateTool(IDockable dockable)
    {
        SetActiveDockable(dockable);
        if (dockable.Owner is IDock owner)
        {
            owner.ActiveDockable = dockable;
            SetFocusedDockable(owner, dockable);
        }

        ActivateWindow(dockable);
    }

    private static IEnumerable<IDockable> EnumerateVisibleDockables(IDockable dockable)
    {
        var visited = new HashSet<IDockable>();
        foreach (var visibleDockable in EnumerateVisibleDockables(dockable, visited))
        {
            yield return visibleDockable;
        }
    }

    private static IEnumerable<IDockable> EnumerateVisibleDockables(IDockable dockable, ISet<IDockable> visited)
    {
        if (!visited.Add(dockable))
        {
            yield break;
        }

        yield return dockable;

        if (dockable is IRootDock rootDock)
        {
            foreach (var child in EnumerateDockableList(rootDock.LeftPinnedDockables, visited))
            {
                yield return child;
            }

            foreach (var child in EnumerateDockableList(rootDock.RightPinnedDockables, visited))
            {
                yield return child;
            }

            foreach (var child in EnumerateDockableList(rootDock.TopPinnedDockables, visited))
            {
                yield return child;
            }

            foreach (var child in EnumerateDockableList(rootDock.BottomPinnedDockables, visited))
            {
                yield return child;
            }

            if (rootDock.PinnedDock is { } pinnedDock)
            {
                foreach (var child in EnumerateVisibleDockables(pinnedDock, visited))
                {
                    yield return child;
                }
            }

            if (rootDock.Windows is { } windows)
            {
                foreach (var window in windows)
                {
                    if (window.Layout is null)
                    {
                        continue;
                    }

                    foreach (var child in EnumerateVisibleDockables(window.Layout, visited))
                    {
                        yield return child;
                    }
                }
            }
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } visibleDockables)
        {
            foreach (var child in EnumerateDockableList(visibleDockables, visited))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<IDockable> EnumerateDockableList(
        IEnumerable<IDockable>? dockables,
        ISet<IDockable> visited)
    {
        if (dockables is null)
        {
            yield break;
        }

        foreach (var dockable in dockables)
        {
            foreach (var child in EnumerateVisibleDockables(dockable, visited))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<IRootDock> EnumerateRoots(IRootDock rootDock)
    {
        var visited = new HashSet<IRootDock>();
        foreach (var root in EnumerateRoots(rootDock, visited))
        {
            yield return root;
        }
    }

    private static IEnumerable<IRootDock> EnumerateRoots(IRootDock rootDock, ISet<IRootDock> visited)
    {
        if (!visited.Add(rootDock))
        {
            yield break;
        }

        yield return rootDock;

        if (rootDock.Windows is null)
        {
            yield break;
        }

        foreach (var window in rootDock.Windows)
        {
            if (window.Layout is not IRootDock childRoot)
            {
                continue;
            }

            foreach (var root in EnumerateRoots(childRoot, visited))
            {
                yield return root;
            }
        }
    }

    private static IHostWindow CreateHostWindow()
    {
        return DockSettings.ResolveFloatingWindowHostMode() == DockFloatingWindowHostMode.Managed
            ? new ManagedHostWindow()
            : new HostWindow();
    }

    private DiagnosticToolDockViewModel CreateDiagnosticsTool(
        string id,
        string title,
        DevToolsViewKind viewKind)
    {
        return new DiagnosticToolDockViewModel(_shell, id, title, viewKind);
    }

    private DiagnosticTreeDockViewModel CreateDiagnosticsTreeTool(
        string id,
        string title,
        DevToolsViewKind viewKind)
    {
        return new DiagnosticTreeDockViewModel(_shell, id, title, viewKind);
    }

    private WorkspaceFileDocumentDockViewModel CreateWorkspaceDocument(XamlPlayground.Workspace.InMemoryProjectFile file)
    {
        return new WorkspaceFileDocumentDockViewModel(_shell, file);
    }

    private void EditorDockOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IDocumentDock.ActiveDockable) ||
            _editorDock?.ActiveDockable is not WorkspaceFileDocumentDockViewModel document)
        {
            return;
        }

        _shell.ActivateWorkspaceFileFromDocument(document.File);
    }
}
