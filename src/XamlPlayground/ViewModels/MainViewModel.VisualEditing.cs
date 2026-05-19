using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Diagnostics;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XamlPlayground.Services;
using XamlPlayground.Services.Animation;
using XamlPlayground.Services.Theming;
using XamlPlayground.Services.VisualEditing;
using XamlPlayground.ViewModels.VisualEditing;
using XamlPlayground.ViewModels.Workspace;
using XamlPlayground.Workspace;

namespace XamlPlayground.ViewModels;

public partial class MainViewModel
{
    private IXamlMutationEngine _visualMutationEngine = null!;
    private XamlToolboxInsertionService _visualToolboxInsertion = null!;
    private ControlEditorRegistry _visualEditorRegistry = null!;
    private AnimationTimelineEditor _animationTimelineEditor = null!;
    private FluentControlThemeCatalog _controlThemeCatalog = null!;
    private IVisualTreeSnapshotService _visualTreeSnapshotService = null!;
    private VisualEditorSelectionService _visualSelectionService = null!;
    private ThemeResourceAnalysis _themeResourceAnalysis = ThemeResourceAnalysis.Empty;
    private XamlDocumentSnapshot? _visualEditorDocument;
    private XamlElementSelector? _visualEditorSelectedSelector;
    private bool _isRefreshingVisualEditor;
    private bool _isSynchronizingVisualEditorSelection;
    private bool _suppressVisualEditorSourceSelectionUpdate;
    private bool _isApplyingVisualEditorMutation;
    private bool _isApplyingDiagnosticsPropertyMutation;
    private InMemoryProjectFile? _diagnosticsPreviewXamlFile;
    private string? _diagnosticsPreviewSourceXamlText;
    private string? _diagnosticsAcceptedXamlText;
    private bool _diagnosticsPreviewUsesGeneratedSource;
    private bool _isSynchronizingVisualEditorPropertySelection;
    private bool _isApplyingVisualEditorPropertyGridValue;
    private XamlElementSelector? _visualEditorCurrentContainerSelector;
    private ThemeEditScope? _themeEditScope;
    private ControlThemeAnalysis _selectedControlThemeAnalysis = ControlThemeAnalysis.Empty;
    private ThemeResourceDeletePlan? _pendingThemeResourceDeletePlan;
    private bool _isRecordingAnimationKeyFrame;
    private DispatcherTimer? _animationPlaybackTimer;
    private DateTime _animationPlaybackStartedUtc;
    private int _animationPlaybackStartPercent;
    private double _animationPlaybackDurationMilliseconds = 300;

    [ObservableProperty] private ObservableCollection<VisualEditorNodeViewModel> _visualEditorStructureNodes = new();
    [ObservableProperty] private HierarchicalModel<VisualEditorNodeViewModel>? _visualEditorStructureModel;
    [ObservableProperty] private ObservableCollection<VisualEditorStructureRowViewModel> _visualEditorStructureRows = new();
    [ObservableProperty] private VisualEditorNodeViewModel? _selectedVisualEditorNode;
    [ObservableProperty] private VisualEditorStructureRowViewModel? _selectedVisualEditorStructureRow;
    [ObservableProperty] private ObservableCollection<VisualEditorPropertyViewModel> _visualEditorProperties = new();
    [ObservableProperty] private DataGridCollectionView? _visualEditorPropertiesView;
    [ObservableProperty] private VisualEditorPropertyViewModel? _selectedVisualEditorProperty;
    [ObservableProperty] private ObservableCollection<VisualEditorAvailablePropertyViewModel> _visualEditorAvailableProperties = new();
    [ObservableProperty] private DataGridCollectionView? _visualEditorAvailablePropertiesView;
    [ObservableProperty] private VisualEditorAvailablePropertyViewModel? _selectedVisualEditorAvailableProperty;
    [ObservableProperty] private string _visualEditorPropertyFilter = string.Empty;
    [ObservableProperty] private ObservableCollection<ToolboxItemDescriptor> _visualEditorToolboxItems = new();
    [ObservableProperty] private ObservableCollection<ToolboxItemDescriptor> _filteredVisualEditorToolboxItems = new();
    [ObservableProperty] private ToolboxItemDescriptor? _selectedVisualEditorToolboxItem;
    [ObservableProperty] private string _visualEditorToolboxSearch = string.Empty;
    [ObservableProperty] private string _visualEditorPropertyName = string.Empty;
    [ObservableProperty] private string _visualEditorPropertyValue = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _visualEditorPropertyOptions = new();
    [ObservableProperty] private string? _selectedVisualEditorPropertyOption;
    [ObservableProperty] private bool _visualEditorDesignerMode = true;
    [ObservableProperty] private string _visualEditorCurrentContainerTitle = "No container";
    [ObservableProperty] private bool _visualEditorPreviewSelectionVisible;
    [ObservableProperty] private double _visualEditorPreviewSelectionLeft;
    [ObservableProperty] private double _visualEditorPreviewSelectionTop;
    [ObservableProperty] private double _visualEditorPreviewSelectionWidth;
    [ObservableProperty] private double _visualEditorPreviewSelectionHeight;
    [ObservableProperty] private bool _visualEditorPreviewCurrentContainerVisible;
    [ObservableProperty] private double _visualEditorPreviewCurrentContainerLeft;
    [ObservableProperty] private double _visualEditorPreviewCurrentContainerTop;
    [ObservableProperty] private double _visualEditorPreviewCurrentContainerWidth;
    [ObservableProperty] private double _visualEditorPreviewCurrentContainerHeight;
    [ObservableProperty] private bool _visualEditorPreviewDropTargetVisible;
    [ObservableProperty] private double _visualEditorPreviewDropTargetLeft;
    [ObservableProperty] private double _visualEditorPreviewDropTargetTop;
    [ObservableProperty] private double _visualEditorPreviewDropTargetWidth;
    [ObservableProperty] private double _visualEditorPreviewDropTargetHeight;
    [ObservableProperty] private bool _visualEditorPreviewInsertionVisible;
    [ObservableProperty] private double _visualEditorPreviewInsertionLeft;
    [ObservableProperty] private double _visualEditorPreviewInsertionTop;
    [ObservableProperty] private double _visualEditorPreviewInsertionWidth;
    [ObservableProperty] private double _visualEditorPreviewInsertionHeight;
    [ObservableProperty] private bool _visualEditorPreviewDropPlaceholderVisible;
    [ObservableProperty] private double _visualEditorPreviewDropPlaceholderLeft;
    [ObservableProperty] private double _visualEditorPreviewDropPlaceholderTop;
    [ObservableProperty] private double _visualEditorPreviewDropPlaceholderWidth;
    [ObservableProperty] private double _visualEditorPreviewDropPlaceholderHeight;
    [ObservableProperty] private bool _visualEditorPreviewVerticalGuideVisible;
    [ObservableProperty] private double _visualEditorPreviewVerticalGuideLeft;
    [ObservableProperty] private double _visualEditorPreviewVerticalGuideTop;
    [ObservableProperty] private double _visualEditorPreviewVerticalGuideHeight;
    [ObservableProperty] private bool _visualEditorPreviewHorizontalGuideVisible;
    [ObservableProperty] private double _visualEditorPreviewHorizontalGuideLeft;
    [ObservableProperty] private double _visualEditorPreviewHorizontalGuideTop;
    [ObservableProperty] private double _visualEditorPreviewHorizontalGuideWidth;
    [ObservableProperty] private bool _visualEditorPreviewMeasurementVisible;
    [ObservableProperty] private double _visualEditorPreviewMeasurementLeft;
    [ObservableProperty] private double _visualEditorPreviewMeasurementTop;
    [ObservableProperty] private string _visualEditorPreviewMeasurementText = string.Empty;
    [ObservableProperty] private string? _visualEditorSourceSelectionFilePath;
    [ObservableProperty] private int _visualEditorSourceSelectionStart;
    [ObservableProperty] private int _visualEditorSourceSelectionLength;
    [ObservableProperty] private int _visualEditorSourceSelectionVersion;
    [ObservableProperty] private string _visualEditorSelectedElementTitle = "No selection";
    [ObservableProperty] private string _visualEditorStatus = "No XAML document selected.";
    [ObservableProperty] private ObservableCollection<ControlThemeDefinitionViewModel> _controlThemes = new();
    [ObservableProperty] private ObservableCollection<ControlThemeDefinitionViewModel> _filteredControlThemes = new();
    [ObservableProperty] private ControlThemeDefinitionViewModel? _selectedControlTheme;
    [ObservableProperty] private ObservableCollection<FluentControlThemeTemplateViewModel> _fluentControlThemeTemplates = new();
    [ObservableProperty] private ObservableCollection<FluentControlThemeTemplateViewModel> _filteredFluentControlThemeTemplates = new();
    [ObservableProperty] private FluentControlThemeTemplateViewModel? _selectedFluentControlThemeTemplate;
    [ObservableProperty] private string _controlThemeSearchText = string.Empty;
    [ObservableProperty] private string _controlThemeRepositoryUrl = string.Empty;
    [ObservableProperty] private string _controlThemeSourceStatus = "Fluent theme source not loaded.";
    [ObservableProperty] private string _controlThemeSelectedTargetType = "No control selected.";
    [ObservableProperty] private string _controlThemeStatus = "No custom control themes.";
    [ObservableProperty] private ObservableCollection<ThemeResourceViewModel> _themeResources = new();
    [ObservableProperty] private ObservableCollection<ThemeResourceViewModel> _filteredThemeResources = new();
    [ObservableProperty] private ThemeResourceViewModel? _selectedThemeResource;
    [ObservableProperty] private ObservableCollection<ThemeResourceUsageViewModel> _selectedThemeResourceUsages = new();
    [ObservableProperty] private ObservableCollection<ThemeResourceDiagnosticViewModel> _themeResourceDiagnostics = new();
    [ObservableProperty] private ObservableCollection<ThemeResourceDiagnosticViewModel> _filteredThemeResourceDiagnostics = new();
    [ObservableProperty] private string _themeResourceKeyEditText = string.Empty;
    [ObservableProperty] private bool _isThemeResourceDeleteDialogOpen;
    [ObservableProperty] private string _themeResourceDeleteDialogTitle = "Delete theme resource";
    [ObservableProperty] private string _themeResourceDeleteDialogMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<ThemeResourceDeleteChangeViewModel> _themeResourceDeleteChanges = new();
    [ObservableProperty] private ThemeResourceDeleteChangeViewModel? _selectedThemeResourceDeleteChange;
    [ObservableProperty] private bool _isThemeEditScopeActive;
    [ObservableProperty] private string _themeEditScopeBreadcrumb = string.Empty;
    [ObservableProperty] private ObservableCollection<ThemePreviewStateViewModel> _themePreviewStates = new();
    [ObservableProperty] private ThemePreviewStateViewModel? _selectedThemePreviewState;
    [ObservableProperty] private ObservableCollection<ThemeStateSelectorViewModel> _themeStateSelectors = new();
    [ObservableProperty] private ObservableCollection<ThemeTemplatePartViewModel> _themeTemplateParts = new();
    [ObservableProperty] private ThemeTemplatePartViewModel? _selectedThemeTemplatePart;
    [ObservableProperty] private ObservableCollection<ThemeTemplatePartSelectorViewModel> _themeTemplatePartSelectors = new();
    [ObservableProperty] private ObservableCollection<ThemeTemplateBindingViewModel> _themeTemplateBindings = new();
    [ObservableProperty] private string _themeStateSetterPropertyName = "Opacity";
    [ObservableProperty] private string _themeStateSetterValue = string.Empty;
    [ObservableProperty] private string _themeTemplatePartSetterPropertyName = "Opacity";
    [ObservableProperty] private string _themeTemplatePartSetterValue = string.Empty;
    [ObservableProperty] private ObservableCollection<ThemeVariantViewModel> _themeVariants = new();
    [ObservableProperty] private ObservableCollection<AnimationTargetOptionViewModel> _animationTargetOptions = new();
    [ObservableProperty] private AnimationTargetOptionViewModel? _selectedAnimationTargetOption;
    [ObservableProperty] private ObservableCollection<AnimationTimelineTrackViewModel> _animationTimelineTracks = new();
    [ObservableProperty] private AnimationTimelineTrackViewModel? _selectedAnimationTimelineTrack;
    [ObservableProperty] private AnimationTimelineKeyFrameViewModel? _selectedAnimationTimelineKeyFrame;
    [ObservableProperty] private ObservableCollection<AnimationPresetViewModel> _animationPresets = new();
    [ObservableProperty] private AnimationPresetViewModel? _selectedAnimationPreset;
    [ObservableProperty] private string _animationTargetSelector = "^";
    [ObservableProperty] private string _animationDurationText = "0:0:0.3";
    [ObservableProperty] private string _animationDelayText = string.Empty;
    [ObservableProperty] private string _animationIterationCountText = string.Empty;
    [ObservableProperty] private string _animationPlaybackDirectionText = "Normal";
    [ObservableProperty] private string _animationFillModeText = "Both";
    [ObservableProperty] private string _animationEasingText = "CubicEaseOut";
    [ObservableProperty] private string _animationPropertyName = "Opacity";
    [ObservableProperty] private int _animationCuePercent = 100;
    [ObservableProperty] private string _animationKeyFrameValue = "1";
    [ObservableProperty] private string _animationKeySplineText = string.Empty;
    [ObservableProperty] private int _animationCurrentTimePercent;
    [ObservableProperty] private bool _animationTimelinePlaying;
    [ObservableProperty] private bool _animationRecordModeEnabled;
    [ObservableProperty] private string _animationPlaybackStatus = "Select a visual element or control theme to edit animations.";

    public string AnimationCurrentTimeText => $"{AnimationCurrentTimePercent}%";

    public string AnimationPlaybackButtonText => AnimationTimelinePlaying ? "Pause" : "Play";

    public bool VisualEditorPreviewContentHitTestVisible => !VisualEditorDesignerMode && !IsRemotePreviewActive;

    public double VisualEditorPreviewThumbSize => 8;

    public double VisualEditorPreviewNorthWestThumbLeft => VisualEditorPreviewSelectionLeft - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewNorthWestThumbTop => VisualEditorPreviewSelectionTop - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewNorthThumbLeft => VisualEditorPreviewSelectionLeft + VisualEditorPreviewSelectionWidth / 2 - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewNorthThumbTop => VisualEditorPreviewSelectionTop - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewNorthEastThumbLeft => VisualEditorPreviewSelectionLeft + VisualEditorPreviewSelectionWidth - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewNorthEastThumbTop => VisualEditorPreviewSelectionTop - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewWestThumbLeft => VisualEditorPreviewSelectionLeft - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewWestThumbTop => VisualEditorPreviewSelectionTop + VisualEditorPreviewSelectionHeight / 2 - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewEastThumbLeft => VisualEditorPreviewSelectionLeft + VisualEditorPreviewSelectionWidth - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewEastThumbTop => VisualEditorPreviewSelectionTop + VisualEditorPreviewSelectionHeight / 2 - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewSouthWestThumbLeft => VisualEditorPreviewSelectionLeft - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewSouthWestThumbTop => VisualEditorPreviewSelectionTop + VisualEditorPreviewSelectionHeight - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewSouthThumbLeft => VisualEditorPreviewSelectionLeft + VisualEditorPreviewSelectionWidth / 2 - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewSouthThumbTop => VisualEditorPreviewSelectionTop + VisualEditorPreviewSelectionHeight - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewSouthEastThumbLeft => VisualEditorPreviewSelectionLeft + VisualEditorPreviewSelectionWidth - VisualEditorPreviewThumbSize / 2;

    public double VisualEditorPreviewSouthEastThumbTop => VisualEditorPreviewSelectionTop + VisualEditorPreviewSelectionHeight - VisualEditorPreviewThumbSize / 2;

    public ICommand RefreshVisualEditorCommand { get; private set; } = null!;

    public ICommand ApplyVisualEditorPropertyCommand { get; private set; } = null!;

    public ICommand RemoveVisualEditorPropertyCommand { get; private set; } = null!;

    public ICommand ResetVisualEditorPropertyCommand { get; private set; } = null!;

    public ICommand OpenVisualEditorPropertyResourceCommand { get; private set; } = null!;

    public ICommand DeleteVisualEditorElementCommand { get; private set; } = null!;

    public ICommand DuplicateVisualEditorElementCommand { get; private set; } = null!;

    public ICommand MoveVisualEditorElementUpCommand { get; private set; } = null!;

    public ICommand MoveVisualEditorElementDownCommand { get; private set; } = null!;

    public ICommand InsertSelectedToolboxItemCommand { get; private set; } = null!;

    public ICommand WrapVisualEditorSelectionCommand { get; private set; } = null!;

    public ICommand UnwrapVisualEditorSelectionCommand { get; private set; } = null!;

    public ICommand CreateCustomControlThemeCommand { get; private set; } = null!;

    public ICommand ApplyControlThemeCommand { get; private set; } = null!;

    public ICommand RemoveControlThemeCommand { get; private set; } = null!;

    public ICommand OpenSelectedControlThemeCommand { get; private set; } = null!;

    public ICommand OpenSelectedThemeResourceCommand { get; private set; } = null!;

    public ICommand RenameSelectedThemeResourceCommand { get; private set; } = null!;

    public ICommand DuplicateSelectedThemeResourceCommand { get; private set; } = null!;

    public ICommand DeleteSelectedThemeResourceCommand { get; private set; } = null!;

    public ICommand ConfirmThemeResourceDeleteCommand { get; private set; } = null!;

    public ICommand CancelThemeResourceDeleteCommand { get; private set; } = null!;

    public ICommand ApplySelectedThemeResourceCommand { get; private set; } = null!;

    public ICommand ReturnFromThemeEditScopeCommand { get; private set; } = null!;

    public ICommand ApplyThemeStateSetterCommand { get; private set; } = null!;

    public ICommand ApplyThemeTemplatePartSetterCommand { get; private set; } = null!;

    public ICommand CreateThemeVariantPreviewCommand { get; private set; } = null!;

    public ICommand ImportControlThemeFilesCommand { get; private set; } = null!;

    public ICommand ExportSelectedControlThemeCommand { get; private set; } = null!;

    public ICommand SaveControlThemeProjectCommand { get; private set; } = null!;

    public ICommand LoadControlThemeProjectCommand { get; private set; } = null!;

    public ICommand LoadControlThemeFolderCommand { get; private set; } = null!;

    public ICommand LoadBundledFluentThemeProjectCommand { get; private set; } = null!;

    public ICommand LoadControlThemeRepositoryCommand { get; private set; } = null!;

    public ICommand AddAnimationTrackCommand { get; private set; } = null!;

    public ICommand AddAnimationKeyFrameCommand { get; private set; } = null!;

    public ICommand UpdateAnimationKeyFrameCommand { get; private set; } = null!;

    public ICommand CommitAnimationKeyFrameEditCommand { get; private set; } = null!;

    public ICommand RemoveAnimationKeyFrameCommand { get; private set; } = null!;

    public ICommand ApplyAnimationTimelineCommand { get; private set; } = null!;

    public ICommand PreviewAnimationTimelineCommand { get; private set; } = null!;

    public ICommand ApplyAnimationFrameToTargetCommand { get; private set; } = null!;

    public ICommand CaptureAnimationKeyFrameCommand { get; private set; } = null!;

    public ICommand DuplicateAnimationKeyFrameCommand { get; private set; } = null!;

    public ICommand SelectPreviousAnimationKeyFrameCommand { get; private set; } = null!;

    public ICommand SelectNextAnimationKeyFrameCommand { get; private set; } = null!;

    public ICommand NudgeAnimationKeyFrameLeftCommand { get; private set; } = null!;

    public ICommand NudgeAnimationKeyFrameRightCommand { get; private set; } = null!;

    public ICommand NudgeAnimationKeyFrameLeftLargeCommand { get; private set; } = null!;

    public ICommand NudgeAnimationKeyFrameRightLargeCommand { get; private set; } = null!;

    public ICommand SeekAnimationStartCommand { get; private set; } = null!;

    public ICommand SeekAnimationEndCommand { get; private set; } = null!;

    public ICommand PlayAnimationTimelineCommand { get; private set; } = null!;

    public ICommand StopAnimationTimelineCommand { get; private set; } = null!;

    private void InitializeVisualEditing()
    {
        _visualMutationEngine = new XamlMutationEngine();
        _visualToolboxInsertion = new XamlToolboxInsertionService(_visualMutationEngine);
        _visualEditorRegistry = new ControlEditorRegistry();
        _animationTimelineEditor = new AnimationTimelineEditor();
        _controlThemeCatalog = new FluentControlThemeCatalog();
        _visualTreeSnapshotService = new AvaloniaVisualTreeSnapshotService();
        _visualSelectionService = new VisualEditorSelectionService(_visualMutationEngine, new XamlVisualTreeMapper());
        _diagnosticsPropertyEditHandler = new DiagnosticsPropertyEditHandler(this);
        DiagnosticsDevToolsOptions = CreateDiagnosticsDevToolsOptions(_diagnosticsPropertyEditHandler);

        RefreshVisualEditorCommand = new RelayCommand(() => RefreshVisualEditingModel());
        ApplyVisualEditorPropertyCommand = new RelayCommand(ApplyVisualEditorProperty);
        RemoveVisualEditorPropertyCommand = new RelayCommand(RemoveVisualEditorProperty);
        ResetVisualEditorPropertyCommand = new RelayCommand(ResetVisualEditorProperty);
        OpenVisualEditorPropertyResourceCommand = new RelayCommand(OpenVisualEditorPropertyResource, CanOpenVisualEditorPropertyResource);
        DeleteVisualEditorElementCommand = new RelayCommand(DeleteVisualEditorElement);
        DuplicateVisualEditorElementCommand = new RelayCommand(DuplicateVisualEditorElement);
        MoveVisualEditorElementUpCommand = new RelayCommand(MoveVisualEditorElementUp);
        MoveVisualEditorElementDownCommand = new RelayCommand(MoveVisualEditorElementDown);
        InsertSelectedToolboxItemCommand = new RelayCommand(InsertSelectedToolboxItem);
        WrapVisualEditorSelectionCommand = new RelayCommand(WrapVisualEditorSelection);
        UnwrapVisualEditorSelectionCommand = new RelayCommand(UnwrapVisualEditorSelection);
        CreateCustomControlThemeCommand = new RelayCommand(CreateCustomControlTheme, CanCreateCustomControlTheme);
        ApplyControlThemeCommand = new RelayCommand<ControlThemeDefinitionViewModel?>(ApplyControlTheme, CanApplyControlTheme);
        RemoveControlThemeCommand = new RelayCommand(RemoveControlTheme, CanRemoveControlTheme);
        OpenSelectedControlThemeCommand = new RelayCommand(OpenSelectedControlTheme, () => SelectedControlTheme is not null);
        OpenSelectedThemeResourceCommand = new RelayCommand(OpenSelectedThemeResource, () => SelectedThemeResource is not null);
        RenameSelectedThemeResourceCommand = new RelayCommand(RenameSelectedThemeResource, CanRenameSelectedThemeResource);
        DuplicateSelectedThemeResourceCommand = new RelayCommand(DuplicateSelectedThemeResource, () => SelectedThemeResource is not null);
        DeleteSelectedThemeResourceCommand = new RelayCommand(DeleteSelectedThemeResource, () => SelectedThemeResource is not null);
        ConfirmThemeResourceDeleteCommand = new RelayCommand(ConfirmThemeResourceDelete, () => _pendingThemeResourceDeletePlan is not null);
        CancelThemeResourceDeleteCommand = new RelayCommand(CancelThemeResourceDelete, () => IsThemeResourceDeleteDialogOpen);
        ApplySelectedThemeResourceCommand = new RelayCommand(ApplySelectedThemeResource, CanApplySelectedThemeResource);
        ReturnFromThemeEditScopeCommand = new RelayCommand(ReturnFromThemeEditScope, () => IsThemeEditScopeActive);
        ApplyThemeStateSetterCommand = new RelayCommand(ApplyThemeStateSetter, CanApplyThemeStateSetter);
        ApplyThemeTemplatePartSetterCommand = new RelayCommand(ApplyThemeTemplatePartSetter, CanApplyThemeTemplatePartSetter);
        CreateThemeVariantPreviewCommand = new RelayCommand(CreateThemeVariantPreview, CanCreateThemeVariantPreview);
        ImportControlThemeFilesCommand = new AsyncRelayCommand(ImportControlThemeFiles, () => ActiveProject is not null);
        ExportSelectedControlThemeCommand = new AsyncRelayCommand(ExportSelectedControlTheme, () => SelectedControlTheme is not null);
        SaveControlThemeProjectCommand = new AsyncRelayCommand(SaveControlThemeProject, CanSaveControlThemeProject);
        LoadControlThemeProjectCommand = new AsyncRelayCommand(LoadControlThemeProject, () => ActiveProject is not null);
        LoadControlThemeFolderCommand = new AsyncRelayCommand(LoadControlThemeFolder, () => ActiveProject is not null);
        LoadBundledFluentThemeProjectCommand = new RelayCommand(LoadBundledFluentThemeProject, () => ActiveProject is not null);
        LoadControlThemeRepositoryCommand = new AsyncRelayCommand(LoadControlThemeRepository, CanLoadControlThemeRepository);
        AddAnimationTrackCommand = new RelayCommand(AddAnimationTrack, CanAddAnimationTrack);
        AddAnimationKeyFrameCommand = new RelayCommand(AddAnimationKeyFrame, CanEditAnimationKeyFrames);
        UpdateAnimationKeyFrameCommand = new RelayCommand(UpdateAnimationKeyFrame, () => SelectedAnimationTimelineKeyFrame is not null);
        CommitAnimationKeyFrameEditCommand = new RelayCommand(CommitAnimationKeyFrameEdit, () => SelectedAnimationTimelineKeyFrame is not null);
        RemoveAnimationKeyFrameCommand = new RelayCommand(RemoveAnimationKeyFrame, () => SelectedAnimationTimelineKeyFrame is not null);
        ApplyAnimationTimelineCommand = new RelayCommand(ApplyAnimationTimeline, CanApplyAnimationTimeline);
        PreviewAnimationTimelineCommand = new RelayCommand(PreviewAnimationTimeline, CanApplyAnimationTimeline);
        ApplyAnimationFrameToTargetCommand = new RelayCommand(ApplyAnimationFrameToTarget, CanApplyAnimationFrameToTarget);
        CaptureAnimationKeyFrameCommand = new RelayCommand(CaptureAnimationKeyFrame, CanCaptureAnimationKeyFrame);
        DuplicateAnimationKeyFrameCommand = new RelayCommand(DuplicateAnimationKeyFrame, CanDuplicateAnimationKeyFrame);
        SelectPreviousAnimationKeyFrameCommand = new RelayCommand(() => SelectAdjacentAnimationKeyFrame(previous: true), CanSelectAdjacentAnimationKeyFrame);
        SelectNextAnimationKeyFrameCommand = new RelayCommand(() => SelectAdjacentAnimationKeyFrame(previous: false), CanSelectAdjacentAnimationKeyFrame);
        NudgeAnimationKeyFrameLeftCommand = new RelayCommand(() => NudgeAnimationKeyFrame(-1), CanNudgeAnimationKeyFrame);
        NudgeAnimationKeyFrameRightCommand = new RelayCommand(() => NudgeAnimationKeyFrame(1), CanNudgeAnimationKeyFrame);
        NudgeAnimationKeyFrameLeftLargeCommand = new RelayCommand(() => NudgeAnimationKeyFrame(-10), CanNudgeAnimationKeyFrame);
        NudgeAnimationKeyFrameRightLargeCommand = new RelayCommand(() => NudgeAnimationKeyFrame(10), CanNudgeAnimationKeyFrame);
        SeekAnimationStartCommand = new RelayCommand(() => SeekAnimationPlayhead(0), CanSeekAnimationTimeline);
        SeekAnimationEndCommand = new RelayCommand(() => SeekAnimationPlayhead(100), CanSeekAnimationTimeline);
        PlayAnimationTimelineCommand = new RelayCommand(PlayAnimationTimeline, CanPlayAnimationTimeline);
        StopAnimationTimelineCommand = new RelayCommand(() => StopAnimationTimeline(completed: false), () => AnimationTimelinePlaying);

        InitializeDesignInspection();
        LoadVisualEditorToolbox();
        LoadAnimationPresets();
        LoadFluentControlThemeTemplates();
        RefreshControlThemes();
        RefreshAnimationTargetOptions();
    }

    partial void OnSelectedVisualEditorNodeChanged(VisualEditorNodeViewModel? value)
    {
        if (_isRefreshingVisualEditor)
        {
            return;
        }

        SynchronizeVisualEditorStructureRow(value);
        VisualEditorPreviewSelectionVisible = false;
        SelectVisualEditorElement(value?.Element);
    }

    partial void OnSelectedVisualEditorStructureRowChanged(VisualEditorStructureRowViewModel? value)
    {
        if (_isRefreshingVisualEditor ||
            _isSynchronizingVisualEditorSelection)
        {
            return;
        }

        if (!ReferenceEquals(SelectedVisualEditorNode, value?.Node))
        {
            SelectedVisualEditorNode = value?.Node;
        }
    }

    partial void OnSelectedVisualEditorPropertyChanged(VisualEditorPropertyViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        VisualEditorPropertyName = value.MutationName;
        VisualEditorPropertyValue = value.Value;
        RefreshVisualEditorPropertyOptions(value.Property);
        if (VisualEditorPropertiesView is not null &&
            !ReferenceEquals(VisualEditorPropertiesView.CurrentItem, value))
        {
            VisualEditorPropertiesView.MoveCurrentTo(value);
        }

        if (_isSynchronizingVisualEditorPropertySelection)
        {
            return;
        }

        try
        {
            _isSynchronizingVisualEditorPropertySelection = true;
            SelectedVisualEditorAvailableProperty = VisualEditorAvailableProperties.FirstOrDefault(property =>
                string.Equals(property.Name, value.Name, StringComparison.Ordinal) ||
                string.Equals(property.Name, value.MutationName, StringComparison.Ordinal));
        }
        finally
        {
            _isSynchronizingVisualEditorPropertySelection = false;
        }
    }

    partial void OnVisualEditorPropertyValueChanged(string value)
    {
        (OpenVisualEditorPropertyResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedVisualEditorAvailablePropertyChanged(VisualEditorAvailablePropertyViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        VisualEditorPropertyName = value.Name;
        VisualEditorPropertyValue = TryGetVisualEditorPropertyValue(value.Name, out var propertyValue)
            ? propertyValue
            : string.Empty;
        RefreshVisualEditorPropertyOptions(value.Property);
        if (VisualEditorAvailablePropertiesView is not null &&
            !ReferenceEquals(VisualEditorAvailablePropertiesView.CurrentItem, value))
        {
            VisualEditorAvailablePropertiesView.MoveCurrentTo(value);
        }

        if (_isSynchronizingVisualEditorPropertySelection)
        {
            return;
        }

        try
        {
            _isSynchronizingVisualEditorPropertySelection = true;
            var propertyRow = VisualEditorProperties.FirstOrDefault(property =>
                string.Equals(property.Name, value.Name, StringComparison.Ordinal) ||
                string.Equals(property.MutationName, value.Name, StringComparison.Ordinal));
            SelectedVisualEditorProperty = propertyRow;
            if (propertyRow is not null &&
                VisualEditorPropertiesView is not null &&
                !ReferenceEquals(VisualEditorPropertiesView.CurrentItem, propertyRow))
            {
                VisualEditorPropertiesView.MoveCurrentTo(propertyRow);
            }
        }
        finally
        {
            _isSynchronizingVisualEditorPropertySelection = false;
        }
    }

    partial void OnSelectedControlThemeChanged(ControlThemeDefinitionViewModel? value)
    {
        RefreshSelectedControlThemeAnalysis();
        RefreshAnimationTargetOptions();
        NotifyControlThemeCommandsChanged();
    }

    partial void OnSelectedFluentControlThemeTemplateChanged(FluentControlThemeTemplateViewModel? value)
    {
        RefreshControlThemeSelectionState();
    }

    partial void OnControlThemeSearchTextChanged(string value)
    {
        RefreshControlThemeFilters();
    }

    partial void OnControlThemeRepositoryUrlChanged(string value)
    {
        (LoadControlThemeRepositoryCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedThemeResourceChanged(ThemeResourceViewModel? value)
    {
        ClearThemeResourceDeleteDialog();
        ThemeResourceKeyEditText = value?.Key ?? string.Empty;
        RefreshSelectedThemeResourceUsages();
        RefreshSelectedResourceThemeAnalysis(value);
        NotifyControlThemeCommandsChanged();
    }

    partial void OnThemeResourceKeyEditTextChanged(string value)
    {
        NotifyControlThemeCommandsChanged();
    }

    partial void OnSelectedThemePreviewStateChanged(ThemePreviewStateViewModel? value)
    {
        RefreshThemeStateSelectors();
        RefreshThemeTemplatePartSelectors();
        RefreshAnimationTargetOptions();
        ThemeStateSetterValue = string.Empty;
        ThemeTemplatePartSetterValue = string.Empty;
        ControlThemeStatus = value is null || string.Equals(value.State, "normal", StringComparison.Ordinal)
            ? "Theme preview state: Normal."
            : $"Theme preview state: {value.PseudoClass}.";
        NotifyControlThemeCommandsChanged();
    }

    partial void OnThemeStateSetterPropertyNameChanged(string value)
    {
        NotifyControlThemeCommandsChanged();
    }

    partial void OnThemeStateSetterValueChanged(string value)
    {
        NotifyControlThemeCommandsChanged();
    }

    partial void OnSelectedThemeTemplatePartChanged(ThemeTemplatePartViewModel? value)
    {
        RefreshThemeTemplatePartSelectors();
        RefreshAnimationTargetOptions();
        NotifyControlThemeCommandsChanged();
    }

    partial void OnThemeTemplatePartSetterPropertyNameChanged(string value)
    {
        NotifyControlThemeCommandsChanged();
    }

    partial void OnThemeTemplatePartSetterValueChanged(string value)
    {
        NotifyControlThemeCommandsChanged();
    }

    partial void OnIsThemeResourceDeleteDialogOpenChanged(bool value)
    {
        (ConfirmThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CancelThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnVisualEditorPropertyFilterChanged(string value)
    {
        ApplyVisualEditorAvailablePropertiesFilter();
    }

    partial void OnSelectedVisualEditorPropertyOptionChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            VisualEditorPropertyValue = value;
        }
    }

    partial void OnVisualEditorToolboxSearchChanged(string value)
    {
        FilterVisualEditorToolbox();
    }

    partial void OnSelectedAnimationTargetOptionChanged(AnimationTargetOptionViewModel? value)
    {
        StopAnimationTimeline(completed: false);
        if (value is null)
        {
            ClearAnimationTimelineEditor();
            NotifyAnimationCommandsChanged();
            return;
        }

        AnimationTargetSelector = value.Selector;
        LoadAnimationTimelineFromTarget();
        NotifyAnimationCommandsChanged();
    }

    partial void OnAnimationTargetSelectorChanged(string value)
    {
        NotifyAnimationCommandsChanged();
    }

    partial void OnAnimationPropertyNameChanged(string value)
    {
        NotifyAnimationCommandsChanged();
    }

    partial void OnSelectedAnimationTimelineTrackChanged(AnimationTimelineTrackViewModel? value)
    {
        if (value is null)
        {
            SelectedAnimationTimelineKeyFrame = null;
        }
        else
        {
            AnimationPropertyName = value.PropertyName;
            SelectedAnimationTimelineKeyFrame = value.KeyFrames
                .OrderBy(static frame => frame.CuePercent)
                .FirstOrDefault();
        }

        NotifyAnimationCommandsChanged();
    }

    partial void OnSelectedAnimationTimelineKeyFrameChanged(AnimationTimelineKeyFrameViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        AnimationCuePercent = value.CuePercent;
        AnimationKeyFrameValue = value.Value;
        AnimationKeySplineText = value.KeySpline;
        AnimationCurrentTimePercent = value.CuePercent;
        NotifyAnimationCommandsChanged();
    }

    partial void OnAnimationCurrentTimePercentChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 100);
        if (clamped != value)
        {
            AnimationCurrentTimePercent = clamped;
            return;
        }

        OnPropertyChanged(nameof(AnimationCurrentTimeText));
    }

    partial void OnAnimationTimelinePlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(AnimationPlaybackButtonText));
        NotifyAnimationCommandsChanged();
    }

    partial void OnSelectedAnimationPresetChanged(AnimationPresetViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        AnimationPropertyName = value.PropertyName;
        AnimationDurationText = value.Duration;
        AnimationEasingText = value.Easing;
        AnimationKeyFrameValue = value.ToValue;
    }

    partial void OnVisualEditorDesignerModeChanged(bool value)
    {
        OnPropertyChanged(nameof(VisualEditorPreviewContentHitTestVisible));
        OnPropertyChanged(nameof(IsVisualEditorOverlayActive));
        if (!value)
        {
            ClearVisualEditorPreviewDropFeedback();
        }
    }

    partial void OnVisualEditorPreviewSelectionLeftChanged(double value)
    {
        NotifyVisualEditorPreviewSelectionGeometryChanged();
    }

    partial void OnVisualEditorPreviewSelectionTopChanged(double value)
    {
        NotifyVisualEditorPreviewSelectionGeometryChanged();
    }

    partial void OnVisualEditorPreviewSelectionWidthChanged(double value)
    {
        NotifyVisualEditorPreviewSelectionGeometryChanged();
    }

    partial void OnVisualEditorPreviewSelectionHeightChanged(double value)
    {
        NotifyVisualEditorPreviewSelectionGeometryChanged();
    }

    private void RefreshVisualEditingModel(bool updateSourceSelection = true)
    {
        var previousSuppressSourceSelectionUpdate = _suppressVisualEditorSourceSelectionUpdate;
        if (!updateSourceSelection)
        {
            _suppressVisualEditorSourceSelectionUpdate = true;
        }

        try
        {
            if (ActiveXamlFile is not { } xamlFile)
            {
                ClearVisualEditor("No XAML document selected.");
                return;
            }

            if (!xamlFile.IsXaml)
            {
                ClearVisualEditor($"{xamlFile.Path} is not a XAML document.");
                return;
            }

            _visualEditorDocument = _visualMutationEngine.Analyze(xamlFile.Text);
            var roots = BuildVisualEditorNodes(_visualEditorDocument);
            var rows = BuildVisualEditorStructureRows(roots);
            var rootCollection = new ObservableCollection<VisualEditorNodeViewModel>(roots);
            var structureModel = CreateVisualEditorStructureModel(rootCollection);

            _isRefreshingVisualEditor = true;
            try
            {
                VisualEditorStructureNodes = rootCollection;
                VisualEditorStructureModel = structureModel;
                VisualEditorStructureRows = new ObservableCollection<VisualEditorStructureRowViewModel>(rows);
                var selected = FindNode(roots, _visualEditorSelectedSelector);
                var usedPassiveFallbackSelection = selected is null;
                SelectedVisualEditorNode = selected;
                SelectedVisualEditorStructureRow = rows.FirstOrDefault(row => ReferenceEquals(row.Node, selected));
                var previousSuppressSelectionUpdate = _suppressVisualEditorSourceSelectionUpdate;
                if (usedPassiveFallbackSelection)
                {
                    _suppressVisualEditorSourceSelectionUpdate = true;
                }

                try
                {
                    SelectVisualEditorElement(selected?.Element);
                }
                finally
                {
                    _suppressVisualEditorSourceSelectionUpdate = previousSuppressSelectionUpdate;
                }

                if (usedPassiveFallbackSelection)
                {
                    _visualEditorSelectedSelector = null;
                    if (VisualEditorSourceSelectionFilePath is not null ||
                        VisualEditorSourceSelectionLength != 0)
                    {
                        ClearVisualEditorSourceSelection();
                    }
                }
            }
            finally
            {
                _isRefreshingVisualEditor = false;
            }
        }
        finally
        {
            _suppressVisualEditorSourceSelectionUpdate = previousSuppressSourceSelectionUpdate;
        }
    }

    private void ClearVisualEditor(string status)
    {
        _visualEditorDocument = null;
        _visualEditorSelectedSelector = null;
        _visualEditorCurrentContainerSelector = null;
        VisualEditorStructureNodes = new ObservableCollection<VisualEditorNodeViewModel>();
        VisualEditorStructureModel = null;
        VisualEditorStructureRows = new ObservableCollection<VisualEditorStructureRowViewModel>();
        SelectedVisualEditorNode = null;
        SelectedVisualEditorStructureRow = null;
        VisualEditorProperties = new ObservableCollection<VisualEditorPropertyViewModel>();
        VisualEditorPropertiesView = null;
        SelectedVisualEditorProperty = null;
        VisualEditorAvailableProperties = new ObservableCollection<VisualEditorAvailablePropertyViewModel>();
        VisualEditorAvailablePropertiesView = null;
        SelectedVisualEditorAvailableProperty = null;
        VisualEditorPropertyOptions = new ObservableCollection<string>();
        SelectedVisualEditorPropertyOption = null;
        VisualEditorPreviewSelectionVisible = false;
        VisualEditorPreviewCurrentContainerVisible = false;
        ClearVisualEditorSourceSelection();
        VisualEditorSelectedElementTitle = "No selection";
        VisualEditorCurrentContainerTitle = "No container";
        VisualEditorStatus = status;
        RefreshControlThemeSelectionState();
        RefreshAnimationTargetOptions();
    }

    private void SelectVisualEditorElement(XamlElementSnapshot? element)
    {
        if (element is null)
        {
            _visualEditorSelectedSelector = null;
            _visualEditorCurrentContainerSelector = null;
            VisualEditorSelectedElementTitle = "No selection";
            VisualEditorCurrentContainerTitle = "No container";
            VisualEditorProperties = new ObservableCollection<VisualEditorPropertyViewModel>();
            VisualEditorPropertiesView = null;
            SelectedVisualEditorProperty = null;
            VisualEditorAvailableProperties = new ObservableCollection<VisualEditorAvailablePropertyViewModel>();
            VisualEditorAvailablePropertiesView = null;
            SelectedVisualEditorAvailableProperty = null;
            VisualEditorPropertyOptions = new ObservableCollection<string>();
            SelectedVisualEditorPropertyOption = null;
            VisualEditorPreviewSelectionVisible = false;
            VisualEditorPreviewCurrentContainerVisible = false;
            ClearVisualEditorSourceSelection();
            VisualEditorStatus = _visualEditorDocument?.Diagnostics.FirstOrDefault() ?? "No XAML element selected.";
            RefreshControlThemeSelectionState();
            RefreshAnimationTargetOptions();
            return;
        }

        _visualEditorSelectedSelector = element.Selector;
        if (!_suppressVisualEditorSourceSelectionUpdate)
        {
            SetVisualEditorSourceSelection(element);
        }
        VisualEditorSelectedElementTitle = string.IsNullOrWhiteSpace(element.Name)
            ? element.TypeName
            : $"{element.TypeName} #{element.Name}";
        SetVisualEditorCurrentContainer(FindCurrentContainerForSelection(element));
        VisualEditorProperties = new ObservableCollection<VisualEditorPropertyViewModel>(
            BuildVisualEditorPropertyRows(element));
        VisualEditorPropertiesView = CreateGroupedVisualEditorPropertyRowsView(VisualEditorProperties);
        VisualEditorAvailableProperties = new ObservableCollection<VisualEditorAvailablePropertyViewModel>(
            ResolveEditorDescriptor(element)
                .Properties
                .Select(static property => new VisualEditorAvailablePropertyViewModel(property)));
        VisualEditorAvailablePropertiesView = CreateGroupedVisualEditorPropertiesView(VisualEditorAvailableProperties);
        ApplyVisualEditorAvailablePropertiesFilter();
        var preferredPropertyName = VisualEditorPropertyName;
        SelectedVisualEditorAvailableProperty =
            VisualEditorAvailableProperties.FirstOrDefault(property =>
                !IsIdentityProperty(property.Name) &&
                string.Equals(property.Name, preferredPropertyName, StringComparison.Ordinal) &&
                HasVisualEditorAttribute(element, property.Name)) ??
            VisualEditorAvailableProperties.FirstOrDefault(property =>
                IsPrimaryContentProperty(property.Name) &&
                HasVisualEditorAttribute(element, property.Name)) ??
            VisualEditorAvailableProperties.FirstOrDefault(property =>
                string.Equals(property.Name, preferredPropertyName, StringComparison.Ordinal) &&
                HasVisualEditorAttribute(element, property.Name)) ??
            VisualEditorAvailableProperties.FirstOrDefault(property =>
                HasVisualEditorAttribute(element, property.Name)) ??
            VisualEditorAvailableProperties.FirstOrDefault(property =>
                string.Equals(property.Name, preferredPropertyName, StringComparison.Ordinal)) ??
            VisualEditorAvailableProperties.FirstOrDefault();
        if (SelectedVisualEditorAvailableProperty is not null)
        {
            VisualEditorAvailablePropertiesView?.MoveCurrentTo(SelectedVisualEditorAvailableProperty);
        }

        SelectedVisualEditorProperty =
            SelectedVisualEditorAvailableProperty is null
                ? VisualEditorProperties.FirstOrDefault()
                : VisualEditorProperties.FirstOrDefault(property =>
                    string.Equals(property.Name, SelectedVisualEditorAvailableProperty.Name, StringComparison.Ordinal) ||
                    string.Equals(property.MutationName, SelectedVisualEditorAvailableProperty.Name, StringComparison.Ordinal)) ??
                  VisualEditorProperties.FirstOrDefault();
        if (SelectedVisualEditorProperty is not null)
        {
            VisualEditorPropertiesView?.MoveCurrentTo(SelectedVisualEditorProperty);
        }
        VisualEditorStatus = $"{element.TypeName}: {element.Attributes.Count} attribute(s), {element.ChildElementCount} child element(s)";
        RefreshControlThemeSelectionState();
        RefreshAnimationTargetOptions();
    }

    private IReadOnlyList<VisualEditorPropertyViewModel> BuildVisualEditorPropertyRows(XamlElementSnapshot element)
    {
        var descriptor = ResolveEditorDescriptor(element);
        var rows = new List<VisualEditorPropertyViewModel>();
        var representedAttributes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in descriptor.Properties)
        {
            var mutationName = ResolveMutationPropertyName(element, property.PropertyName);
            var isSet = TryGetVisualEditorAttributeValue(element, property.PropertyName, out var value);
            rows.Add(new VisualEditorPropertyViewModel(
                property,
                value,
                isSet,
                mutationName,
                ApplyVisualEditorPropertyFromGrid));

            representedAttributes.Add(property.PropertyName);
            representedAttributes.Add(mutationName);
        }

        foreach (var attribute in element.Attributes.OrderBy(static attribute => attribute.Key, StringComparer.Ordinal))
        {
            if (representedAttributes.Contains(attribute.Key))
            {
                continue;
            }

            rows.Add(new VisualEditorPropertyViewModel(
                new ControlEditorProperty(
                    attribute.Key,
                    attribute.Key,
                    ControlEditorValueKind.String,
                    "Attributes"),
                attribute.Value,
                isSet: true,
                mutationName: attribute.Key,
                valueChanged: ApplyVisualEditorPropertyFromGrid));
        }

        return rows;
    }

    private static string ResolveMutationPropertyName(XamlElementSnapshot element, string propertyName)
    {
        if (string.Equals(propertyName, "Name", StringComparison.Ordinal) &&
            !element.Attributes.ContainsKey("Name") &&
            element.Attributes.ContainsKey("x:Name"))
        {
            return "x:Name";
        }

        return propertyName;
    }

    private void SetVisualEditorCurrentContainer(XamlElementSnapshot? container)
    {
        _visualEditorCurrentContainerSelector = container?.Selector;
        VisualEditorCurrentContainerTitle = container is null
            ? "No container"
            : FormatVisualEditorElementTitle(container);
    }

    private XamlElementSnapshot? FindCurrentContainerForSelection(XamlElementSnapshot? element)
    {
        if (_visualEditorDocument is null ||
            element is null)
        {
            return null;
        }

        if (IsContainerElement(element))
        {
            return element;
        }

        return _visualEditorDocument.Elements
            .Where(candidate =>
                candidate.Path.Count < element.Path.Count &&
                IsSameOrDescendantPath(element.Path, candidate.Path) &&
                IsContainerElement(candidate))
            .OrderByDescending(static candidate => candidate.Path.Count)
            .FirstOrDefault() ??
            _visualEditorDocument.Elements.FirstOrDefault(candidate => candidate.Path.Count == 0);
    }

    private VisualEditorStructureRowViewModel[] GetCurrentContainerChildRows()
    {
        var container = FindElement(_visualEditorDocument, _visualEditorCurrentContainerSelector);
        if (container is null)
        {
            return Array.Empty<VisualEditorStructureRowViewModel>();
        }

        return VisualEditorStructureRows
            .Where(row => HasParentPath(row.Element, container.Path))
            .ToArray();
    }

    private static bool HasParentPath(XamlElementSnapshot element, IReadOnlyList<int> parentPath)
    {
        return element.Path.Count == parentPath.Count + 1 &&
               element.Path.Take(parentPath.Count).SequenceEqual(parentPath);
    }

    private static bool IsSameOrDescendantPath(IReadOnlyList<int> path, IReadOnlyList<int> ancestorPath)
    {
        return path.Count >= ancestorPath.Count &&
               path.Take(ancestorPath.Count).SequenceEqual(ancestorPath);
    }

    private bool IsContainerElement(XamlElementSnapshot element)
    {
        if (element.Path.Count == 0)
        {
            return true;
        }

        var type = ResolveControlType(element.TypeName);
        return type is not null &&
               (typeof(Panel).IsAssignableFrom(type) ||
                element.ChildElementCount == 0 &&
                (typeof(Decorator).IsAssignableFrom(type) ||
                 typeof(ContentControl).IsAssignableFrom(type) &&
                 !element.Attributes.ContainsKey("Content")));
    }

    private static string FormatVisualEditorElementTitle(XamlElementSnapshot element)
    {
        return string.IsNullOrWhiteSpace(element.Name)
            ? element.TypeName
            : $"{element.TypeName} #{element.Name}";
    }

    private static string CreateElementStyleSelector(XamlElementSnapshot element)
    {
        var typeName = element.TypeName.Trim();
        var colonIndex = typeName.IndexOf(':', StringComparison.Ordinal);
        return colonIndex > 0
            ? $"{typeName[..colonIndex]}|{typeName[(colonIndex + 1)..]}"
            : typeName;
    }

    private void ApplyVisualEditorPropertyFromGrid(VisualEditorPropertyViewModel property, string value)
    {
        if (_isApplyingVisualEditorMutation ||
            _isApplyingVisualEditorPropertyGridValue ||
            !TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            return;
        }

        try
        {
            _isApplyingVisualEditorPropertyGridValue = true;
            VisualEditorPropertyName = property.MutationName;
            VisualEditorPropertyValue = value;
            ApplyVisualEditorMutation(_visualMutationEngine.SetProperty(
                xamlFile.Text,
                selector,
                property.MutationName,
                value));
            RecordAnimationKeyFrameFromVisualEdit(
                new[] { (PropertyName: property.MutationName, Value: value) },
                applyTimeline: true);
        }
        finally
        {
            _isApplyingVisualEditorPropertyGridValue = false;
        }
    }

    private bool TryGetVisualEditorPropertyValue(string propertyName, out string value)
    {
        if (SelectedVisualEditorNode?.Element is { } element &&
            TryGetVisualEditorAttributeValue(element, propertyName, out value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool HasVisualEditorAttribute(XamlElementSnapshot element, string propertyName)
    {
        return TryGetVisualEditorAttributeValue(element, propertyName, out _);
    }

    private static bool TryGetVisualEditorAttributeValue(
        XamlElementSnapshot element,
        string propertyName,
        out string value)
    {
        if (element.Attributes.TryGetValue(propertyName, out value!))
        {
            return true;
        }

        if (string.Equals(propertyName, "Name", StringComparison.Ordinal) &&
            element.Attributes.TryGetValue("x:Name", out value!))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsPrimaryContentProperty(string propertyName)
    {
        return propertyName is "Content" or "Text" or "Header";
    }

    private static bool IsIdentityProperty(string propertyName)
    {
        return propertyName is "Name" or "Classes" ||
               propertyName.StartsWith("AutomationProperties.", StringComparison.Ordinal);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The visual editor property grid uses ProDataGrid grouping for design-time metadata.")]
    private static DataGridCollectionView CreateGroupedVisualEditorPropertyRowsView(
        IEnumerable<VisualEditorPropertyViewModel> properties)
    {
        var view = new DataGridCollectionView(properties.ToArray());
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(VisualEditorPropertyViewModel.Group)));
        view.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(VisualEditorPropertyViewModel.Group),
            ListSortDirection.Ascending));
        view.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(VisualEditorPropertyViewModel.Name),
            ListSortDirection.Ascending));
        return view;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The visual editor property grid uses ProDataGrid grouping for design-time metadata.")]
    private static DataGridCollectionView CreateGroupedVisualEditorPropertiesView(
        IEnumerable<VisualEditorAvailablePropertyViewModel> properties)
    {
        var view = new DataGridCollectionView(properties.ToArray());
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(VisualEditorAvailablePropertyViewModel.Group)));
        return view;
    }

    private void ApplyVisualEditorAvailablePropertiesFilter()
    {
        var query = VisualEditorPropertyFilter.Trim();

        if (VisualEditorPropertiesView is { } propertiesView)
        {
            propertiesView.Filter = string.IsNullOrWhiteSpace(query)
                ? null!
                : item => item is VisualEditorPropertyViewModel property &&
                          MatchesVisualEditorPropertyFilter(property, query);

            if (SelectedVisualEditorProperty is { } selectedProperty &&
                propertiesView.Contains(selectedProperty))
            {
                propertiesView.MoveCurrentTo(selectedProperty);
            }
            else
            {
                propertiesView.MoveCurrentToFirst();
            }
        }

        if (VisualEditorAvailablePropertiesView is not { } view)
        {
            return;
        }

        view.Filter = string.IsNullOrWhiteSpace(query)
            ? null!
            : item => item is VisualEditorAvailablePropertyViewModel property &&
                      MatchesVisualEditorPropertyFilter(property, query);

        if (SelectedVisualEditorAvailableProperty is { } selected &&
            view.Contains(selected))
        {
            view.MoveCurrentTo(selected);
        }
        else
        {
            view.MoveCurrentToFirst();
        }
    }

    private static bool MatchesVisualEditorPropertyFilter(
        VisualEditorPropertyViewModel property,
        string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token =>
                property.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.MutationName.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.Group.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.Category.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.Kind.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.TypeName.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.Value.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.Priority.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesVisualEditorPropertyFilter(
        VisualEditorAvailablePropertyViewModel property,
        string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token =>
                property.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.DisplayName.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.Group.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                property.Kind.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private void SynchronizeVisualEditorStructureRow(VisualEditorNodeViewModel? node)
    {
        if (_isSynchronizingVisualEditorSelection)
        {
            return;
        }

        try
        {
            _isSynchronizingVisualEditorSelection = true;
            SelectedVisualEditorStructureRow = node is null
                ? null
                : VisualEditorStructureRows.FirstOrDefault(row => ReferenceEquals(row.Node, node));
        }
        finally
        {
            _isSynchronizingVisualEditorSelection = false;
        }
    }

    private void SetVisualEditorSourceSelection(XamlElementSnapshot element)
    {
        WorkspaceEditorNavigationFilePath = null;
        VisualEditorSourceSelectionFilePath = ActiveXamlFile?.Path;
        VisualEditorSourceSelectionStart = element.Start;
        VisualEditorSourceSelectionLength = Math.Max(0, element.Length);
        VisualEditorSourceSelectionVersion++;
    }

    private void ClearVisualEditorSourceSelection()
    {
        WorkspaceEditorNavigationFilePath = null;
        VisualEditorSourceSelectionFilePath = null;
        VisualEditorSourceSelectionStart = 0;
        VisualEditorSourceSelectionLength = 0;
        VisualEditorSourceSelectionVersion++;
    }

    private ControlEditorDescriptor ResolveEditorDescriptor(XamlElementSnapshot element)
    {
        return _visualEditorRegistry.Resolve(ResolveControlType(element.TypeName) ?? typeof(Control));
    }

    private void RefreshVisualEditorPropertyOptions(string propertyName)
    {
        var property = VisualEditorAvailableProperties.FirstOrDefault(available =>
            string.Equals(available.Name, propertyName, StringComparison.Ordinal))?.Property;
        RefreshVisualEditorPropertyOptions(property);
    }

    private void RefreshVisualEditorPropertyOptions(ControlEditorProperty? property)
    {
        var options = property is null
            ? Array.Empty<string>()
            : GetSuggestedValues(property)
                .Concat(GetCompatibleResourceReferenceValues(property))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        VisualEditorPropertyOptions = new ObservableCollection<string>(options);
        SelectedVisualEditorPropertyOption = options.FirstOrDefault(option =>
            string.Equals(option, VisualEditorPropertyValue, StringComparison.Ordinal));
        (OpenVisualEditorPropertyResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private static IEnumerable<string> GetSuggestedValues(ControlEditorProperty property)
    {
        return property.ValueKind switch
        {
            ControlEditorValueKind.Boolean => new[] { "True", "False" },
            ControlEditorValueKind.Brush => new[]
            {
                "Transparent",
                "Black",
                "White",
                "Red",
                "Green",
                "Blue",
                "#0078D4",
                "#1F2937",
                "#F8FAFC"
            },
            ControlEditorValueKind.Thickness => new[] { "0", "4", "8", "12", "16", "0,8", "8,0", "4,8,4,8" },
            ControlEditorValueKind.Enum => GetEnumSuggestedValues(property.PropertyName),
            _ => Array.Empty<string>()
        };
    }

    private IEnumerable<string> GetCompatibleResourceReferenceValues(ControlEditorProperty property)
    {
        foreach (var resource in ThemeResources.Where(resource => IsCompatibleResource(property, resource)))
        {
            if (string.Equals(property.PropertyName, "Theme", StringComparison.Ordinal))
            {
                yield return $"{{StaticResource {resource.Key}}}";
                continue;
            }

            if (property.ValueKind == ControlEditorValueKind.Brush)
            {
                yield return $"{{DynamicResource {resource.Key}}}";
            }

            yield return $"{{StaticResource {resource.Key}}}";
        }
    }

    private bool IsCompatibleResource(ControlEditorProperty property, ThemeResourceViewModel resource)
    {
        if (string.Equals(property.PropertyName, "Theme", StringComparison.Ordinal))
        {
            return resource.ResourceType == "ControlTheme" &&
                   TryGetSelectedControlThemeTargetType(out var selectedTargetType) &&
                   string.Equals(resource.TargetType, selectedTargetType, StringComparison.Ordinal);
        }

        var resourceType = GetLocalName(resource.ResourceType);
        var targetType = GetLocalName(resource.TargetType);
        if (property.ValueType is { } valueType &&
            (string.Equals(resourceType, valueType.Name, StringComparison.Ordinal) ||
             string.Equals(targetType, valueType.Name, StringComparison.Ordinal)))
        {
            return true;
        }

        if (IsTemplateProperty(property) && IsTemplateResourceType(resourceType))
        {
            return true;
        }

        if (IsCollectionProperty(property) && IsCollectionResourceType(resourceType, resource.Key))
        {
            return true;
        }

        if (IsDataProperty(property) && IsDataResourceType(resourceType, resource.Key))
        {
            return true;
        }

        return property.ValueKind switch
        {
            ControlEditorValueKind.Brush =>
                resourceType.EndsWith("Brush", StringComparison.Ordinal) ||
                resourceType.EndsWith("Color", StringComparison.Ordinal) ||
                resource.Key.Contains("Brush", StringComparison.OrdinalIgnoreCase) ||
                resource.Key.Contains("Color", StringComparison.OrdinalIgnoreCase),
            ControlEditorValueKind.Thickness =>
                resourceType is "Thickness" or "CornerRadius" ||
                resource.Key.Contains("Padding", StringComparison.OrdinalIgnoreCase) ||
                resource.Key.Contains("Margin", StringComparison.OrdinalIgnoreCase) ||
                resource.Key.Contains("Radius", StringComparison.OrdinalIgnoreCase),
            ControlEditorValueKind.Number =>
                resourceType is "Double" or "Single" or "Int32" or "x:Double" or "sys:Double",
            ControlEditorValueKind.String =>
                resourceType is "String" or "x:String" or "sys:String",
            ControlEditorValueKind.Content =>
                resourceType is "String" or "x:String" or "sys:String" ||
                IsDataResourceType(resourceType, resource.Key),
            ControlEditorValueKind.Collection =>
                IsCollectionResourceType(resourceType, resource.Key),
            _ => false
        };
    }

    private static bool IsTemplateProperty(ControlEditorProperty property)
    {
        var localName = GetLocalPropertyName(property.PropertyName);
        return localName.EndsWith("Template", StringComparison.Ordinal) ||
               property.ValueType is { } valueType &&
               (valueType.Name.EndsWith("Template", StringComparison.Ordinal) ||
                valueType.Name.Contains("DataTemplate", StringComparison.Ordinal) ||
                valueType.Name.Contains("ControlTemplate", StringComparison.Ordinal));
    }

    private static bool IsCollectionProperty(ControlEditorProperty property)
    {
        var localName = GetLocalPropertyName(property.PropertyName);
        return property.ValueKind == ControlEditorValueKind.Collection ||
               localName is "Items" or "ItemsSource" or "Children" or "Columns" or "Rows" ||
               localName.EndsWith("Items", StringComparison.Ordinal) ||
               property.ValueType is { } valueType &&
               valueType != typeof(string) &&
               typeof(System.Collections.IEnumerable).IsAssignableFrom(valueType);
    }

    private static bool IsDataProperty(ControlEditorProperty property)
    {
        var localName = GetLocalPropertyName(property.PropertyName);
        return localName is "DataContext" or "SelectedItem" or "SelectedValue" or "CommandParameter" or "Tag" ||
               localName.EndsWith("Data", StringComparison.Ordinal) ||
               property.ValueType == typeof(object);
    }

    private static bool IsTemplateResourceType(string resourceType)
    {
        return resourceType.EndsWith("Template", StringComparison.Ordinal) ||
               resourceType is "DataTemplate" or "ControlTemplate" or "ItemsPanelTemplate" or "TreeDataTemplate" or "FuncDataTemplate";
    }

    private static bool IsCollectionResourceType(string resourceType, string key)
    {
        return resourceType.EndsWith("Collection", StringComparison.Ordinal) ||
               resourceType.EndsWith("List", StringComparison.Ordinal) ||
               resourceType is "Array" or "x:Array" or "sys:Array" ||
               key.Contains("Items", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("Collection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataResourceType(string resourceType, string key)
    {
        if (IsTemplateResourceType(resourceType) ||
            resourceType is "ControlTheme" or "Style" or "SolidColorBrush" or "LinearGradientBrush" or "RadialGradientBrush" or "Color" or "Thickness" or "CornerRadius")
        {
            return false;
        }

        return resourceType is "Object" or "x:Object" or "sys:Object" ||
               resourceType.EndsWith("Model", StringComparison.Ordinal) ||
               resourceType.EndsWith("ViewModel", StringComparison.Ordinal) ||
               resourceType.EndsWith("Data", StringComparison.Ordinal) ||
               key.Contains("Data", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("Model", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLocalPropertyName(string propertyName)
    {
        var index = propertyName.LastIndexOf('.');
        return index < 0 ? propertyName : propertyName[(index + 1)..];
    }

    private static IEnumerable<string> GetEnumSuggestedValues(string propertyName)
    {
        return propertyName switch
        {
            "HorizontalAlignment" => new[] { "Stretch", "Left", "Center", "Right" },
            "VerticalAlignment" => new[] { "Stretch", "Top", "Center", "Bottom" },
            "TextWrapping" => new[] { "NoWrap", "Wrap", "WrapWithOverflow" },
            "FontWeight" => new[] { "Normal", "SemiBold", "Bold", "Light" },
            _ => Array.Empty<string>()
        };
    }

    private static Type? ResolveControlType(string xamlTypeName)
    {
        var localName = GetLocalName(xamlTypeName);
        return GetAvaloniaControlTypes()
            .FirstOrDefault(type =>
                typeof(Control).IsAssignableFrom(type) &&
                string.Equals(type.Name, localName, StringComparison.Ordinal));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Visual editor control resolution is dynamic by design for designer metadata.")]
    private static IEnumerable<Type> GetAvaloniaControlTypes()
    {
        try
        {
            return typeof(Control).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type is not null)!;
        }
    }

    private static string GetLocalName(string typeName)
    {
        var index = typeName.IndexOf(':', StringComparison.Ordinal);
        return index < 0 ? typeName : typeName[(index + 1)..];
    }

    private bool CanOpenVisualEditorPropertyResource()
    {
        return TryGetResourceReferenceKey(VisualEditorPropertyValue, out var key) &&
               ThemeResources.Any(resource => string.Equals(resource.Key, key, StringComparison.Ordinal));
    }

    private void OpenVisualEditorPropertyResource()
    {
        if (!TryGetResourceReferenceKey(VisualEditorPropertyValue, out var key))
        {
            VisualEditorStatus = "Selected property value is not a resource reference.";
            return;
        }

        SelectedThemeResource = ThemeResources.FirstOrDefault(resource =>
            string.Equals(resource.Key, key, StringComparison.Ordinal));
        if (SelectedThemeResource is null)
        {
            VisualEditorStatus = $"Resource '{key}' was not found.";
            return;
        }

        OpenSelectedThemeResource();
    }

    private static bool TryGetResourceReferenceKey(
        string? value,
        [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!ResourceReferenceParser.TryGetExactKey(value, out var referenceKey) ||
            string.IsNullOrWhiteSpace(referenceKey))
        {
            return false;
        }

        key = referenceKey;
        return true;
    }

    private void ApplyVisualEditorProperty()
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(VisualEditorPropertyName))
        {
            VisualEditorStatus = "Property name is required.";
            return;
        }

        ApplyVisualEditorMutation(_visualMutationEngine.SetProperty(
            xamlFile.Text,
            selector,
            VisualEditorPropertyName.Trim(),
            VisualEditorPropertyValue));
    }

    private void RemoveVisualEditorProperty()
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector) ||
            string.IsNullOrWhiteSpace(VisualEditorPropertyName))
        {
            return;
        }

        ApplyVisualEditorMutation(_visualMutationEngine.RemoveProperty(
            xamlFile.Text,
            selector,
            VisualEditorPropertyName.Trim()));
    }

    private void ResetVisualEditorProperty()
    {
        RemoveVisualEditorProperty();
    }

    private void DeleteVisualEditorElement()
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            return;
        }

        ApplyVisualEditorMutation(_visualMutationEngine.RemoveElement(xamlFile.Text, selector));
    }

    private void DuplicateVisualEditorElement()
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector) ||
            !TryGetSelectedVisualEditorElement(out var selected))
        {
            return;
        }

        if (selected.Path.Count == 0)
        {
            VisualEditorStatus = "The root element cannot be duplicated.";
            return;
        }

        var duplicatePath = selected.Path.ToArray();
        duplicatePath[^1]++;
        _visualEditorSelectedSelector = XamlElementSelector.ByPath(duplicatePath);
        ApplyVisualEditorMutation(_visualMutationEngine.DuplicateElement(xamlFile.Text, selector));
    }

    private void MoveVisualEditorElementUp()
    {
        MoveSelectedVisualEditorElement(-1);
    }

    private void MoveVisualEditorElementDown()
    {
        MoveSelectedVisualEditorElement(1);
    }

    private void MoveSelectedVisualEditorElement(int delta)
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector) ||
            !TryGetSelectedVisualEditorElement(out var selected))
        {
            return;
        }

        if (selected.Path.Count == 0)
        {
            VisualEditorStatus = "The root element cannot be reordered.";
            return;
        }

        var index = selected.Path[^1];
        var targetIndex = index + delta;
        var parentPath = selected.Path.Take(selected.Path.Count - 1).ToArray();
        var siblings = _visualEditorDocument?.Elements.Count(element =>
            element.Path.Count == selected.Path.Count &&
            element.Path.Take(parentPath.Length).SequenceEqual(parentPath)) ?? 0;

        if (targetIndex < 0 || targetIndex >= siblings)
        {
            VisualEditorStatus = delta < 0
                ? "The selected element is already first."
                : "The selected element is already last.";
            return;
        }

        var newPath = selected.Path.ToArray();
        newPath[^1] = targetIndex;
        _visualEditorSelectedSelector = XamlElementSelector.ByPath(newPath);
        ApplyVisualEditorMutation(_visualMutationEngine.ReorderElement(xamlFile.Text, selector, targetIndex));
    }

    public bool SelectVisualEditorPreviewControl(Control control, Rect selectionBounds)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (ActiveXamlFile is not { } xamlFile)
        {
            ClearVisualEditor("No XAML document selected.");
            return false;
        }

        var visualNode = _visualTreeSnapshotService.Snapshot(control);
        var selection = _visualSelectionService.SelectVisual(xamlFile.Text, visualNode);
        if (!selection.HasSelection || selection.XamlElement is null)
        {
            VisualEditorStatus = string.Join(Environment.NewLine, selection.Diagnostics);
            return false;
        }

        _visualEditorDocument = selection.Document;
        _visualEditorSelectedSelector = selection.XamlElement.Selector;
        SetVisualEditorPreviewSelectionBounds(selectionBounds);
        RefreshVisualEditingModel();
        VisualEditorStatus = $"Selected {selection.XamlElement.TypeName} from preview.";
        return true;
    }

    public bool TryResolveVisualEditorPreviewControl(
        Control control,
        [NotNullWhen(true)] out XamlDocumentSnapshot? document,
        [NotNullWhen(true)] out XamlElementSnapshot? element,
        out IReadOnlyList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(control);

        document = null;
        element = null;
        diagnostics = Array.Empty<string>();

        if (ActiveXamlFile is not { } xamlFile)
        {
            diagnostics = new[] { "No XAML document selected." };
            return false;
        }

        return TryResolveVisualEditorPreviewControl(
            control,
            xamlFile.Text,
            out document,
            out element,
            out diagnostics);
    }

    public bool SelectVisualEditorPreviewElement(
        XamlDocumentSnapshot document,
        XamlElementSnapshot element,
        Rect selectionBounds,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);

        _visualEditorDocument = document;
        _visualEditorSelectedSelector = element.Selector;
        SetVisualEditorPreviewSelectionBounds(selectionBounds);
        RefreshVisualEditingModel();
        VisualEditorStatus = $"Selected {element.TypeName} {reason}.";
        return true;
    }

    public bool SelectVisualEditorSourceRange(
        string? filePath,
        int selectionStart,
        int selectionLength,
        int caretOffset)
    {
        if (_isApplyingVisualEditorMutation)
        {
            return false;
        }

        if (ActiveXamlFile is not { } xamlFile ||
            !xamlFile.IsXaml ||
            !string.Equals(filePath, xamlFile.Path, StringComparison.Ordinal))
        {
            return false;
        }

        var document = _visualMutationEngine.Analyze(xamlFile.Text);
        var element = FindVisualEditorElementAtSourceRange(
            document,
            selectionStart,
            selectionLength,
            caretOffset);
        if (element is null)
        {
            return false;
        }

        _visualEditorSelectedSelector = element.Selector;
        try
        {
            _suppressVisualEditorSourceSelectionUpdate = true;
            RefreshVisualEditingModel();
        }
        finally
        {
            _suppressVisualEditorSourceSelectionUpdate = false;
        }

        VisualEditorStatus = $"Selected {element.TypeName} from XAML.";
        return true;
    }

    public void UpdateVisualEditorPreviewSelectionBounds(Rect bounds)
    {
        SetVisualEditorPreviewSelectionBounds(bounds);
    }

    public void UpdateVisualEditorPreviewCurrentContainerBounds(Rect? bounds)
    {
        if (bounds is not { } value)
        {
            VisualEditorPreviewCurrentContainerVisible = false;
            return;
        }

        VisualEditorPreviewCurrentContainerLeft = value.X;
        VisualEditorPreviewCurrentContainerTop = value.Y;
        VisualEditorPreviewCurrentContainerWidth = Math.Max(1, value.Width);
        VisualEditorPreviewCurrentContainerHeight = Math.Max(1, value.Height);
        VisualEditorPreviewCurrentContainerVisible = true;
    }

    public void UpdateVisualEditorPreviewDropFeedback(
        Rect? targetBounds,
        Rect? insertionBounds,
        Rect? placeholderBounds = null)
    {
        if (targetBounds is { } target)
        {
            VisualEditorPreviewDropTargetLeft = target.X;
            VisualEditorPreviewDropTargetTop = target.Y;
            VisualEditorPreviewDropTargetWidth = Math.Max(1, target.Width);
            VisualEditorPreviewDropTargetHeight = Math.Max(1, target.Height);
            VisualEditorPreviewDropTargetVisible = true;
        }
        else
        {
            VisualEditorPreviewDropTargetVisible = false;
        }

        if (insertionBounds is { } insertion)
        {
            VisualEditorPreviewInsertionLeft = insertion.X;
            VisualEditorPreviewInsertionTop = insertion.Y;
            VisualEditorPreviewInsertionWidth = Math.Max(1, insertion.Width);
            VisualEditorPreviewInsertionHeight = Math.Max(1, insertion.Height);
            VisualEditorPreviewInsertionVisible = true;
        }
        else
        {
            VisualEditorPreviewInsertionVisible = false;
        }

        if (placeholderBounds is { } placeholder)
        {
            VisualEditorPreviewDropPlaceholderLeft = placeholder.X;
            VisualEditorPreviewDropPlaceholderTop = placeholder.Y;
            VisualEditorPreviewDropPlaceholderWidth = Math.Max(1, placeholder.Width);
            VisualEditorPreviewDropPlaceholderHeight = Math.Max(1, placeholder.Height);
            VisualEditorPreviewDropPlaceholderVisible = true;
        }
        else
        {
            VisualEditorPreviewDropPlaceholderVisible = false;
        }
    }

    public void UpdateVisualEditorPreviewGuides(
        Rect? verticalGuide,
        Rect? horizontalGuide,
        Point? measurementPosition,
        string? measurementText)
    {
        if (verticalGuide is { } vertical)
        {
            VisualEditorPreviewVerticalGuideLeft = vertical.X;
            VisualEditorPreviewVerticalGuideTop = vertical.Y;
            VisualEditorPreviewVerticalGuideHeight = Math.Max(1, vertical.Height);
            VisualEditorPreviewVerticalGuideVisible = true;
        }
        else
        {
            VisualEditorPreviewVerticalGuideVisible = false;
        }

        if (horizontalGuide is { } horizontal)
        {
            VisualEditorPreviewHorizontalGuideLeft = horizontal.X;
            VisualEditorPreviewHorizontalGuideTop = horizontal.Y;
            VisualEditorPreviewHorizontalGuideWidth = Math.Max(1, horizontal.Width);
            VisualEditorPreviewHorizontalGuideVisible = true;
        }
        else
        {
            VisualEditorPreviewHorizontalGuideVisible = false;
        }

        if (measurementPosition is { } position &&
            !string.IsNullOrWhiteSpace(measurementText))
        {
            VisualEditorPreviewMeasurementLeft = position.X;
            VisualEditorPreviewMeasurementTop = position.Y;
            VisualEditorPreviewMeasurementText = measurementText;
            VisualEditorPreviewMeasurementVisible = true;
        }
        else
        {
            VisualEditorPreviewMeasurementVisible = false;
            VisualEditorPreviewMeasurementText = string.Empty;
        }
    }

    public void ClearVisualEditorPreviewDropFeedback()
    {
        VisualEditorPreviewDropTargetVisible = false;
        VisualEditorPreviewInsertionVisible = false;
        VisualEditorPreviewDropPlaceholderVisible = false;
        UpdateVisualEditorPreviewGuides(null, null, null, null);
    }

    public bool SelectAdjacentVisualEditorElement(bool previous)
    {
        var rows = GetCurrentContainerChildRows();
        if (rows.Length == 0)
        {
            rows = VisualEditorStructureRows.ToArray();
        }

        if (rows.Length == 0)
        {
            return false;
        }

        var currentIndex = SelectedVisualEditorStructureRow is { } current
            ? Array.IndexOf(rows, current)
            : -1;
        if (currentIndex < 0 && SelectedVisualEditorNode is not null)
        {
            for (var index = 0; index < rows.Length; index++)
            {
                if (ReferenceEquals(rows[index].Node, SelectedVisualEditorNode))
                {
                    currentIndex = index;
                    break;
                }
            }
        }
        if (currentIndex < 0)
        {
            currentIndex = previous ? rows.Length : -1;
        }

        var nextIndex = previous
            ? (currentIndex - 1 + rows.Length) % rows.Length
            : (currentIndex + 1) % rows.Length;
        var next = rows[nextIndex];

        SelectedVisualEditorNode = next.Node;
        VisualEditorStatus = $"Selected {next.Element.TypeName} from keyboard in {VisualEditorCurrentContainerTitle}.";
        return true;
    }

    public bool SelectVisualEditorParentElement()
    {
        if (!TryGetSelectedVisualEditorElement(out var selected) ||
            selected.Path.Count == 0 ||
            _visualEditorDocument is null)
        {
            return false;
        }

        var parentPath = selected.Path.Take(selected.Path.Count - 1).ToArray();
        var parent = _visualEditorDocument.Elements.FirstOrDefault(element =>
            element.Path.SequenceEqual(parentPath));
        if (parent is null)
        {
            return false;
        }

        _visualEditorSelectedSelector = parent.Selector;
        RefreshVisualEditingModel();
        VisualEditorStatus = $"Selected parent {parent.TypeName}.";
        return true;
    }

    public bool EnterVisualEditorCurrentContainer()
    {
        if (!TryGetSelectedVisualEditorElement(out var selected) ||
            _visualEditorDocument is null)
        {
            return false;
        }

        var selectedIsContainer = IsContainerElement(selected);
        var container = selectedIsContainer
            ? selected
            : FindCurrentContainerForSelection(selected);
        if (container is null ||
            !selectedIsContainer)
        {
            return false;
        }

        SetVisualEditorCurrentContainer(container);
        var firstChild = _visualEditorDocument.Elements
            .Where(element =>
                element.Path.Count == container.Path.Count + 1 &&
                element.Path.Take(container.Path.Count).SequenceEqual(container.Path))
            .OrderBy(static element => element.Path[^1])
            .FirstOrDefault();
        if (firstChild is not null)
        {
            _visualEditorSelectedSelector = firstChild.Selector;
            RefreshVisualEditingModel();
        }

        VisualEditorStatus = $"Entered {FormatVisualEditorElementTitle(container)}.";
        return true;
    }

    public bool IsVisualEditorCandidateInCurrentContainer(XamlElementSnapshot element)
    {
        if (_visualEditorCurrentContainerSelector is null)
        {
            return false;
        }

        var container = FindElement(_visualEditorDocument, _visualEditorCurrentContainerSelector);
        return container is not null && HasParentPath(element, container.Path);
    }

    public bool IsVisualEditorCandidateInCurrentContainerSubtree(XamlElementSnapshot element)
    {
        if (_visualEditorCurrentContainerSelector is null)
        {
            return true;
        }

        var container = FindElement(_visualEditorDocument, _visualEditorCurrentContainerSelector);
        return container is null || IsSameOrDescendantPath(element.Path, container.Path);
    }

    public XamlElementSnapshot? GetVisualEditorCurrentContainerElement()
    {
        return FindElement(_visualEditorDocument, _visualEditorCurrentContainerSelector);
    }

    public bool MoveVisualEditorSelectionBy(double deltaX, double deltaY)
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector) ||
            !TryGetSelectedVisualEditorElement(out var selected))
        {
            return false;
        }

        if (Math.Abs(deltaX) < 0.1 && Math.Abs(deltaY) < 0.1)
        {
            return false;
        }

        var requests = UsesCanvasPositioning(selected)
            ? CreateCanvasMoveRequests(selected, selector, deltaX, deltaY)
            : CreateMarginMoveRequests(selected, selector, deltaX, deltaY);
        var requestList = requests.ToArray();
        var result = _visualMutationEngine.Batch(xamlFile.Text, requestList);
        ApplyVisualEditorMutation(result);

        if (result.Success)
        {
            RecordAnimationKeyFrameFromVisualEdit(requestList, applyTimeline: true);
            VisualEditorStatus = UsesCanvasPositioning(selected)
                ? "Moved selection using Canvas.Left and Canvas.Top."
                : "Moved selection using Margin.";
        }

        return result.Success;
    }

    public bool ResizeVisualEditorSelectionBy(double deltaWidth, double deltaHeight)
    {
        var start = new Rect(
            VisualEditorPreviewSelectionLeft,
            VisualEditorPreviewSelectionTop,
            VisualEditorPreviewSelectionWidth,
            VisualEditorPreviewSelectionHeight);
        return ResizeVisualEditorSelectionToBounds(
            start,
            new Rect(
                start.Position,
                new Size(
                    Math.Max(1, start.Width + deltaWidth),
                    Math.Max(1, start.Height + deltaHeight))));
    }

    public bool ResizeVisualEditorSelectionToBounds(Rect oldBounds, Rect newBounds)
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector) ||
            !TryGetSelectedVisualEditorElement(out var selected))
        {
            return false;
        }

        var deltaX = newBounds.X - oldBounds.X;
        var deltaY = newBounds.Y - oldBounds.Y;
        var width = Math.Max(1, newBounds.Width);
        var height = Math.Max(1, newBounds.Height);
        if (Math.Abs(deltaX) < 0.1 &&
            Math.Abs(deltaY) < 0.1 &&
            Math.Abs(width - oldBounds.Width) < 0.1 &&
            Math.Abs(height - oldBounds.Height) < 0.1)
        {
            return false;
        }

        var requests = new List<XamlMutationRequest>();
        if (Math.Abs(deltaX) >= 0.1 || Math.Abs(deltaY) >= 0.1)
        {
            requests.AddRange(UsesCanvasPositioning(selected)
                ? CreateCanvasMoveRequests(selected, selector, deltaX, deltaY)
                : CreateMarginMoveRequests(selected, selector, deltaX, deltaY));
        }

        requests.AddRange(new[]
        {
            new XamlMutationRequest(
                XamlMutationKind.SetProperty,
                selector,
                PropertyName: "Width",
                Value: FormatDesignerDouble(width)),
            new XamlMutationRequest(
                XamlMutationKind.SetProperty,
                selector,
                PropertyName: "Height",
                Value: FormatDesignerDouble(height))
        });

        var result = _visualMutationEngine.Batch(
            xamlFile.Text,
            requests);

        ApplyVisualEditorMutation(result);

        if (result.Success)
        {
            RecordAnimationKeyFrameFromVisualEdit(requests, applyTimeline: true);
            VisualEditorStatus = "Resized selection using Width and Height.";
        }

        return result.Success;
    }

    public bool MoveVisualEditorSelectionNearPreviewControl(
        Control targetControl,
        bool after,
        XamlElementSnapshot? resolvedTarget = null)
    {
        ArgumentNullException.ThrowIfNull(targetControl);

        if (!TryGetVisualEditingContext(out var xamlFile, out var selector) ||
            !TryGetSelectedVisualEditorElement(out var selected))
        {
            return false;
        }

        var target = ResolvePreviewTargetElement(
            xamlFile.Text,
            targetControl,
            resolvedTarget,
            out var diagnostics);
        if (target is null)
        {
            VisualEditorStatus = diagnostics.Count > 0
                ? string.Join(Environment.NewLine, diagnostics)
                : "The drop target could not be mapped to XAML.";
            return false;
        }

        if (Matches(target, selector) ||
            IsDescendantOf(target, selected) ||
            target.Path.Count == 0)
        {
            return false;
        }

        var targetParentPath = target.Path.Take(target.Path.Count - 1).ToArray();
        var selectedParentPath = selected.Path.Take(Math.Max(0, selected.Path.Count - 1)).ToArray();
        var targetIndex = target.Path[^1] + (after ? 1 : 0);
        if (selectedParentPath.SequenceEqual(targetParentPath) &&
            selected.Path.Count > 0 &&
            selected.Path[^1] < targetIndex)
        {
            targetIndex--;
        }

        if (selectedParentPath.SequenceEqual(targetParentPath) &&
            selected.Path.Count > 0 &&
            selected.Path[^1] == targetIndex)
        {
            return false;
        }

        var targetParentSelector = XamlElementSelector.ByPath(targetParentPath);
        var selectedPathAfterMove = selectedParentPath.SequenceEqual(targetParentPath)
            ? targetParentPath.Concat(new[] { targetIndex }).ToArray()
            : AdjustPathAfterRemoval(targetParentPath, selected.Path).Concat(new[] { targetIndex }).ToArray();
        var result = selectedParentPath.SequenceEqual(targetParentPath)
            ? _visualMutationEngine.ReorderElement(xamlFile.Text, selector, targetIndex)
            : _visualMutationEngine.MoveElement(xamlFile.Text, selector, targetParentSelector, targetIndex);
        if (result.Success)
        {
            _visualEditorSelectedSelector = XamlElementSelector.ByPath(selectedPathAfterMove);
        }

        ApplyVisualEditorMutation(result);

        if (result.Success)
        {
            VisualEditorStatus = after
                ? $"Moved selection after {target.TypeName}."
                : $"Moved selection before {target.TypeName}.";
        }

        return result.Success;
    }

    public bool MoveVisualEditorSelectionIntoPreviewControl(
        Control targetParent,
        XamlElementSnapshot? resolvedTarget = null)
    {
        ArgumentNullException.ThrowIfNull(targetParent);

        if (!TryGetVisualEditingContext(out var xamlFile, out var selector) ||
            !TryGetSelectedVisualEditorElement(out var selected))
        {
            return false;
        }

        if (targetParent is Decorator { Child: not null } ||
            targetParent is ContentControl { Content: not null })
        {
            VisualEditorStatus = "Drop target already has content.";
            return false;
        }

        if (targetParent is not Panel and not Decorator and not ContentControl)
        {
            VisualEditorStatus = "Drop target must be a layout container.";
            return false;
        }

        var target = ResolvePreviewTargetElement(
            xamlFile.Text,
            targetParent,
            resolvedTarget,
            out var diagnostics);
        if (target is null)
        {
            VisualEditorStatus = diagnostics.Count > 0
                ? string.Join(Environment.NewLine, diagnostics)
                : "The drop target could not be mapped to XAML.";
            return false;
        }

        if (Matches(target, selector) ||
            IsDescendantOf(target, selected))
        {
            VisualEditorStatus = "Cannot move an element into itself.";
            return false;
        }

        if (HasSameParent(selected, target))
        {
            return false;
        }

        var selectedPathAfterMove = AdjustPathAfterRemoval(target.Path, selected.Path)
            .Concat(new[] { target.ChildElementCount })
            .ToArray();
        var result = _visualMutationEngine.MoveElement(xamlFile.Text, selector, target.Selector);
        if (result.Success)
        {
            _visualEditorSelectedSelector = XamlElementSelector.ByPath(selectedPathAfterMove);
        }

        ApplyVisualEditorMutation(result);

        if (result.Success)
        {
            VisualEditorStatus = $"Moved selection into {target.TypeName}.";
        }

        return result.Success;
    }

    public bool InsertToolboxItemIntoPreviewControl(
        ToolboxItemDescriptor item,
        Control targetParent,
        int? childIndex = null,
        Point? canvasPosition = null,
        XamlElementSnapshot? resolvedTarget = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(targetParent);

        if (ActiveXamlFile is not { } xamlFile)
        {
            ClearVisualEditor("No XAML document selected.");
            return false;
        }

        var target = resolvedTarget is not null
            ? FindElement(_visualMutationEngine.Analyze(xamlFile.Text), XamlElementSelector.ByPath(resolvedTarget.Path.ToArray()))
            : null;

        if (target is null)
        {
            var targetVisualNode = _visualTreeSnapshotService.Snapshot(targetParent);
            var targetSelection = _visualSelectionService.SelectVisual(xamlFile.Text, targetVisualNode);
            if (!targetSelection.HasSelection || targetSelection.XamlElement is null)
            {
                VisualEditorStatus = string.Join(Environment.NewLine, targetSelection.Diagnostics);
                return false;
            }

            target = targetSelection.XamlElement;
        }

        var insertionProperties = CreateToolboxInsertionProperties(targetParent, canvasPosition);
        var insertionIndex = childIndex is { } index
            ? Math.Clamp(index, 0, target.ChildElementCount)
            : target.ChildElementCount;
        var selectedPath = target.Path.Concat(new[] { insertionIndex }).ToArray();
        _visualEditorSelectedSelector = XamlElementSelector.ByPath(selectedPath);
        var targetSelector = XamlElementSelector.ByPath(target.Path.ToArray());

        var insertion = _visualToolboxInsertion.Insert(
            xamlFile.Text,
            targetSelector,
            item,
            childIndex is null ? null : insertionIndex,
            insertionProperties);
        ApplyVisualEditorMutation(insertion.Mutation);

        if (insertion.Success)
        {
            VisualEditorStatus = canvasPosition is not null
                ? $"Inserted {item.TypeName} at canvas position."
                : $"Inserted {item.TypeName} into {target.TypeName}.";
        }

        return insertion.Success;
    }

    public bool CanInsertToolboxItemIntoStructure(
        ToolboxItemDescriptor item,
        XamlElementSnapshot target,
        VisualEditorStructureDropPosition position)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(target);

        if (ActiveXamlFile is null)
        {
            return false;
        }

        return ResolveStructureInsertionTarget(
            _visualMutationEngine.Analyze(ActiveXamlFile.Text),
            target,
            position,
            out _,
            out _,
            setStatus: false);
    }

    public bool InsertToolboxItemIntoStructure(
        ToolboxItemDescriptor item,
        XamlElementSnapshot target,
        VisualEditorStructureDropPosition position)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(target);

        if (ActiveXamlFile is not { } xamlFile)
        {
            ClearVisualEditor("No XAML document selected.");
            return false;
        }

        var document = _visualMutationEngine.Analyze(xamlFile.Text);
        if (!ResolveStructureInsertionTarget(
                document,
                target,
                position,
                out var parent,
                out var insertionIndex,
                setStatus: true))
        {
            return false;
        }

        var selectedPath = parent.Path.Concat(new[] { insertionIndex }).ToArray();
        _visualEditorSelectedSelector = XamlElementSelector.ByPath(selectedPath);
        var insertion = _visualToolboxInsertion.Insert(
            xamlFile.Text,
            XamlElementSelector.ByPath(parent.Path.ToArray()),
            item,
            insertionIndex);

        ApplyVisualEditorMutation(insertion.Mutation);

        if (insertion.Success)
        {
            VisualEditorStatus = position switch
            {
                VisualEditorStructureDropPosition.Before => $"Inserted {item.TypeName} before {target.TypeName}.",
                VisualEditorStructureDropPosition.After => $"Inserted {item.TypeName} after {target.TypeName}.",
                _ => $"Inserted {item.TypeName} into {parent.TypeName}."
            };
        }

        return insertion.Success;
    }

    public bool CanMoveVisualEditorElementInStructure(
        XamlElementSnapshot source,
        XamlElementSnapshot target,
        VisualEditorStructureDropPosition position)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (ActiveXamlFile is not { } xamlFile)
        {
            return false;
        }

        var document = _visualMutationEngine.Analyze(xamlFile.Text);
        var currentSource = FindElement(document, XamlElementSelector.ByPath(source.Path.ToArray()));
        var currentTarget = FindElement(document, XamlElementSelector.ByPath(target.Path.ToArray()));
        if (currentSource is null || currentTarget is null)
        {
            return false;
        }

        return ResolveStructureMoveTarget(
            document,
            currentSource,
            currentTarget,
            position,
            out _,
            out _,
            out _,
            setStatus: false);
    }

    public bool MoveVisualEditorElementInStructure(
        XamlElementSnapshot source,
        XamlElementSnapshot target,
        VisualEditorStructureDropPosition position)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (ActiveXamlFile is not { } xamlFile)
        {
            ClearVisualEditor("No XAML document selected.");
            return false;
        }

        var document = _visualMutationEngine.Analyze(xamlFile.Text);
        var currentSource = FindElement(document, XamlElementSelector.ByPath(source.Path.ToArray()));
        var currentTarget = FindElement(document, XamlElementSelector.ByPath(target.Path.ToArray()));
        if (currentSource is null || currentTarget is null)
        {
            VisualEditorStatus = "The structure drop source or target is no longer available.";
            return false;
        }

        if (!ResolveStructureMoveTarget(
                document,
                currentSource,
                currentTarget,
                position,
                out var targetParent,
                out var targetIndex,
                out var selectedPathAfterMove,
                setStatus: true))
        {
            return false;
        }

        var sourceSelector = XamlElementSelector.ByPath(currentSource.Path.ToArray());
        var sourceParentPath = currentSource.Path.Take(currentSource.Path.Count - 1).ToArray();
        var targetParentSelector = XamlElementSelector.ByPath(targetParent.Path.ToArray());
        var result = sourceParentPath.SequenceEqual(targetParent.Path)
            ? _visualMutationEngine.ReorderElement(xamlFile.Text, sourceSelector, targetIndex)
            : _visualMutationEngine.MoveElement(xamlFile.Text, sourceSelector, targetParentSelector, targetIndex);

        if (result.Success)
        {
            _visualEditorSelectedSelector = XamlElementSelector.ByPath(selectedPathAfterMove.ToArray());
        }

        ApplyVisualEditorMutation(result);

        if (result.Success)
        {
            VisualEditorStatus = position switch
            {
                VisualEditorStructureDropPosition.Before => $"Moved {currentSource.TypeName} before {currentTarget.TypeName}.",
                VisualEditorStructureDropPosition.After => $"Moved {currentSource.TypeName} after {currentTarget.TypeName}.",
                _ => $"Moved {currentSource.TypeName} into {currentTarget.TypeName}."
            };
        }

        return result.Success;
    }

    private bool ResolveStructureInsertionTarget(
        XamlDocumentSnapshot document,
        XamlElementSnapshot target,
        VisualEditorStructureDropPosition position,
        [NotNullWhen(true)] out XamlElementSnapshot? parent,
        out int insertionIndex,
        bool setStatus)
    {
        parent = null;
        insertionIndex = 0;

        var currentTarget = FindElement(document, XamlElementSelector.ByPath(target.Path.ToArray()));
        if (currentTarget is null)
        {
            if (setStatus)
            {
                VisualEditorStatus = "The structure drop target is no longer available.";
            }

            return false;
        }

        if (currentTarget.Path.Count == 0)
        {
            parent = currentTarget;
            insertionIndex = position switch
            {
                VisualEditorStructureDropPosition.Before => 0,
                _ => currentTarget.ChildElementCount
            };
            return true;
        }

        if (position == VisualEditorStructureDropPosition.Inside)
        {
            if (!IsContainerElement(currentTarget))
            {
                if (setStatus)
                {
                    VisualEditorStatus = "Drop target must be a layout container.";
                }

                return false;
            }

            parent = currentTarget;
            insertionIndex = currentTarget.ChildElementCount;
            return true;
        }

        var parentPath = currentTarget.Path.Take(currentTarget.Path.Count - 1).ToArray();
        parent = FindElement(document, XamlElementSelector.ByPath(parentPath));
        if (parent is null)
        {
            if (setStatus)
            {
                VisualEditorStatus = "The structure drop parent is no longer available.";
            }

            return false;
        }

        insertionIndex = currentTarget.Path[^1] + (position == VisualEditorStructureDropPosition.After ? 1 : 0);
        insertionIndex = Math.Clamp(insertionIndex, 0, parent.ChildElementCount);
        return true;
    }

    private bool ResolveStructureMoveTarget(
        XamlDocumentSnapshot document,
        XamlElementSnapshot source,
        XamlElementSnapshot target,
        VisualEditorStructureDropPosition position,
        [NotNullWhen(true)] out XamlElementSnapshot? targetParent,
        out int targetIndex,
        out IReadOnlyList<int> selectedPathAfterMove,
        bool setStatus)
    {
        targetParent = null;
        targetIndex = 0;
        selectedPathAfterMove = Array.Empty<int>();

        if (source.Path.Count == 0)
        {
            if (setStatus)
            {
                VisualEditorStatus = "The root element cannot be moved.";
            }

            return false;
        }

        if (source.Path.SequenceEqual(target.Path) ||
            IsDescendantOf(target, source))
        {
            if (setStatus)
            {
                VisualEditorStatus = "Cannot move an element into itself.";
            }

            return false;
        }

        if (document.Root is null)
        {
            if (setStatus)
            {
                VisualEditorStatus = "No XAML document selected.";
            }

            return false;
        }

        if (position == VisualEditorStructureDropPosition.Inside)
        {
            if (!IsContainerElement(target))
            {
                if (setStatus)
                {
                    VisualEditorStatus = "Drop target must be a layout container.";
                }

                return false;
            }

            if (HasSameParent(source, target))
            {
                return false;
            }

            targetParent = target;
            targetIndex = target.ChildElementCount;
            selectedPathAfterMove = AdjustPathAfterRemoval(target.Path, source.Path)
                .Concat(new[] { targetIndex })
                .ToArray();
            return true;
        }

        if (target.Path.Count == 0)
        {
            if (setStatus)
            {
                VisualEditorStatus = "The root element cannot be used as a sibling drop target.";
            }

            return false;
        }

        var targetParentPath = target.Path.Take(target.Path.Count - 1).ToArray();
        var sourceParentPath = source.Path.Take(source.Path.Count - 1).ToArray();
        targetParent = FindElement(document, XamlElementSelector.ByPath(targetParentPath));
        if (targetParent is null)
        {
            if (setStatus)
            {
                VisualEditorStatus = "The structure drop parent is no longer available.";
            }

            return false;
        }

        targetIndex = target.Path[^1] + (position == VisualEditorStructureDropPosition.After ? 1 : 0);
        if (sourceParentPath.SequenceEqual(targetParentPath) &&
            source.Path[^1] < targetIndex)
        {
            targetIndex--;
        }

        if (sourceParentPath.SequenceEqual(targetParentPath) &&
            source.Path[^1] == targetIndex)
        {
            return false;
        }

        selectedPathAfterMove = sourceParentPath.SequenceEqual(targetParentPath)
            ? targetParentPath.Concat(new[] { targetIndex }).ToArray()
            : AdjustPathAfterRemoval(targetParentPath, source.Path).Concat(new[] { targetIndex }).ToArray();
        targetIndex = Math.Clamp(targetIndex, 0, targetParent.ChildElementCount);
        return true;
    }

    private XamlElementSnapshot? ResolvePreviewTargetElement(
        string xaml,
        Control targetControl,
        XamlElementSnapshot? resolvedTarget,
        out IReadOnlyList<string> diagnostics)
    {
        if (resolvedTarget is not null)
        {
            var document = _visualMutationEngine.Analyze(xaml);
            if (FindElement(document, XamlElementSelector.ByPath(resolvedTarget.Path.ToArray())) is { } target)
            {
                diagnostics = Array.Empty<string>();
                return target;
            }
        }

        var targetVisualNode = _visualTreeSnapshotService.Snapshot(targetControl);
        var targetSelection = _visualSelectionService.SelectVisual(xaml, targetVisualNode);
        diagnostics = targetSelection.Diagnostics;
        return targetSelection.HasSelection
            ? targetSelection.XamlElement
            : null;
    }

    private void SetVisualEditorPreviewSelectionBounds(Rect bounds)
    {
        VisualEditorPreviewSelectionLeft = bounds.X;
        VisualEditorPreviewSelectionTop = bounds.Y;
        VisualEditorPreviewSelectionWidth = Math.Max(1, bounds.Width);
        VisualEditorPreviewSelectionHeight = Math.Max(1, bounds.Height);
        VisualEditorPreviewSelectionVisible = true;
    }

    private void NotifyVisualEditorPreviewSelectionGeometryChanged()
    {
        OnPropertyChanged(nameof(VisualEditorPreviewNorthWestThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewNorthWestThumbTop));
        OnPropertyChanged(nameof(VisualEditorPreviewNorthThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewNorthThumbTop));
        OnPropertyChanged(nameof(VisualEditorPreviewNorthEastThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewNorthEastThumbTop));
        OnPropertyChanged(nameof(VisualEditorPreviewWestThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewWestThumbTop));
        OnPropertyChanged(nameof(VisualEditorPreviewEastThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewEastThumbTop));
        OnPropertyChanged(nameof(VisualEditorPreviewSouthWestThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewSouthWestThumbTop));
        OnPropertyChanged(nameof(VisualEditorPreviewSouthThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewSouthThumbTop));
        OnPropertyChanged(nameof(VisualEditorPreviewSouthEastThumbLeft));
        OnPropertyChanged(nameof(VisualEditorPreviewSouthEastThumbTop));
    }

    private IEnumerable<XamlMutationRequest> CreateCanvasMoveRequests(
        XamlElementSnapshot selected,
        XamlElementSelector selector,
        double deltaX,
        double deltaY)
    {
        var left = GetDoubleAttribute(selected, "Canvas.Left", 0) + deltaX;
        var top = GetDoubleAttribute(selected, "Canvas.Top", 0) + deltaY;

        return new[]
        {
            new XamlMutationRequest(
                XamlMutationKind.SetProperty,
                selector,
                PropertyName: "Canvas.Left",
                Value: FormatDesignerDouble(left)),
            new XamlMutationRequest(
                XamlMutationKind.SetProperty,
                selector,
                PropertyName: "Canvas.Top",
                Value: FormatDesignerDouble(top))
        };
    }

    private static IEnumerable<XamlMutationRequest> CreateMarginMoveRequests(
        XamlElementSnapshot selected,
        XamlElementSelector selector,
        double deltaX,
        double deltaY)
    {
        var thickness = ParseThicknessAttribute(selected, "Margin");
        thickness = thickness with
        {
            Left = thickness.Left + deltaX,
            Top = thickness.Top + deltaY
        };

        return new[]
        {
            new XamlMutationRequest(
                XamlMutationKind.SetProperty,
                selector,
                PropertyName: "Margin",
                Value: FormatDesignerThickness(thickness))
        };
    }

    private bool UsesCanvasPositioning(XamlElementSnapshot selected)
    {
        if (selected.Attributes.ContainsKey("Canvas.Left") ||
            selected.Attributes.ContainsKey("Canvas.Top"))
        {
            return true;
        }

        if (_visualEditorDocument is null || selected.Path.Count == 0)
        {
            return false;
        }

        var parentPath = selected.Path.Take(selected.Path.Count - 1);
        var parent = _visualEditorDocument.Elements.FirstOrDefault(element =>
            element.Path.SequenceEqual(parentPath));

        return parent is not null &&
               string.Equals(GetLocalName(parent.TypeName), "Canvas", StringComparison.Ordinal);
    }

    private static bool IsDescendantOf(XamlElementSnapshot candidate, XamlElementSnapshot ancestor)
    {
        return candidate.Path.Count > ancestor.Path.Count &&
               candidate.Path.Take(ancestor.Path.Count).SequenceEqual(ancestor.Path);
    }

    private static IReadOnlyList<int> AdjustPathAfterRemoval(
        IReadOnlyList<int> path,
        IReadOnlyList<int> removedPath)
    {
        if (path.Count == 0 || removedPath.Count == 0)
        {
            return path.ToArray();
        }

        var adjusted = path.ToArray();
        var removedIndexDepth = removedPath.Count - 1;
        if (path.Count <= removedIndexDepth ||
            !path.Take(removedIndexDepth).SequenceEqual(removedPath.Take(removedIndexDepth)))
        {
            return adjusted;
        }

        if (removedPath[removedIndexDepth] < path[removedIndexDepth])
        {
            adjusted[removedIndexDepth]--;
        }

        return adjusted;
    }

    private static XamlElementSnapshot? FindVisualEditorElementAtSourceRange(
        XamlDocumentSnapshot document,
        int selectionStart,
        int selectionLength,
        int caretOffset)
    {
        if (document.Elements.Count == 0)
        {
            return null;
        }

        var documentLength = document.Text.Length;
        var start = Math.Clamp(selectionStart, 0, documentLength);
        var length = Math.Clamp(selectionLength, 0, documentLength - start);
        var offset = length > 0
            ? start
            : Math.Clamp(caretOffset, 0, documentLength);
        var end = length > 0
            ? start + length
            : offset;

        return document.Elements
            .Where(element => ContainsSourceRange(element, offset, end))
            .OrderByDescending(static element => element.Path.Count)
            .ThenBy(static element => element.Length)
            .FirstOrDefault();
    }

    private static bool ContainsSourceRange(XamlElementSnapshot element, int start, int end)
    {
        var elementStart = element.Start;
        var elementEnd = element.Start + element.Length;
        return start >= elementStart &&
               start <= elementEnd &&
               end >= elementStart &&
               end <= elementEnd;
    }

    private static bool HasSameParent(XamlElementSnapshot selected, XamlElementSnapshot target)
    {
        return selected.Path.Count > 0 &&
               selected.Path.Take(selected.Path.Count - 1).SequenceEqual(target.Path);
    }

    private static double GetDoubleAttribute(
        XamlElementSnapshot selected,
        string attributeName,
        double fallback)
    {
        return selected.Attributes.TryGetValue(attributeName, out var value) &&
               double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) &&
               !double.IsNaN(result) &&
               !double.IsInfinity(result)
            ? result
            : fallback;
    }

    private static DesignerThickness ParseThicknessAttribute(
        XamlElementSnapshot selected,
        string attributeName)
    {
        if (!selected.Attributes.TryGetValue(attributeName, out var value))
        {
            return default;
        }

        var values = value
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0)
            .ToArray();

        return values.Length switch
        {
            1 => new DesignerThickness(values[0], values[0], values[0], values[0]),
            2 => new DesignerThickness(values[0], values[1], values[0], values[1]),
            4 => new DesignerThickness(values[0], values[1], values[2], values[3]),
            _ => default
        };
    }

    private static string FormatDesignerThickness(DesignerThickness thickness)
    {
        return string.Join(
            ",",
            FormatDesignerDouble(thickness.Left),
            FormatDesignerDouble(thickness.Top),
            FormatDesignerDouble(thickness.Right),
            FormatDesignerDouble(thickness.Bottom));
    }

    private static string FormatDesignerDouble(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static DevToolsOptions CreateDiagnosticsDevToolsOptions(IDevToolsPropertyEditHandler propertyEditHandler)
    {
        return new DevToolsOptions
        {
            ShowMenu = false,
            ShowResourcesTab = false,
            ShowAssetsTab = false,
            ScopeEventsToRoot = true,
            PropertyEditHandler = propertyEditHandler
        };
    }

    private void ApplyDiagnosticsPropertyEdit(DevToolsPropertyEdit edit)
    {
        if (_isApplyingVisualEditorMutation ||
            edit.Target is not Control control ||
            !ReferenceEquals(edit.Target, edit.InspectedObject) ||
            string.IsNullOrWhiteSpace(edit.XamlPropertyName) ||
            !TryFormatDiagnosticsPropertyValue(edit, out var value))
        {
            return;
        }

        if (!TryGetDiagnosticsPreviewXamlFileForEdit(out var xamlFile, out var previewXaml))
        {
            return;
        }

        if (!TryResolveDiagnosticsPreviewControl(
                control,
                xamlFile,
                previewXaml,
                out _,
                out var element,
                out var diagnostics))
        {
            VisualEditorStatus = string.Join(Environment.NewLine, diagnostics);
            return;
        }

        var mutationSelector = XamlElementSelector.ByPath(element.Path.ToArray());
        var mutationPropertyName = ResolveMutationPropertyName(element, edit.XamlPropertyName);
        _visualEditorSelectedSelector = mutationSelector;
        VisualEditorPropertyName = mutationPropertyName;
        VisualEditorPropertyValue = value;
        var result = _visualMutationEngine.SetProperty(
            xamlFile.Text,
            mutationSelector,
            mutationPropertyName,
            value);

        try
        {
            _isApplyingDiagnosticsPropertyMutation = true;
            ApplyVisualEditorMutation(result);
        }
        finally
        {
            _isApplyingDiagnosticsPropertyMutation = false;
        }

        if (result.Success)
        {
            SetDiagnosticsAcceptedXamlText(xamlFile);
            VisualEditorStatus = $"Updated {mutationPropertyName} from diagnostics.";
        }
    }

    private static bool TryFormatDiagnosticsPropertyValue(DevToolsPropertyEdit edit, out string value)
    {
        value = string.Empty;

        if (edit.ResourceReferenceKind != DevToolsResourceReferenceKind.None)
        {
            if (!string.IsNullOrWhiteSpace(edit.NewValueText))
            {
                value = edit.NewValueText;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(edit.ResourceKeyText))
            {
                var extensionName = edit.ResourceReferenceKind == DevToolsResourceReferenceKind.Dynamic
                    ? "DynamicResource"
                    : "StaticResource";
                value = $"{{{extensionName} {edit.ResourceKeyText}}}";
                return true;
            }

            return false;
        }

        switch (edit.NewValue)
        {
            case null:
                return false;
            case string text:
                value = text;
                return true;
            case bool flag:
                value = flag ? "True" : "False";
                return true;
            case double number when double.IsNaN(number) || double.IsInfinity(number):
                return false;
            case double number:
                value = FormatDiagnosticsDouble(number);
                return true;
            case float number when float.IsNaN(number) || float.IsInfinity(number):
                return false;
            case float number:
                value = number.ToString("R", CultureInfo.InvariantCulture);
                return true;
            case decimal number:
                value = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case int or long or short or byte or uint or ulong or ushort or sbyte:
                value = ((IFormattable)edit.NewValue).ToString(null, CultureInfo.InvariantCulture);
                return true;
            case Enum enumValue:
                value = enumValue.ToString();
                return true;
            case Color color:
                value = color.ToString();
                return true;
            case ISolidColorBrush solidColorBrush:
                value = solidColorBrush.Color.ToString();
                return true;
            case Thickness thickness:
                value = string.Join(
                    ",",
                    FormatDiagnosticsDouble(thickness.Left),
                    FormatDiagnosticsDouble(thickness.Top),
                    FormatDiagnosticsDouble(thickness.Right),
                    FormatDiagnosticsDouble(thickness.Bottom));
                return true;
            case CornerRadius cornerRadius:
                value = string.Join(
                    ",",
                    FormatDiagnosticsDouble(cornerRadius.TopLeft),
                    FormatDiagnosticsDouble(cornerRadius.TopRight),
                    FormatDiagnosticsDouble(cornerRadius.BottomRight),
                    FormatDiagnosticsDouble(cornerRadius.BottomLeft));
                return true;
        }

        if (edit.NewValueText is not { } valueText)
        {
            return false;
        }

        if (valueText.Length == 0 && edit.PropertyType != typeof(string))
        {
            return false;
        }

        value = valueText;
        return true;
    }

    private static string FormatDiagnosticsDouble(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private readonly record struct DesignerThickness(double Left, double Top, double Right, double Bottom);

    private sealed class DiagnosticsPropertyEditHandler : IDevToolsPropertyEditHandler
    {
        private readonly MainViewModel _owner;

        public DiagnosticsPropertyEditHandler(MainViewModel owner)
        {
            _owner = owner;
        }

        public void OnPropertyEdited(DevToolsPropertyEdit edit)
        {
            _owner.ApplyDiagnosticsPropertyEdit(edit);
        }
    }

    private sealed record ThemeEditScope(
        InMemoryProjectFile OwnerFile,
        string? SelectionFilePath,
        int SelectionStart,
        int SelectionLength,
        int CaretOffset,
        string ThemeKey);

    private static IReadOnlyDictionary<string, string>? CreateToolboxInsertionProperties(
        Control targetParent,
        Point? canvasPosition)
    {
        if (targetParent is not Canvas || canvasPosition is not { } point)
        {
            return null;
        }

        return new Dictionary<string, string>
        {
            ["Canvas.Left"] = FormatDesignerDouble(Math.Max(0, point.X)),
            ["Canvas.Top"] = FormatDesignerDouble(Math.Max(0, point.Y))
        };
    }

    private void InsertSelectedToolboxItem()
    {
        if (ActiveXamlFile is not { } xamlFile ||
            SelectedVisualEditorToolboxItem is not { } item)
        {
            return;
        }

        var parentSelector = _visualEditorCurrentContainerSelector ??
                             _visualEditorSelectedSelector ??
                             XamlElementSelector.ByPath();
        var insertion = _visualToolboxInsertion.Insert(xamlFile.Text, parentSelector, item);
        ApplyVisualEditorMutation(insertion.Mutation);
    }

    private void WrapVisualEditorSelection()
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            return;
        }

        ApplyVisualEditorMutation(_visualMutationEngine.WrapElement(
            xamlFile.Text,
            selector,
            "<Border Padding=\"8\" />"));
    }

    private void UnwrapVisualEditorSelection()
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            return;
        }

        ApplyVisualEditorMutation(_visualMutationEngine.UnwrapElement(xamlFile.Text, selector));
    }

    private void LoadFluentControlThemeTemplates()
    {
        FluentControlThemeTemplates = new ObservableCollection<FluentControlThemeTemplateViewModel>(
            _controlThemeCatalog.Templates
                .Select(static template => new FluentControlThemeTemplateViewModel(
                    template.Key,
                    FluentControlThemeCatalog.GetLocalName(template.TargetType),
                    template.SourcePath)));
        RefreshControlThemeFilters();

        ControlThemeSourceStatus = FluentControlThemeTemplates.Count == 0
            ? $"{_controlThemeCatalog.SourceDescription} No ControlTheme templates found."
            : $"{_controlThemeCatalog.SourceDescription} ({FluentControlThemeTemplates.Count} template(s)).";
    }

    private void RefreshControlThemes()
    {
        var previousKey = SelectedControlTheme?.Key;
        var themes = ActiveProject is null
            ? Array.Empty<ControlThemeDefinition>()
            : ControlThemeResourceBuilder.FindCustomThemes(
                ActiveProject
                    .GetXamlFiles()
                    .Where(static file => file.Kind == ProjectFileKind.Resource)
                    .Select(static file => (file.Path, file.Text)));

        ControlThemes = new ObservableCollection<ControlThemeDefinitionViewModel>(
            themes.Select(static theme => new ControlThemeDefinitionViewModel(
                theme.Key,
                theme.TargetType,
                theme.FilePath)));

        RefreshThemeResourceAnalysis();
        RefreshDesignInspection();
        RefreshControlThemeFilters(previousKey);
        NotifyControlThemeCommandsChanged();
    }

    private void RefreshThemeResourceAnalysis()
    {
        var previousKey = SelectedThemeResource?.Key;
        _themeResourceAnalysis = ActiveProject is null
            ? ThemeResourceAnalysis.Empty
            : ResourceDictionaryAnalyzer.Analyze(
                ActiveProject.GetXamlFiles()
                    .Select(static file => new ThemeResourceDocument(
                        file.Path,
                        file.Text,
                        file.Kind == ProjectFileKind.Resource)));

        ThemeResources = new ObservableCollection<ThemeResourceViewModel>(
            _themeResourceAnalysis.Resources.Select(static resource => new ThemeResourceViewModel(
                resource.Key,
                resource.ResourceType,
                resource.TargetType,
                resource.FilePath,
                resource.Line,
                resource.ThemeScope)));

        ThemeResourceDiagnostics = new ObservableCollection<ThemeResourceDiagnosticViewModel>(
            _themeResourceAnalysis.Diagnostics.Select(static diagnostic => new ThemeResourceDiagnosticViewModel(
                diagnostic.Severity.ToString(),
                diagnostic.Message,
                diagnostic.FilePath,
                diagnostic.Line)));

        RefreshThemeVariants();
        RefreshThemeResourceFilters();
        var previousFilePath = SelectedThemeResource?.FilePath;
        var previousLine = SelectedThemeResource?.Line;
        var previousThemeScope = SelectedThemeResource?.ThemeScope;
        SelectedThemeResource =
            FilteredThemeResources.FirstOrDefault(resource =>
                string.Equals(resource.Key, previousKey, StringComparison.Ordinal) &&
                string.Equals(resource.FilePath, previousFilePath, StringComparison.OrdinalIgnoreCase) &&
                resource.Line == previousLine &&
                string.Equals(resource.ThemeScope, previousThemeScope, StringComparison.OrdinalIgnoreCase)) ??
            FilteredThemeResources.FirstOrDefault(resource => string.Equals(resource.Key, previousKey, StringComparison.Ordinal)) ??
            FilteredThemeResources.FirstOrDefault();
        RefreshSelectedThemeResourceUsages();
    }

    private void RefreshSelectedThemeResourceUsages()
    {
        var selectedKey = SelectedThemeResource?.Key;
        SelectedThemeResourceUsages = string.IsNullOrWhiteSpace(selectedKey)
            ? new ObservableCollection<ThemeResourceUsageViewModel>()
            : new ObservableCollection<ThemeResourceUsageViewModel>(
                _themeResourceAnalysis.References
                    .Where(reference => string.Equals(reference.Key, selectedKey, StringComparison.Ordinal))
                    .Select(static reference => new ThemeResourceUsageViewModel(
                        reference.Key,
                        reference.Kind.ToString(),
                        reference.FilePath,
                        reference.Line,
                        reference.Snippet)));
    }

    private void RefreshThemeVariants()
    {
        ThemeVariants = ActiveProject is null
            ? new ObservableCollection<ThemeVariantViewModel>()
            : new ObservableCollection<ThemeVariantViewModel>(
                ActiveProject
                    .GetXamlFiles()
                    .Where(static file => file.Kind == ProjectFileKind.Resource)
                    .GroupBy(static file => ThemeProjectStorage.InferVariant(file.Path), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(static group => new ThemeVariantViewModel(
                        group.Key,
                        group.Count(),
                        string.Join(", ", group.Select(file => file.Path).OrderBy(path => path, StringComparer.OrdinalIgnoreCase)))));
    }

    private void RefreshThemeResourceFilters()
    {
        var query = ControlThemeSearchText?.Trim();
        var resources = ThemeResources.AsEnumerable();
        var diagnostics = ThemeResourceDiagnostics.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            resources = resources.Where(resource => MatchesThemeResourceSearch(resource, query));
            diagnostics = diagnostics.Where(diagnostic => MatchesThemeResourceSearch(diagnostic, query));
        }

        FilteredThemeResources = new ObservableCollection<ThemeResourceViewModel>(resources);
        FilteredThemeResourceDiagnostics = new ObservableCollection<ThemeResourceDiagnosticViewModel>(diagnostics);
    }

    private void RefreshControlThemeFilters(string? preferredThemeKey = null)
    {
        var query = ControlThemeSearchText?.Trim();
        var customThemes = ControlThemes.AsEnumerable();
        var fluentTemplates = FluentControlThemeTemplates.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            customThemes = customThemes.Where(theme => MatchesControlThemeSearch(theme, query));
            fluentTemplates = fluentTemplates.Where(template => MatchesControlThemeSearch(template, query));
        }

        FilteredControlThemes = new ObservableCollection<ControlThemeDefinitionViewModel>(customThemes);
        FilteredFluentControlThemeTemplates = new ObservableCollection<FluentControlThemeTemplateViewModel>(fluentTemplates);
        RefreshThemeResourceFilters();

        SelectedControlTheme = SelectFilteredControlTheme(preferredThemeKey ?? SelectedControlTheme?.Key);
        if (SelectedFluentControlThemeTemplate is not null &&
            !FilteredFluentControlThemeTemplates.Contains(SelectedFluentControlThemeTemplate))
        {
            SelectedFluentControlThemeTemplate = null;
        }

        if (SelectedThemeResource is null ||
            !FilteredThemeResources.Any(resource => ReferenceEquals(resource, SelectedThemeResource)))
        {
            SelectedThemeResource = FilteredThemeResources.FirstOrDefault();
        }

        UpdateControlThemeStatus(query);

        NotifyControlThemeCommandsChanged();
    }

    private ControlThemeDefinitionViewModel? SelectFilteredControlTheme(string? preferredThemeKey)
    {
        if (!string.IsNullOrWhiteSpace(preferredThemeKey))
        {
            var preferredTheme = FilteredControlThemes.FirstOrDefault(theme =>
                string.Equals(theme.Key, preferredThemeKey, StringComparison.Ordinal));
            if (preferredTheme is not null)
            {
                return preferredTheme;
            }
        }

        return GetControlThemesForSelectedVisualElement(FilteredControlThemes).FirstOrDefault() ??
               FilteredControlThemes.FirstOrDefault();
    }

    private void UpdateControlThemeStatus(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ControlThemeStatus = ControlThemes.Count == 0
                ? "No custom control themes."
                : $"{ControlThemes.Count} custom control theme(s).";
            return;
        }

        ControlThemeStatus =
            $"{FilteredControlThemes.Count} of {ControlThemes.Count} custom, " +
            $"{FilteredFluentControlThemeTemplates.Count} of {FluentControlThemeTemplates.Count} Fluent source template(s).";
    }

    private static bool MatchesControlThemeSearch(ControlThemeDefinitionViewModel theme, string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token =>
                theme.Key.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                theme.TargetType.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                theme.FilePath.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                theme.Title.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesControlThemeSearch(FluentControlThemeTemplateViewModel template, string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token =>
                template.Key.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                template.TargetType.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                template.SourcePath.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                template.Title.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesThemeResourceSearch(ThemeResourceViewModel resource, string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token =>
                resource.Key.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                resource.ResourceType.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                resource.TargetType.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                resource.FilePath.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                resource.Title.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesThemeResourceSearch(ThemeResourceDiagnosticViewModel diagnostic, string query)
    {
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token =>
                diagnostic.Severity.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Message.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                diagnostic.FilePath.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                diagnostic.Title.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<ControlThemeDefinitionViewModel> GetControlThemesForSelectedVisualElement()
    {
        return GetControlThemesForSelectedVisualElement(ControlThemes);
    }

    private IReadOnlyList<ControlThemeDefinitionViewModel> GetControlThemesForSelectedVisualElement(
        IEnumerable<ControlThemeDefinitionViewModel> themes)
    {
        return TryGetSelectedControlThemeTargetType(out var targetType)
            ? themes
                .Where(theme => string.Equals(theme.TargetType, targetType, StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<ControlThemeDefinitionViewModel>();
    }

    private void RefreshControlThemeSelectionState()
    {
        if (TryGetSelectedControlThemeTargetType(out var targetType))
        {
            ControlThemeSelectedTargetType = targetType;
            if (SelectedControlTheme is null ||
                !FilteredControlThemes.Contains(SelectedControlTheme) ||
                !string.Equals(SelectedControlTheme.TargetType, targetType, StringComparison.Ordinal))
            {
                SelectedControlTheme = FilteredControlThemes.FirstOrDefault(theme =>
                    string.Equals(theme.TargetType, targetType, StringComparison.Ordinal));
            }
        }
        else
        {
            ControlThemeSelectedTargetType = SelectedFluentControlThemeTemplate is { } template
                ? $"Fluent template: {template.TargetType}"
                : "No control selected.";
        }

        NotifyControlThemeCommandsChanged();
    }

    private bool TryGetSelectedControlThemeTargetType([NotNullWhen(true)] out string? targetType)
    {
        targetType = null;
        var element = FindElement(_visualEditorDocument, _visualEditorSelectedSelector);
        if (element is null)
        {
            return false;
        }

        var localName = GetLocalName(element.TypeName);
        if (ResolveControlType(localName) is null)
        {
            return false;
        }

        targetType = localName;
        return true;
    }

    private void RefreshSelectedResourceThemeAnalysis(ThemeResourceViewModel? resource)
    {
        if (resource is { ResourceType: "ControlTheme" } &&
            ControlThemes.FirstOrDefault(theme =>
                string.Equals(theme.Key, resource.Key, StringComparison.Ordinal) &&
                string.Equals(theme.FilePath, resource.FilePath, StringComparison.OrdinalIgnoreCase)) is { } theme)
        {
            SelectedControlTheme = theme;
            return;
        }

        if (SelectedControlTheme is not null)
        {
            RefreshSelectedControlThemeAnalysis();
            return;
        }

        _selectedControlThemeAnalysis = ControlThemeAnalysis.Empty;
        ThemePreviewStates = new ObservableCollection<ThemePreviewStateViewModel>();
        SelectedThemePreviewState = null;
        ThemeStateSelectors = new ObservableCollection<ThemeStateSelectorViewModel>();
        ThemeTemplateParts = new ObservableCollection<ThemeTemplatePartViewModel>();
        SelectedThemeTemplatePart = null;
        ThemeTemplatePartSelectors = new ObservableCollection<ThemeTemplatePartSelectorViewModel>();
        ThemeTemplateBindings = new ObservableCollection<ThemeTemplateBindingViewModel>();
    }

    private void RefreshSelectedControlThemeAnalysis()
    {
        if (ActiveProject is null ||
            SelectedControlTheme is not { } selectedTheme ||
            ActiveProject.FindFile(selectedTheme.FilePath) is not { } themeFile)
        {
            _selectedControlThemeAnalysis = ControlThemeAnalysis.Empty;
            ThemePreviewStates = new ObservableCollection<ThemePreviewStateViewModel>();
            SelectedThemePreviewState = null;
            ThemeStateSelectors = new ObservableCollection<ThemeStateSelectorViewModel>();
            ThemeTemplateParts = new ObservableCollection<ThemeTemplatePartViewModel>();
            SelectedThemeTemplatePart = null;
            ThemeTemplatePartSelectors = new ObservableCollection<ThemeTemplatePartSelectorViewModel>();
            ThemeTemplateBindings = new ObservableCollection<ThemeTemplateBindingViewModel>();
            return;
        }

        var previousState = SelectedThemePreviewState?.State;
        var previousPartName = SelectedThemeTemplatePart?.Name;
        _selectedControlThemeAnalysis = ControlThemeAnalyzer.Analyze(themeFile.Text, selectedTheme.Key);
        var stateSelectorKeys = _selectedControlThemeAnalysis.StateSelectors
            .Select(static selector => selector.State)
            .ToHashSet(StringComparer.Ordinal);
        ThemePreviewStates = new ObservableCollection<ThemePreviewStateViewModel>(
            _selectedControlThemeAnalysis.AvailableStates.Select(state => new ThemePreviewStateViewModel(
                state,
                stateSelectorKeys.Contains(state))));
        ThemeTemplateParts = new ObservableCollection<ThemeTemplatePartViewModel>(
            _selectedControlThemeAnalysis.Parts.Select(static part => new ThemeTemplatePartViewModel(
                part.Name,
                part.Type,
                part.Line)));
        SelectedThemeTemplatePart =
            ThemeTemplateParts.FirstOrDefault(part => string.Equals(part.Name, previousPartName, StringComparison.Ordinal)) ??
            ThemeTemplateParts.FirstOrDefault();
        ThemeTemplateBindings = new ObservableCollection<ThemeTemplateBindingViewModel>(
            _selectedControlThemeAnalysis.TemplateBindings.Select(static binding => new ThemeTemplateBindingViewModel(
                binding.Property,
                binding.Line,
                binding.Snippet)));
        SelectedThemePreviewState =
            ThemePreviewStates.FirstOrDefault(state => string.Equals(state.State, previousState, StringComparison.Ordinal)) ??
            ThemePreviewStates.FirstOrDefault(state => string.Equals(state.State, "normal", StringComparison.Ordinal)) ??
            ThemePreviewStates.FirstOrDefault();
        RefreshThemeStateSelectors();
        RefreshThemeTemplatePartSelectors();
    }

    private void RefreshThemeStateSelectors()
    {
        var state = SelectedThemePreviewState?.State;
        ThemeStateSelectors = string.IsNullOrWhiteSpace(state) ||
                              string.Equals(state, "normal", StringComparison.Ordinal)
            ? new ObservableCollection<ThemeStateSelectorViewModel>()
            : new ObservableCollection<ThemeStateSelectorViewModel>(
                _selectedControlThemeAnalysis.StateSelectors
                    .Where(selector => string.Equals(selector.State, state, StringComparison.Ordinal))
                    .Select(static selector => new ThemeStateSelectorViewModel(
                        selector.State,
                        selector.Selector,
                        selector.Line)));
    }

    private void RefreshThemeTemplatePartSelectors()
    {
        var partName = SelectedThemeTemplatePart?.Name;
        ThemeTemplatePartSelectors = string.IsNullOrWhiteSpace(partName)
            ? new ObservableCollection<ThemeTemplatePartSelectorViewModel>()
            : new ObservableCollection<ThemeTemplatePartSelectorViewModel>(
                _selectedControlThemeAnalysis.PartSelectors
                    .Where(selector => string.Equals(selector.PartName, partName, StringComparison.Ordinal))
                    .Select(static selector => new ThemeTemplatePartSelectorViewModel(
                        selector.PartName,
                        selector.PartType,
                        selector.State,
                        selector.Selector,
                        selector.Line)));
    }

    private bool CanApplyThemeStateSetter()
    {
        return ActiveProject is not null &&
               SelectedControlTheme is not null &&
               SelectedThemePreviewState is { } state &&
               !string.Equals(state.State, "normal", StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(ThemeStateSetterPropertyName);
    }

    private void ApplyThemeStateSetter()
    {
        if (ActiveProject is null ||
            SelectedControlTheme is not { } selectedTheme ||
            SelectedThemePreviewState is not { } selectedState ||
            ActiveProject.FindFile(selectedTheme.FilePath) is not { } themeFile)
        {
            return;
        }

        var edit = ControlThemeEditor.SetStateSetter(
            themeFile.Text,
            selectedTheme.Key,
            selectedState.State,
            ThemeStateSetterPropertyName.Trim(),
            ThemeStateSetterValue);
        if (!edit.Changed)
        {
            ControlThemeStatus = edit.Error ?? $"Could not update {selectedState.PseudoClass}.";
            return;
        }

        themeFile.Text = edit.Text;
        RefreshWorkspaceAfterThemeFileChanges(themeFile);
        SelectThemeResource(selectedTheme.Key);
        SelectedThemePreviewState = ThemePreviewStates.FirstOrDefault(state =>
            string.Equals(state.State, selectedState.State, StringComparison.Ordinal));
        ControlThemeStatus = $"Updated {selectedState.PseudoClass} setter {ThemeStateSetterPropertyName}.";
    }

    private bool CanApplyThemeTemplatePartSetter()
    {
        return ActiveProject is not null &&
               SelectedControlTheme is not null &&
               SelectedThemeTemplatePart is not null &&
               !string.IsNullOrWhiteSpace(ThemeTemplatePartSetterPropertyName);
    }

    private void ApplyThemeTemplatePartSetter()
    {
        if (ActiveProject is null ||
            SelectedControlTheme is not { } selectedTheme ||
            SelectedThemeTemplatePart is not { } selectedPart ||
            ActiveProject.FindFile(selectedTheme.FilePath) is not { } themeFile)
        {
            return;
        }

        var selectedState = SelectedThemePreviewState;
        var selector = CreateTemplatePartSelector(selectedPart, selectedState);
        var edit = ControlThemeEditor.SetSelectorSetter(
            themeFile.Text,
            selectedTheme.Key,
            selector,
            ThemeTemplatePartSetterPropertyName.Trim(),
            ThemeTemplatePartSetterValue);
        if (!edit.Changed)
        {
            ControlThemeStatus = edit.Error ?? $"Could not update {selector}.";
            return;
        }

        var partName = selectedPart.Name;
        var state = selectedState?.State;
        themeFile.Text = edit.Text;
        RefreshWorkspaceAfterThemeFileChanges(themeFile);
        SelectThemeResource(selectedTheme.Key);
        SelectedThemeTemplatePart = ThemeTemplateParts.FirstOrDefault(part =>
            string.Equals(part.Name, partName, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(state))
        {
            SelectedThemePreviewState = ThemePreviewStates.FirstOrDefault(previewState =>
                string.Equals(previewState.State, state, StringComparison.Ordinal));
        }

        ControlThemeStatus = $"Updated {selector} setter {ThemeTemplatePartSetterPropertyName}.";
    }

    private static string CreateTemplatePartSelector(
        ThemeTemplatePartViewModel part,
        ThemePreviewStateViewModel? state)
    {
        var selector = $"^ /template/ {part.Type}#{part.Name}";
        return state is null || string.Equals(state.State, "normal", StringComparison.Ordinal)
            ? selector
            : $"^:{state.State.TrimStart(':')} /template/ {part.Type}#{part.Name}";
    }

    private bool CanCreateThemeVariantPreview()
    {
        return ActiveProject is not null &&
               SelectedControlTheme is not null;
    }

    private void CreateThemeVariantPreview()
    {
        if (ActiveProject is null ||
            SelectedControlTheme is not { } selectedTheme ||
            ActiveProject.FindFile(selectedTheme.FilePath) is not { } themeFile)
        {
            return;
        }

        var preview = ControlThemeResourceBuilder.CreateVariantPreviewXaml(
            selectedTheme.TargetType,
            selectedTheme.Key);
        var edit = ControlThemeEditor.SetDesignPreview(themeFile.Text, preview);
        if (!edit.Changed)
        {
            ControlThemeStatus = edit.Error ?? $"Could not create variant preview for {selectedTheme.Key}.";
            return;
        }

        themeFile.Text = edit.Text;
        RefreshWorkspaceAfterThemeFileChanges(themeFile);
        SelectThemeResource(selectedTheme.Key);
        ControlThemeStatus = $"Created side-by-side light/dark preview for {selectedTheme.Key}.";
    }

    private void LoadAnimationPresets()
    {
        AnimationPresets = new ObservableCollection<AnimationPresetViewModel>
        {
            new("Fade in", "Opacity", "0", "1", "0:0:0.25", "CubicEaseOut"),
            new("Fade out", "Opacity", "1", "0", "0:0:0.2", "CubicEaseIn"),
            new("Subtle scale", "ScaleTransform.ScaleX", "1", "1.04", "0:0:0.16", "CubicEaseOut"),
            new("Slide right", "TranslateTransform.X", "0", "12", "0:0:0.22", "CubicEaseOut"),
            new("Rotate", "RotateTransform.Angle", "0", "8", "0:0:0.22", "BackEaseOut")
        };
        SelectedAnimationPreset = AnimationPresets.FirstOrDefault();
    }

    private void RefreshAnimationTargetOptions()
    {
        var previousId = SelectedAnimationTargetOption?.Id;
        var previousSelector = AnimationTargetSelector;
        var options = new List<AnimationTargetOptionViewModel>();

        if (_visualEditorSelectedSelector is not null &&
            SelectedVisualEditorNode?.Element is { } selectedElement)
        {
            options.Add(new AnimationTargetOptionViewModel(
                "visual-selection",
                $"Selected {FormatVisualEditorElementTitle(selectedElement)}",
                "Visual",
                CreateElementStyleSelector(selectedElement)));
        }

        if (ActiveXamlFile is { IsXaml: true } xamlFile)
        {
            foreach (var styleTarget in _animationTimelineEditor.GetDocumentStyleTargets(xamlFile.Text))
            {
                options.Add(new AnimationTargetOptionViewModel(
                    $"style:{styleTarget.Index}",
                    $"Style {styleTarget.Index + 1}: {styleTarget.Selector}",
                    "Style",
                    styleTarget.Selector,
                    styleTarget.Index));
            }
        }

        if (SelectedControlTheme is { } selectedTheme)
        {
            options.Add(new AnimationTargetOptionViewModel(
                "theme-root",
                $"{selectedTheme.Key} root",
                "Theme",
                "^"));

            if (SelectedThemePreviewState is { } selectedState &&
                !string.Equals(selectedState.State, "normal", StringComparison.Ordinal))
            {
                options.Add(new AnimationTargetOptionViewModel(
                    $"theme-state:{selectedState.State}",
                    $"{selectedTheme.Key} {selectedState.PseudoClass}",
                    "Theme state",
                    $"^:{selectedState.State.TrimStart(':')}"));
            }

            if (SelectedThemeTemplatePart is { } selectedPart)
            {
                options.Add(new AnimationTargetOptionViewModel(
                    $"theme-part:{selectedPart.Name}",
                    selectedPart.Title,
                    "Template part",
                    CreateTemplatePartSelector(selectedPart, SelectedThemePreviewState)));
            }
        }

        AnimationTargetOptions = new ObservableCollection<AnimationTargetOptionViewModel>(options);
        SelectedAnimationTargetOption =
            AnimationTargetOptions.FirstOrDefault(option => string.Equals(option.Id, previousId, StringComparison.Ordinal)) ??
            AnimationTargetOptions.FirstOrDefault(option => string.Equals(option.Selector, previousSelector, StringComparison.Ordinal)) ??
            AnimationTargetOptions.FirstOrDefault();

        if (SelectedAnimationTargetOption is null)
        {
            ClearAnimationTimelineEditor();
        }

        NotifyAnimationCommandsChanged();
    }

    private void ClearAnimationTimelineEditor()
    {
        StopAnimationTimeline(completed: false);
        AnimationTimelineTracks = new ObservableCollection<AnimationTimelineTrackViewModel>();
        SelectedAnimationTimelineTrack = null;
        SelectedAnimationTimelineKeyFrame = null;
        AnimationPlaybackStatus = "Select a visual element or control theme to edit animations.";
    }

    private void LoadAnimationTimelineFromTarget()
    {
        var target = SelectedAnimationTargetOption;
        if (target is null)
        {
            return;
        }

        var selector = string.IsNullOrWhiteSpace(AnimationTargetSelector)
            ? target.Selector
            : AnimationTargetSelector.Trim();
        var timeline = AnimationTimelineDefinition.CreateEmpty(selector);

        if (IsVisualAnimationTarget(target) &&
            ActiveXamlFile is { } xamlFile &&
            _visualEditorSelectedSelector is { } selectedSelector)
        {
            timeline = _animationTimelineEditor.ReadElementAnimation(
                xamlFile.Text,
                selectedSelector,
                selector);
        }
        else if (IsDocumentStyleAnimationTarget(target) &&
                 ActiveXamlFile is { } styleFile)
        {
            timeline = _animationTimelineEditor.ReadDocumentStyleAnimation(
                styleFile.Text,
                target.StyleIndex ?? -1,
                target.Selector,
                selector);
        }
        else if (IsThemeAnimationTarget(target) &&
                 ActiveProject is { } project &&
                 SelectedControlTheme is { } selectedTheme &&
                 project.FindFile(selectedTheme.FilePath) is { } themeFile)
        {
            timeline = _animationTimelineEditor.ReadControlThemeAnimation(
                themeFile.Text,
                selectedTheme.Key,
                selector);
        }

        ApplyAnimationTimelineDefinitionToEditor(timeline);
        AnimationPlaybackStatus = timeline.Tracks.Count == 0
            ? $"No animation exists for {target.Title}."
            : $"Loaded {timeline.Tracks.Count} animation track(s) for {target.Title}.";
    }

    private void ApplyAnimationTimelineDefinitionToEditor(AnimationTimelineDefinition timeline)
    {
        AnimationTargetSelector = timeline.TargetSelector;
        AnimationDurationText = timeline.Duration;
        AnimationDelayText = timeline.Delay;
        AnimationIterationCountText = timeline.IterationCount;
        AnimationPlaybackDirectionText = string.IsNullOrWhiteSpace(timeline.PlaybackDirection) ? "Normal" : timeline.PlaybackDirection;
        AnimationFillModeText = string.IsNullOrWhiteSpace(timeline.FillMode) ? "Both" : timeline.FillMode;
        AnimationEasingText = string.IsNullOrWhiteSpace(timeline.Easing) ? "CubicEaseOut" : timeline.Easing;
        AnimationTimelineTracks = new ObservableCollection<AnimationTimelineTrackViewModel>(
            timeline.Tracks.Select(static track => new AnimationTimelineTrackViewModel(
                track.TargetSelector,
                track.PropertyName,
                new ObservableCollection<AnimationTimelineKeyFrameViewModel>(
                    track.KeyFrames.Select(static frame => new AnimationTimelineKeyFrameViewModel(
                        frame.CuePercent,
                        frame.Value,
                        frame.KeySpline))))));
        SelectedAnimationTimelineTrack = AnimationTimelineTracks.FirstOrDefault();
        if (SelectedAnimationTimelineTrack is null &&
            !string.IsNullOrWhiteSpace(AnimationPropertyName))
        {
            AnimationKeyFrameValue = "1";
        }
    }

    private bool CanAddAnimationTrack()
    {
        return SelectedAnimationTargetOption is not null &&
               !string.IsNullOrWhiteSpace(AnimationTargetSelector) &&
               !string.IsNullOrWhiteSpace(AnimationPropertyName);
    }

    private void AddAnimationTrack()
    {
        if (!CanAddAnimationTrack())
        {
            return;
        }

        var propertyName = AnimationPropertyName.Trim();
        var existing = AnimationTimelineTracks.FirstOrDefault(track =>
            string.Equals(track.PropertyName, propertyName, StringComparison.Ordinal));
        if (existing is not null)
        {
            SelectedAnimationTimelineTrack = existing;
            AnimationPlaybackStatus = $"Selected existing {propertyName} track.";
            return;
        }

        var preset = SelectedAnimationPreset;
        var presetMatches = preset is not null &&
                            string.Equals(preset.PropertyName, propertyName, StringComparison.Ordinal);
        var fromValue = presetMatches
            ? preset!.FromValue
            : ResolveCurrentAnimationPropertyValue(propertyName);
        var toValue = presetMatches
            ? preset!.ToValue
            : AnimationKeyFrameValue;
        var track = new AnimationTimelineTrackViewModel(
            AnimationTargetSelector.Trim(),
            propertyName,
            new ObservableCollection<AnimationTimelineKeyFrameViewModel>
            {
                new(0, fromValue, string.Empty),
                new(Math.Clamp(AnimationCuePercent, 1, 100), string.IsNullOrWhiteSpace(toValue) ? fromValue : toValue, AnimationKeySplineText)
            });

        AnimationTimelineTracks.Add(track);
        SelectedAnimationTimelineTrack = track;
        SelectedAnimationTimelineKeyFrame = track.KeyFrames.LastOrDefault();
        AnimationPlaybackStatus = $"Added {propertyName} animation track.";
        NotifyAnimationTimelineChanged();
    }

    private bool CanEditAnimationKeyFrames()
    {
        return SelectedAnimationTimelineTrack is not null;
    }

    private bool CanCaptureAnimationKeyFrame()
    {
        return SelectedAnimationTargetOption is not null &&
               !string.IsNullOrWhiteSpace(AnimationPropertyName);
    }

    private void CaptureAnimationKeyFrame()
    {
        if (!CanCaptureAnimationKeyFrame())
        {
            return;
        }

        var propertyName = AnimationPropertyName.Trim();
        var value = AnimationKeyFrameValue;
        if (SelectedVisualEditorProperty is { } selectedProperty &&
            (string.Equals(selectedProperty.MutationName, propertyName, StringComparison.Ordinal) ||
             string.Equals(selectedProperty.Name, propertyName, StringComparison.Ordinal)))
        {
            value = selectedProperty.Value;
        }
        else if (TryGetVisualEditorPropertyValue(propertyName, out var currentValue) &&
                 !string.IsNullOrWhiteSpace(currentValue))
        {
            value = currentValue;
        }

        AddOrUpdateAnimationKeyFrame(propertyName, value, AnimationCurrentTimePercent, AnimationKeySplineText);
        ApplyAnimationTimeline();
        AnimationPlaybackStatus = $"Captured {propertyName} at {AnimationCurrentTimePercent}%.";
    }

    private void AddAnimationKeyFrame()
    {
        if (SelectedAnimationTimelineTrack is not { } track)
        {
            return;
        }

        AddOrUpdateAnimationKeyFrame(track.PropertyName, AnimationKeyFrameValue, AnimationCuePercent, AnimationKeySplineText);
    }

    private AnimationTimelineTrackViewModel? AddOrUpdateAnimationKeyFrame(
        string propertyName,
        string value,
        int cuePercent,
        string keySpline)
    {
        var normalizedPropertyName = propertyName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPropertyName))
        {
            AnimationPlaybackStatus = "Animation property name is required.";
            NotifyAnimationCommandsChanged();
            return null;
        }

        var cue = Math.Clamp(cuePercent, 0, 100);
        var track = AnimationTimelineTracks.FirstOrDefault(candidate =>
            string.Equals(candidate.PropertyName, normalizedPropertyName, StringComparison.Ordinal));
        if (track is null)
        {
            track = new AnimationTimelineTrackViewModel(
                string.IsNullOrWhiteSpace(AnimationTargetSelector) ? "^" : AnimationTargetSelector.Trim(),
                normalizedPropertyName,
                new ObservableCollection<AnimationTimelineKeyFrameViewModel>());
            AnimationTimelineTracks.Add(track);
        }

        var existing = track.KeyFrames.FirstOrDefault(frame => frame.CuePercent == cue);
        if (existing is not null)
        {
            existing.Value = value;
            existing.KeySpline = keySpline;
            SelectedAnimationTimelineKeyFrame = existing;
            AnimationPlaybackStatus = $"Updated {track.PropertyName} keyframe at {cue}%.";
            NotifyAnimationTimelineChanged();
            return track;
        }

        var keyFrame = new AnimationTimelineKeyFrameViewModel(cue, value, keySpline);
        track.KeyFrames.Add(keyFrame);
        SortAnimationTrackKeyFrames(track);
        SelectedAnimationTimelineTrack = track;
        SelectedAnimationTimelineKeyFrame = keyFrame;
        AnimationPlaybackStatus = $"Added {track.PropertyName} keyframe at {cue}%.";
        NotifyAnimationTimelineChanged();
        return track;
    }

    private void UpdateAnimationKeyFrame()
    {
        if (SelectedAnimationTimelineTrack is not { } track ||
            SelectedAnimationTimelineKeyFrame is not { } keyFrame)
        {
            return;
        }

        var requestedCue = Math.Clamp(AnimationCuePercent, 0, 100);
        var cue = FindAvailableCue(track, requestedCue, keyFrame);
        keyFrame.CuePercent = cue;
        keyFrame.Value = AnimationKeyFrameValue;
        keyFrame.KeySpline = AnimationKeySplineText;
        SortAnimationTrackKeyFrames(track);
        SelectedAnimationTimelineKeyFrame = keyFrame;
        AnimationCuePercent = cue;
        AnimationCurrentTimePercent = cue;
        AnimationPlaybackStatus = cue == requestedCue
            ? $"Updated {track.PropertyName} keyframe at {cue}%."
            : $"Updated {track.PropertyName} keyframe at nearest available cue {cue}%.";
        NotifyAnimationTimelineChanged();
    }

    private void CommitAnimationKeyFrameEdit()
    {
        if (SelectedAnimationTimelineKeyFrame is not { } keyFrame)
        {
            return;
        }

        AnimationCuePercent = keyFrame.CuePercent;
        AnimationKeyFrameValue = keyFrame.Value;
        AnimationKeySplineText = keyFrame.KeySpline;
        UpdateAnimationKeyFrame();
    }

    private void RemoveAnimationKeyFrame()
    {
        if (SelectedAnimationTimelineTrack is not { } track ||
            SelectedAnimationTimelineKeyFrame is not { } keyFrame)
        {
            return;
        }

        track.KeyFrames.Remove(keyFrame);
        SelectedAnimationTimelineKeyFrame = track.KeyFrames
            .OrderBy(static frame => frame.CuePercent)
            .FirstOrDefault();
        AnimationPlaybackStatus = $"Removed {track.PropertyName} keyframe.";
        NotifyAnimationTimelineChanged();
    }

    private bool CanDuplicateAnimationKeyFrame()
    {
        return SelectedAnimationTimelineTrack is not null &&
               SelectedAnimationTimelineKeyFrame is not null;
    }

    private void DuplicateAnimationKeyFrame()
    {
        if (SelectedAnimationTimelineTrack is not { } track ||
            SelectedAnimationTimelineKeyFrame is not { } keyFrame)
        {
            return;
        }

        var cue = FindAvailableCue(track, Math.Min(100, keyFrame.CuePercent + 10), preferredDirection: 1);
        var duplicate = new AnimationTimelineKeyFrameViewModel(cue, keyFrame.Value, keyFrame.KeySpline);
        track.KeyFrames.Add(duplicate);
        SortAnimationTrackKeyFrames(track);
        SelectedAnimationTimelineKeyFrame = duplicate;
        AnimationCuePercent = cue;
        AnimationCurrentTimePercent = cue;
        AnimationPlaybackStatus = $"Duplicated {track.PropertyName} keyframe at {cue}%.";
        NotifyAnimationTimelineChanged();
    }

    private bool CanSelectAdjacentAnimationKeyFrame()
    {
        return SelectedAnimationTimelineTrack?.KeyFrames.Count > 0;
    }

    private void SelectAdjacentAnimationKeyFrame(bool previous)
    {
        if (SelectedAnimationTimelineTrack is not { } track ||
            track.KeyFrames.Count == 0)
        {
            return;
        }

        var frames = track.KeyFrames
            .OrderBy(static frame => frame.CuePercent)
            .ToArray();
        var currentIndex = SelectedAnimationTimelineKeyFrame is { } current
            ? Array.IndexOf(frames, current)
            : -1;
        if (currentIndex < 0)
        {
            currentIndex = previous ? frames.Length : -1;
        }

        var nextIndex = previous
            ? (currentIndex - 1 + frames.Length) % frames.Length
            : (currentIndex + 1) % frames.Length;
        SelectedAnimationTimelineKeyFrame = frames[nextIndex];
        AnimationPlaybackStatus = $"Selected {track.PropertyName} keyframe at {frames[nextIndex].CuePercent}%.";
    }

    private bool CanNudgeAnimationKeyFrame()
    {
        return SelectedAnimationTimelineTrack is not null &&
               SelectedAnimationTimelineKeyFrame is not null;
    }

    private void NudgeAnimationKeyFrame(int delta)
    {
        if (delta == 0 ||
            SelectedAnimationTimelineTrack is not { } track ||
            SelectedAnimationTimelineKeyFrame is not { } keyFrame)
        {
            return;
        }

        var requestedCue = Math.Clamp(keyFrame.CuePercent + delta, 0, 100);
        var cue = FindAvailableCue(track, requestedCue, keyFrame, Math.Sign(delta));
        keyFrame.CuePercent = cue;
        SortAnimationTrackKeyFrames(track);
        SelectedAnimationTimelineKeyFrame = keyFrame;
        AnimationCuePercent = cue;
        AnimationCurrentTimePercent = cue;
        AnimationPlaybackStatus = $"Moved {track.PropertyName} keyframe to {cue}%.";
        NotifyAnimationTimelineChanged();
    }

    private bool CanSeekAnimationTimeline()
    {
        return SelectedAnimationTargetOption is not null;
    }

    private void SeekAnimationPlayhead(int cuePercent)
    {
        AnimationCurrentTimePercent = Math.Clamp(cuePercent, 0, 100);
        AnimationPlaybackStatus = $"Playhead at {AnimationCurrentTimePercent}%.";
    }

    private bool CanPlayAnimationTimeline()
    {
        return SelectedAnimationTargetOption is not null &&
               AnimationTimelineTracks.Any(static track => track.KeyFrames.Count > 0);
    }

    private void PlayAnimationTimeline()
    {
        if (!CanPlayAnimationTimeline())
        {
            return;
        }

        if (AnimationTimelinePlaying)
        {
            StopAnimationTimeline(completed: false);
            return;
        }

        EnsureAnimationPlaybackTimer();
        _animationPlaybackDurationMilliseconds = ResolveAnimationPlaybackDurationMilliseconds();
        _animationPlaybackStartPercent = AnimationCurrentTimePercent >= 100 ? 0 : Math.Clamp(AnimationCurrentTimePercent, 0, 100);
        AnimationCurrentTimePercent = _animationPlaybackStartPercent;
        _animationPlaybackStartedUtc = DateTime.UtcNow;
        AnimationTimelinePlaying = true;
        _animationPlaybackTimer!.Start();
        AnimationPlaybackStatus = $"Playing timeline from {_animationPlaybackStartPercent}%.";
    }

    private void StopAnimationTimeline(bool completed)
    {
        _animationPlaybackTimer?.Stop();
        if (!AnimationTimelinePlaying)
        {
            return;
        }

        AnimationTimelinePlaying = false;
        AnimationPlaybackStatus = completed
            ? "Timeline playback complete."
            : $"Paused timeline at {AnimationCurrentTimePercent}%.";
    }

    private void EnsureAnimationPlaybackTimer()
    {
        if (_animationPlaybackTimer is not null)
        {
            return;
        }

        _animationPlaybackTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (_, _) => TickAnimationPlayback());
    }

    private void TickAnimationPlayback()
    {
        if (!AnimationTimelinePlaying)
        {
            _animationPlaybackTimer?.Stop();
            return;
        }

        var duration = Math.Max(1, _animationPlaybackDurationMilliseconds);
        var elapsed = Math.Max(0, (DateTime.UtcNow - _animationPlaybackStartedUtc).TotalMilliseconds);
        var remainingPercent = Math.Max(0, 100 - _animationPlaybackStartPercent);
        var cue = _animationPlaybackStartPercent + (int)Math.Round(elapsed / duration * remainingPercent);
        AnimationCurrentTimePercent = Math.Clamp(cue, 0, 100);
        if (AnimationCurrentTimePercent >= 100)
        {
            StopAnimationTimeline(completed: true);
        }
    }

    private double ResolveAnimationPlaybackDurationMilliseconds()
    {
        if (TimeSpan.TryParse(AnimationDurationText, CultureInfo.InvariantCulture, out var duration) &&
            duration.TotalMilliseconds > 0)
        {
            return duration.TotalMilliseconds;
        }

        return 300;
    }

    private bool CanApplyAnimationTimeline()
    {
        return SelectedAnimationTargetOption is not null &&
               AnimationTimelineTracks.Count > 0 &&
               AnimationTimelineTracks.Any(static track => track.KeyFrames.Count > 0);
    }

    private void ApplyAnimationTimeline()
    {
        if (SelectedAnimationTargetOption is not { } target)
        {
            return;
        }

        var timeline = CreateAnimationTimelineDefinition();
        if (IsVisualAnimationTarget(target))
        {
            ApplyVisualAnimationTimeline(target, timeline, runPreview: false);
            return;
        }

        if (IsDocumentStyleAnimationTarget(target))
        {
            ApplyDocumentStyleAnimationTimeline(target, timeline, runPreview: false);
            return;
        }

        ApplyThemeAnimationTimeline(target, timeline, runPreview: false);
    }

    private void PreviewAnimationTimeline()
    {
        if (SelectedAnimationTargetOption is not { } target)
        {
            return;
        }

        var timeline = CreateAnimationTimelineDefinition();
        if (IsVisualAnimationTarget(target))
        {
            ApplyVisualAnimationTimeline(target, timeline, runPreview: true);
            return;
        }

        if (IsDocumentStyleAnimationTarget(target))
        {
            ApplyDocumentStyleAnimationTimeline(target, timeline, runPreview: true);
            return;
        }

        ApplyThemeAnimationTimeline(target, timeline, runPreview: true);
    }

    private bool CanApplyAnimationFrameToTarget()
    {
        return SelectedAnimationTargetOption is not null &&
               AnimationTimelineTracks.Any(static track => track.KeyFrames.Count > 0);
    }

    private void ApplyAnimationFrameToTarget()
    {
        if (SelectedAnimationTargetOption is not { } target)
        {
            return;
        }

        var setters = ResolveAnimationFrameSetters(AnimationCurrentTimePercent).ToArray();
        if (setters.Length == 0)
        {
            AnimationPlaybackStatus = "No keyframe values exist at the current playhead.";
            return;
        }

        if (IsVisualAnimationTarget(target))
        {
            if (!TryGetVisualEditingContext(out var xamlFile, out var selector))
            {
                return;
            }

            var requests = setters.Select(setter => new XamlMutationRequest(
                XamlMutationKind.SetProperty,
                selector,
                setter.PropertyName,
                setter.Value));
            ApplyVisualEditorMutation(_visualMutationEngine.Batch(xamlFile.Text, requests));
            AnimationPlaybackStatus = $"Applied playhead values at {AnimationCurrentTimePercent}% to selected element.";
            return;
        }

        if (IsDocumentStyleAnimationTarget(target))
        {
            if (ActiveXamlFile is not { } styleFile)
            {
                return;
            }

            var styleText = styleFile.Text;
            foreach (var setter in setters)
            {
                var edit = _animationTimelineEditor.SetDocumentStyleSetter(
                    styleText,
                    target.StyleIndex ?? -1,
                    target.Selector,
                    AnimationTargetSelector.Trim(),
                    setter.PropertyName,
                    setter.Value);
                if (!edit.Changed)
                {
                    AnimationPlaybackStatus = edit.Error ?? "Could not apply playhead values.";
                    return;
                }

                styleText = edit.Text;
            }

            ApplyVisualEditorMutation(new XamlMutationResult(
                styleText,
                _visualMutationEngine.Analyze(styleText),
                Array.Empty<string>()));
            AnimationPlaybackStatus = $"Applied playhead values at {AnimationCurrentTimePercent}% to {AnimationTargetSelector}.";
            return;
        }

        if (ActiveProject is not { } project ||
            SelectedControlTheme is not { } selectedTheme ||
            project.FindFile(selectedTheme.FilePath) is not { } themeFile)
        {
            return;
        }

        var text = themeFile.Text;
        foreach (var setter in setters)
        {
            var edit = ControlThemeEditor.SetSelectorSetter(
                text,
                selectedTheme.Key,
                AnimationTargetSelector.Trim(),
                setter.PropertyName,
                setter.Value);
            if (!edit.Changed)
            {
                AnimationPlaybackStatus = edit.Error ?? "Could not apply playhead values.";
                return;
            }

            text = edit.Text;
        }

        themeFile.Text = text;
        RefreshWorkspaceAfterThemeFileChanges(themeFile);
        SelectThemeResource(selectedTheme.Key);
        AnimationPlaybackStatus = $"Applied playhead values at {AnimationCurrentTimePercent}% to {AnimationTargetSelector}.";
    }

    private void ApplyVisualAnimationTimeline(
        AnimationTargetOptionViewModel target,
        AnimationTimelineDefinition timeline,
        bool runPreview)
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            return;
        }

        var edit = _animationTimelineEditor.SetElementAnimation(xamlFile.Text, selector, timeline);
        if (!edit.Changed)
        {
            AnimationPlaybackStatus = edit.Error ?? "Could not apply animation timeline.";
            VisualEditorStatus = AnimationPlaybackStatus;
            return;
        }

        ApplyVisualEditorMutation(new XamlMutationResult(
            edit.Text,
            _visualMutationEngine.Analyze(edit.Text),
            Array.Empty<string>()));
        AnimationPlaybackStatus = $"Applied animation timeline to {target.Title}.";
        if (runPreview)
        {
            RunActiveDocument();
        }
    }

    private void ApplyDocumentStyleAnimationTimeline(
        AnimationTargetOptionViewModel target,
        AnimationTimelineDefinition timeline,
        bool runPreview)
    {
        if (ActiveXamlFile is not { } xamlFile)
        {
            return;
        }

        var edit = _animationTimelineEditor.SetDocumentStyleAnimation(
            xamlFile.Text,
            target.StyleIndex ?? -1,
            target.Selector,
            timeline);
        if (!edit.Changed)
        {
            AnimationPlaybackStatus = edit.Error ?? "Could not apply animation timeline.";
            VisualEditorStatus = AnimationPlaybackStatus;
            return;
        }

        ApplyVisualEditorMutation(new XamlMutationResult(
            edit.Text,
            _visualMutationEngine.Analyze(edit.Text),
            Array.Empty<string>()));
        SelectedAnimationTargetOption = AnimationTargetOptions.FirstOrDefault(option =>
            string.Equals(option.Id, target.Id, StringComparison.Ordinal));
        AnimationPlaybackStatus = $"Applied animation timeline to {target.Title}.";
        if (runPreview)
        {
            RunActiveDocument();
        }
    }

    private void ApplyThemeAnimationTimeline(
        AnimationTargetOptionViewModel target,
        AnimationTimelineDefinition timeline,
        bool runPreview)
    {
        if (ActiveProject is not { } project ||
            SelectedControlTheme is not { } selectedTheme ||
            project.FindFile(selectedTheme.FilePath) is not { } themeFile)
        {
            return;
        }

        var edit = _animationTimelineEditor.SetControlThemeAnimation(
            themeFile.Text,
            selectedTheme.Key,
            timeline);
        if (!edit.Changed)
        {
            AnimationPlaybackStatus = edit.Error ?? "Could not apply animation timeline.";
            ControlThemeStatus = AnimationPlaybackStatus;
            return;
        }

        themeFile.Text = edit.Text;
        RefreshWorkspaceAfterThemeFileChanges(themeFile);
        SelectThemeResource(selectedTheme.Key);
        SelectedAnimationTargetOption = AnimationTargetOptions.FirstOrDefault(option =>
            string.Equals(option.Id, target.Id, StringComparison.Ordinal));
        AnimationPlaybackStatus = $"Applied animation timeline to {target.Title}.";
        ControlThemeStatus = AnimationPlaybackStatus;
        if (runPreview)
        {
            RunActiveDocument();
        }
    }

    private AnimationTimelineDefinition CreateAnimationTimelineDefinition()
    {
        var selector = string.IsNullOrWhiteSpace(AnimationTargetSelector)
            ? "^"
            : AnimationTargetSelector.Trim();
        return new AnimationTimelineDefinition(
            selector,
            string.IsNullOrWhiteSpace(AnimationDurationText) ? "0:0:0.3" : AnimationDurationText.Trim(),
            AnimationDelayText.Trim(),
            AnimationIterationCountText.Trim(),
            string.IsNullOrWhiteSpace(AnimationPlaybackDirectionText) ? "Normal" : AnimationPlaybackDirectionText.Trim(),
            string.IsNullOrWhiteSpace(AnimationFillModeText) ? "Both" : AnimationFillModeText.Trim(),
            string.IsNullOrWhiteSpace(AnimationEasingText) ? "CubicEaseOut" : AnimationEasingText.Trim(),
            AnimationTimelineTracks
                .Where(static track => !string.IsNullOrWhiteSpace(track.PropertyName))
                .Select(track =>
                {
                    track.TargetSelector = selector;
                    return track.ToDefinition();
                })
                .ToArray());
    }

    private IEnumerable<(string PropertyName, string Value)> ResolveAnimationFrameSetters(int cuePercent)
    {
        var cue = Math.Clamp(cuePercent, 0, 100);
        foreach (var track in AnimationTimelineTracks)
        {
            if (string.IsNullOrWhiteSpace(track.PropertyName))
            {
                continue;
            }

            var frames = track.KeyFrames
                .OrderBy(static keyFrame => keyFrame.CuePercent)
                .ToArray();
            if (frames.Length == 0)
            {
                continue;
            }

            var first = frames[0];
            if (cue <= first.CuePercent)
            {
                yield return (track.PropertyName, first.Value);
                continue;
            }

            var last = frames[^1];
            if (cue >= last.CuePercent)
            {
                yield return (track.PropertyName, last.Value);
                continue;
            }

            var previous = frames.Last(keyFrame => keyFrame.CuePercent <= cue);
            var next = frames.First(keyFrame => keyFrame.CuePercent >= cue);
            if (ReferenceEquals(previous, next) ||
                previous.CuePercent == next.CuePercent)
            {
                yield return (track.PropertyName, previous.Value);
                continue;
            }

            var progress = (cue - previous.CuePercent) / (double)(next.CuePercent - previous.CuePercent);
            var value = TryInterpolateAnimationValue(previous.Value, next.Value, progress, out var interpolated)
                ? interpolated
                : previous.Value;
            yield return (track.PropertyName, value);
        }
    }

    public bool IsVisualAnimationPlayheadPreviewActive()
    {
        return SelectedAnimationTargetOption is { } target &&
               IsVisualAnimationTarget(target) &&
               AnimationTimelineTracks.Count > 0;
    }

    public IReadOnlyList<(string PropertyName, string Value)> GetAnimationFrameSettersForCurrentTime()
    {
        return ResolveAnimationFrameSetters(AnimationCurrentTimePercent).ToArray();
    }

    private void RecordAnimationKeyFrameFromVisualEdit(
        IEnumerable<XamlMutationRequest> requests,
        bool applyTimeline)
    {
        RecordAnimationKeyFrameFromVisualEdit(
            requests
                .Where(static request => request.Kind == XamlMutationKind.SetProperty &&
                                         !string.IsNullOrWhiteSpace(request.PropertyName))
                .Select(static request => (request.PropertyName!, request.Value ?? string.Empty)),
            applyTimeline);
    }

    private void RecordAnimationKeyFrameFromVisualEdit(
        IEnumerable<(string PropertyName, string Value)> setters,
        bool applyTimeline)
    {
        if (!AnimationRecordModeEnabled ||
            _isRecordingAnimationKeyFrame ||
            SelectedAnimationTargetOption is not { } target ||
            !IsVisualAnimationTarget(target))
        {
            return;
        }

        var recorded = false;
        try
        {
            _isRecordingAnimationKeyFrame = true;
            foreach (var setter in setters)
            {
                if (string.IsNullOrWhiteSpace(setter.PropertyName))
                {
                    continue;
                }

                AddOrUpdateAnimationKeyFrame(
                    setter.PropertyName,
                    setter.Value,
                    AnimationCurrentTimePercent,
                    AnimationKeySplineText);
                recorded = true;
            }

            if (recorded && applyTimeline)
            {
                ApplyAnimationTimeline();
            }
        }
        finally
        {
            _isRecordingAnimationKeyFrame = false;
        }
    }

    private string ResolveCurrentAnimationPropertyValue(string propertyName)
    {
        if (SelectedVisualEditorNode?.Element is { } element &&
            TryGetVisualEditorAttributeValue(element, propertyName, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return propertyName switch
        {
            "Opacity" => "1",
            "ScaleTransform.ScaleX" or "ScaleTransform.ScaleY" => "1",
            "RotateTransform.Angle" => "0",
            "TranslateTransform.X" or "TranslateTransform.Y" => "0",
            _ => string.Empty
        };
    }

    private static bool TryInterpolateAnimationValue(
        string from,
        string to,
        double progress,
        [NotNullWhen(true)] out string? value)
    {
        progress = Math.Clamp(progress, 0, 1);
        if (TryParseInvariantDouble(from, out var fromDouble) &&
            TryParseInvariantDouble(to, out var toDouble))
        {
            value = FormatDesignerDouble(fromDouble + (toDouble - fromDouble) * progress);
            return true;
        }

        if (TryParseDesignerThickness(from, out var fromThickness) &&
            TryParseDesignerThickness(to, out var toThickness))
        {
            value = FormatDesignerThickness(new DesignerThickness(
                fromThickness.Left + (toThickness.Left - fromThickness.Left) * progress,
                fromThickness.Top + (toThickness.Top - fromThickness.Top) * progress,
                fromThickness.Right + (toThickness.Right - fromThickness.Right) * progress,
                fromThickness.Bottom + (toThickness.Bottom - fromThickness.Bottom) * progress));
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseInvariantDouble(string value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
               !double.IsNaN(result) &&
               !double.IsInfinity(result);
    }

    private static bool TryParseDesignerThickness(string value, out DesignerThickness thickness)
    {
        var values = value
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : double.NaN)
            .ToArray();

        if (values.Any(static parsed => double.IsNaN(parsed) || double.IsInfinity(parsed)))
        {
            thickness = default;
            return false;
        }

        thickness = values.Length switch
        {
            1 => new DesignerThickness(values[0], values[0], values[0], values[0]),
            2 => new DesignerThickness(values[0], values[1], values[0], values[1]),
            4 => new DesignerThickness(values[0], values[1], values[2], values[3]),
            _ => default
        };
        return values.Length is 1 or 2 or 4;
    }

    private static void SortAnimationTrackKeyFrames(AnimationTimelineTrackViewModel track)
    {
        var sorted = track.KeyFrames
            .OrderBy(static frame => frame.CuePercent)
            .ToArray();
        track.KeyFrames.Clear();
        foreach (var frame in sorted)
        {
            track.KeyFrames.Add(frame);
        }
    }

    private static int FindAvailableCue(
        AnimationTimelineTrackViewModel track,
        int preferredCue,
        AnimationTimelineKeyFrameViewModel? ignoredFrame = null,
        int preferredDirection = 0)
    {
        var used = track.KeyFrames
            .Where(frame => !ReferenceEquals(frame, ignoredFrame))
            .Select(static frame => frame.CuePercent)
            .ToHashSet();
        var cue = Math.Clamp(preferredCue, 0, 100);
        if (!used.Contains(cue))
        {
            return cue;
        }

        for (var offset = 1; offset <= 100; offset++)
        {
            var next = cue + offset;
            var previous = cue - offset;
            if (preferredDirection < 0)
            {
                if (previous >= 0 && !used.Contains(previous))
                {
                    return previous;
                }

                if (next <= 100 && !used.Contains(next))
                {
                    return next;
                }

                continue;
            }

            if (next <= 100 && !used.Contains(next))
            {
                return next;
            }

            if (previous >= 0 && !used.Contains(previous))
            {
                return previous;
            }
        }

        return cue;
    }

    private static bool IsVisualAnimationTarget(AnimationTargetOptionViewModel target)
    {
        return string.Equals(target.Scope, "Visual", StringComparison.Ordinal);
    }

    private static bool IsDocumentStyleAnimationTarget(AnimationTargetOptionViewModel target)
    {
        return string.Equals(target.Scope, "Style", StringComparison.Ordinal);
    }

    private static bool IsThemeAnimationTarget(AnimationTargetOptionViewModel target)
    {
        return !IsVisualAnimationTarget(target) &&
               !IsDocumentStyleAnimationTarget(target);
    }

    private bool CanCreateCustomControlTheme()
    {
        return ActiveProject is not null && TryResolveControlThemeTemplateForCreate(
            out _,
            out _,
            out _);
    }

    private void CreateCustomControlTheme()
    {
        if (ActiveProject is not { } project)
        {
            return;
        }

        if (!TryResolveControlThemeTemplateForCreate(
                out var template,
                out var targetType,
                out var applyToSelectedControl))
        {
            ControlThemeStatus = "Select a preview control or Fluent source template first.";
            return;
        }

        if (template is null)
        {
            ControlThemeStatus = $"No Fluent ControlTheme template found for {targetType}.";
            return;
        }

        var themeKey = CreateUniqueControlThemeKey(project, targetType);
        var xaml = ControlThemeResourceBuilder.CreateResourceDictionary(template, themeKey);
        var isolateFromRuntimePreview = TemplateRequiresRuntimeIsolation(template.Xaml, project);
        var themeFile = _solutionFactory.AddControlThemeResource(
            project,
            themeKey,
            xaml,
            includeInRuntimePreview: !isolateFromRuntimePreview);
        var ownerFile = ActiveXamlFile;

        ClearControlThemeSearchFilter();
        SolutionExplorerNodes = Solution is { } solution
            ? BuildSolutionExplorer(solution)
            : new ObservableCollection<SolutionExplorerNodeViewModel>();
        RefreshControlThemes();

        var createdTheme = ControlThemes.FirstOrDefault(theme =>
            string.Equals(theme.Key, themeKey, StringComparison.Ordinal));
        if (createdTheme is not null)
        {
            SelectedControlTheme = createdTheme;
            if (applyToSelectedControl)
            {
                ApplyControlTheme(createdTheme);
            }
        }

        if (applyToSelectedControl && ownerFile is not null)
        {
            EnterThemeEditScope(ownerFile, themeKey);
        }

        if (isolateFromRuntimePreview)
        {
            OpenWorkspaceFileWithoutPreview(themeFile);
            ControlThemeStatus =
                $"Created isolated {themeKey} from {targetType}. External CLR theme types are kept out of runtime preview.";
        }
        else
        {
            OpenWorkspaceFile(themeFile);
            ControlThemeStatus = $"Created {themeKey} from {targetType}. {ControlThemes.Count} custom theme(s) available.";
        }
    }

    private bool TryResolveControlThemeTemplateForCreate(
        [NotNullWhen(true)] out FluentControlThemeTemplate? template,
        [NotNullWhen(true)] out string? targetType,
        out bool applyToSelectedControl)
    {
        if (TryGetSelectedControlThemeTargetType(out targetType))
        {
            template = _controlThemeCatalog.FindDefaultTemplate(targetType);
            if (template is not null)
            {
                applyToSelectedControl = true;
                return true;
            }
        }

        if (SelectedFluentControlThemeTemplate is not { } selectedTemplate)
        {
            template = null;
            targetType = null;
            applyToSelectedControl = false;
            return false;
        }

        template = _controlThemeCatalog.Templates.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, selectedTemplate.Key, StringComparison.Ordinal) &&
            string.Equals(FluentControlThemeCatalog.GetLocalName(candidate.TargetType), selectedTemplate.TargetType, StringComparison.Ordinal) &&
            string.Equals(candidate.SourcePath, selectedTemplate.SourcePath, StringComparison.OrdinalIgnoreCase));
        targetType = selectedTemplate.TargetType;
        applyToSelectedControl = false;
        return template is not null;
    }

    private void ClearControlThemeSearchFilter()
    {
        if (!string.IsNullOrWhiteSpace(ControlThemeSearchText))
        {
            ControlThemeSearchText = string.Empty;
        }
    }

    private void OpenWorkspaceFileWithoutPreview(InMemoryProjectFile file)
    {
        var wasOpeningSample = _openingSample;
        try
        {
            _openingSample = true;
            OpenWorkspaceFile(file);
        }
        finally
        {
            _openingSample = wasOpeningSample;
        }
    }

    private static bool TemplateRequiresRuntimeIsolation(string xaml, InMemoryProject? project = null)
    {
        try
        {
            var document = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            return document.Root is not null && document.Root
                .DescendantsAndSelf()
                .Attributes()
                .Where(static attribute => attribute.IsNamespaceDeclaration)
                .Select(static attribute => attribute.Value)
                .Any(value => IsExternalClrNamespace(value, project));
        }
        catch
        {
            return xaml.Contains("clr-namespace:Material.", StringComparison.Ordinal) ||
                   xaml.Contains("assembly=Material.", StringComparison.Ordinal);
        }
    }

    private static bool IsExternalClrNamespace(string namespaceValue, InMemoryProject? project)
    {
        if (!namespaceValue.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = namespaceValue
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Trim())
            .ToArray();
        var clrNamespace = parts[0]["clr-namespace:".Length..];
        var assembly = parts.FirstOrDefault(static part => part.StartsWith("assembly=", StringComparison.Ordinal));

        if (assembly is null)
        {
            return !IsKnownRuntimeClrNamespace(clrNamespace) &&
                   !IsProjectClrNamespace(clrNamespace, project);
        }

        var assemblyName = assembly["assembly=".Length..];
        return !IsKnownRuntimeAssembly(assemblyName) &&
               !IsProjectAssembly(assemblyName, project);
    }

    private static bool IsKnownRuntimeClrNamespace(string clrNamespace)
    {
        return clrNamespace.StartsWith("Avalonia.", StringComparison.Ordinal) ||
               clrNamespace.StartsWith("System", StringComparison.Ordinal) ||
               clrNamespace.StartsWith("XamlPlayground", StringComparison.Ordinal);
    }

    private static bool IsKnownRuntimeAssembly(string assemblyName)
    {
        return assemblyName.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("XamlPlayground", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectClrNamespace(string clrNamespace, InMemoryProject? project)
    {
        return IsNameOrChildNamespace(clrNamespace, project?.RootNamespace) ||
               IsNameOrChildNamespace(clrNamespace, project?.Name);
    }

    private static bool IsProjectAssembly(string assemblyName, InMemoryProject? project)
    {
        return MatchesProjectName(assemblyName, project?.Name) ||
               MatchesProjectName(assemblyName, project?.RootNamespace);
    }

    private static bool IsNameOrChildNamespace(string clrNamespace, string? rootNamespace)
    {
        return !string.IsNullOrWhiteSpace(rootNamespace) &&
               (clrNamespace.Equals(rootNamespace, StringComparison.Ordinal) ||
                clrNamespace.StartsWith($"{rootNamespace}.", StringComparison.Ordinal));
    }

    private static bool MatchesProjectName(string name, string? projectName)
    {
        return !string.IsNullOrWhiteSpace(projectName) &&
               name.Equals(projectName, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateUniqueControlThemeKey(InMemoryProject project, string targetType)
    {
        var index = 1;
        string key;
        do
        {
            key = $"My{targetType}Theme{index}";
            index++;
        }
        while (ControlThemes.Any(theme => string.Equals(theme.Key, key, StringComparison.Ordinal)) ||
               project.FindFile($"Themes/{key}.axaml") is not null);

        return key;
    }

    private bool CanApplyControlTheme(ControlThemeDefinitionViewModel? theme)
    {
        theme ??= SelectedControlTheme;
        return theme is not null &&
               ActiveXamlFile is { Kind: ProjectFileKind.Xaml } &&
               TryGetSelectedControlThemeTargetType(out var targetType) &&
               string.Equals(theme.TargetType, targetType, StringComparison.Ordinal);
    }

    private void ApplyControlTheme(ControlThemeDefinitionViewModel? theme)
    {
        theme ??= SelectedControlTheme;
        if (theme is null ||
            !TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            ControlThemeStatus = "Select a control and a theme first.";
            return;
        }

        ApplyVisualEditorMutation(_visualMutationEngine.SetProperty(
            xamlFile.Text,
            selector,
            "Theme",
            $"{{StaticResource {theme.Key}}}"));
        VisualEditorPropertyName = "Theme";
        VisualEditorPropertyValue = $"{{StaticResource {theme.Key}}}";
        ControlThemeStatus = $"Applied {theme.Key}.";
    }

    private bool CanRemoveControlTheme()
    {
        return ActiveXamlFile is { Kind: ProjectFileKind.Xaml } &&
               TryGetSelectedControlThemeTargetType(out _);
    }

    private void RemoveControlTheme()
    {
        if (!TryGetVisualEditingContext(out var xamlFile, out var selector))
        {
            ControlThemeStatus = "Select a control first.";
            return;
        }

        ApplyVisualEditorMutation(_visualMutationEngine.RemoveProperty(
            xamlFile.Text,
            selector,
            "Theme"));
        ControlThemeStatus = "Restored default theme.";
    }

    private void OpenSelectedControlTheme()
    {
        if (ActiveProject is null || SelectedControlTheme is null)
        {
            return;
        }

        var file = ActiveProject.FindFile(SelectedControlTheme.FilePath);
        if (file is not null)
        {
            if (ActiveXamlFile is { } ownerFile &&
                !ReferenceEquals(ownerFile, file))
            {
                EnterThemeEditScope(ownerFile, SelectedControlTheme.Key);
            }

            OpenWorkspaceFile(file);
        }
    }

    private void OpenSelectedThemeResource()
    {
        if (ActiveProject is null || SelectedThemeResource is null)
        {
            return;
        }

        var file = ActiveProject.FindFile(SelectedThemeResource.FilePath);
        if (file is null)
        {
            return;
        }

        if (SelectedThemeResource.ResourceType == "ControlTheme" &&
            ActiveXamlFile is { } ownerFile &&
            !ReferenceEquals(ownerFile, file))
        {
            EnterThemeEditScope(ownerFile, SelectedThemeResource.Key);
        }

        OpenWorkspaceFile(file);
    }

    private bool CanRenameSelectedThemeResource()
    {
        var newKey = ThemeResourceKeyEditText.Trim();
        return SelectedThemeResource is { } selectedResource &&
               !string.IsNullOrWhiteSpace(newKey) &&
               !string.Equals(selectedResource.Key, newKey, StringComparison.Ordinal) &&
               !ThemeResources.Any(resource =>
                   !ReferenceEquals(resource, selectedResource) &&
                   string.Equals(resource.Key, newKey, StringComparison.Ordinal) &&
                   ThemeResourceScopesConflict(resource, selectedResource));
    }

    private void RenameSelectedThemeResource()
    {
        if (ActiveProject is not { } project ||
            SelectedThemeResource is not { } selectedResource ||
            project.FindFile(selectedResource.FilePath) is not { } resourceFile)
        {
            return;
        }

        var newKey = ThemeResourceKeyEditText.Trim();
        var rename = ThemeResourceEditor.RenameResourceKey(resourceFile.Text, selectedResource.Key, newKey, selectedResource.Line);
        if (!rename.Changed)
        {
            ControlThemeStatus = rename.Error ?? $"Could not rename {selectedResource.Key}.";
            return;
        }

        resourceFile.Text = rename.Text;
        var renameReferences = !HasMultipleResourceDefinitions(selectedResource);
        if (renameReferences)
        {
            foreach (var file in project.GetXamlFiles())
            {
                file.Text = ThemeResourceEditor.RenameResourceReferences(file.Text, selectedResource.Key, newKey);
            }
        }

        RefreshWorkspaceAfterThemeFileChanges(resourceFile);
        SelectThemeResource(newKey, selectedResource.FilePath, selectedResource.Line, selectedResource.ThemeScope);
        ControlThemeStatus = renameReferences
            ? $"Renamed {selectedResource.Key} to {newKey}."
            : $"Renamed {selectedResource.Key} to {newKey}. References were left unchanged because the key has multiple definitions.";
    }

    private void DuplicateSelectedThemeResource()
    {
        if (ActiveProject is not { } project ||
            SelectedThemeResource is not { } selectedResource ||
            project.FindFile(selectedResource.FilePath) is not { } resourceFile)
        {
            return;
        }

        var duplicateKey = CreateUniqueThemeResourceKey(selectedResource.Key);
        var duplicate = ThemeResourceEditor.DuplicateResource(resourceFile.Text, selectedResource.Key, duplicateKey, selectedResource.Line);
        if (!duplicate.Changed)
        {
            ControlThemeStatus = duplicate.Error ?? $"Could not duplicate {selectedResource.Key}.";
            return;
        }

        resourceFile.Text = duplicate.Text;
        RefreshWorkspaceAfterThemeFileChanges(resourceFile);
        SelectThemeResource(duplicateKey, selectedResource.FilePath, selectedResource.Line, selectedResource.ThemeScope);
        ControlThemeStatus = $"Duplicated {selectedResource.Key} as {duplicateKey}.";
    }

    private void DeleteSelectedThemeResource()
    {
        if (ActiveProject is not { } project ||
            SelectedThemeResource is not { } selectedResource ||
            project.FindFile(selectedResource.FilePath) is not { } resourceFile)
        {
            return;
        }

        var plan = CreateThemeResourceDeletePlan(project, selectedResource, resourceFile);
        if (plan.Error is not null)
        {
            ControlThemeStatus = plan.Error;
            return;
        }

        if (plan.Plan is null)
        {
            return;
        }

        _pendingThemeResourceDeletePlan = plan.Plan;
        ThemeResourceDeleteDialogTitle = $"Delete {selectedResource.Key}";
        ThemeResourceDeleteDialogMessage =
            $"Review deletion of '{selectedResource.Key}'. This will update {plan.Plan.UpdatedFileCount} file(s), " +
            $"remove {plan.Plan.UsageCount} reference(s), and delete the resource definition.";
        ThemeResourceDeleteChanges = new ObservableCollection<ThemeResourceDeleteChangeViewModel>(
            plan.Plan.Changes.Select(static change => new ThemeResourceDeleteChangeViewModel(
                change.File.Path,
                change.Kind == ThemeResourceDeleteChangeKind.DeleteFile ? "Delete file" : "Update file",
                CountRemovedLines(change.BeforeText, change.AfterText),
                CountAddedLines(change.BeforeText, change.AfterText),
                CreateDiffPreview(change.BeforeText, change.AfterText))));
        SelectedThemeResourceDeleteChange = ThemeResourceDeleteChanges.FirstOrDefault();
        IsThemeResourceDeleteDialogOpen = true;
        ControlThemeStatus = $"Review delete changes for {selectedResource.Key}.";
        (ConfirmThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CancelThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private (ThemeResourceDeletePlan? Plan, string? Error) CreateThemeResourceDeletePlan(
        InMemoryProject project,
        ThemeResourceViewModel selectedResource,
        InMemoryProjectFile resourceFile)
    {
        var removeReferences = !HasMultipleResourceDefinitions(selectedResource);
        var usages = removeReferences
            ? _themeResourceAnalysis.References
                .Where(reference => string.Equals(reference.Key, selectedResource.Key, StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<ThemeResourceReference>();
        var afterTexts = project.GetXamlFiles()
            .ToDictionary(static file => file, static file => file.Text);

        if (removeReferences)
        {
            foreach (var file in project.GetXamlFiles())
            {
                afterTexts[file] = ThemeResourceEditor.RemoveResourceReferences(
                    afterTexts[file],
                    selectedResource.Key);
            }
        }

        var delete = ThemeResourceEditor.DeleteResource(afterTexts[resourceFile], selectedResource.Key, selectedResource.Line);
        if (!delete.Changed)
        {
            return (null, delete.Error ?? $"Could not delete {selectedResource.Key}.");
        }

        var removeResourceFile = delete.RemovedLastResource &&
                                 resourceFile.Path.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase);
        afterTexts[resourceFile] = removeResourceFile ? string.Empty : delete.Text;

        var changes = new List<ThemeResourceDeleteFileChange>();
        foreach (var file in project.GetXamlFiles())
        {
            if (ReferenceEquals(file, resourceFile) && removeResourceFile)
            {
                changes.Add(new ThemeResourceDeleteFileChange(
                    file,
                    file.Text,
                    string.Empty,
                    ThemeResourceDeleteChangeKind.DeleteFile));
                continue;
            }

            if (!string.Equals(file.Text, afterTexts[file], StringComparison.Ordinal))
            {
                changes.Add(new ThemeResourceDeleteFileChange(
                    file,
                    file.Text,
                    afterTexts[file],
                    ThemeResourceDeleteChangeKind.UpdateFile));
            }
        }

        if (changes.Count == 0)
        {
            return (null, $"No deletable resource named {selectedResource.Key} was found.");
        }

        return (new ThemeResourceDeletePlan(selectedResource.Key, usages.Length, changes), null);
    }

    private bool HasMultipleResourceDefinitions(ThemeResourceViewModel selectedResource)
    {
        return ThemeResources.Any(resource =>
            !ReferenceEquals(resource, selectedResource) &&
            string.Equals(resource.Key, selectedResource.Key, StringComparison.Ordinal));
    }

    private void ConfirmThemeResourceDelete()
    {
        if (ActiveProject is not { } project ||
            _pendingThemeResourceDeletePlan is not { } plan)
        {
            return;
        }

        InMemoryProjectFile? fileToOpen = null;
        foreach (var change in plan.Changes)
        {
            if (change.Kind == ThemeResourceDeleteChangeKind.DeleteFile)
            {
                project.Files.Remove(change.File);
                if (ReferenceEquals(ActiveXamlFile, change.File))
                {
                    ActiveXamlFile = project.GetXamlFiles().FirstOrDefault(file => file.Kind == ProjectFileKind.Xaml) ??
                                     project.GetXamlFiles().FirstOrDefault();
                    RefreshVisualEditingModel(updateSourceSelection: false);
                }

                continue;
            }

            change.File.Text = change.AfterText;
            fileToOpen ??= change.File;
        }

        var resourceKey = plan.ResourceKey;
        ClearThemeResourceDeleteDialog();
        RefreshWorkspaceAfterThemeFileChanges(fileToOpen);
        ControlThemeStatus = $"Deleted {resourceKey}.";
    }

    private void CancelThemeResourceDelete()
    {
        if (_pendingThemeResourceDeletePlan is { } plan)
        {
            ControlThemeStatus = $"Canceled deletion of {plan.ResourceKey}.";
        }
        else
        {
            ControlThemeStatus = "Canceled resource deletion.";
        }

        ClearThemeResourceDeleteDialog();
    }

    private void ClearThemeResourceDeleteDialog()
    {
        _pendingThemeResourceDeletePlan = null;
        IsThemeResourceDeleteDialogOpen = false;
        ThemeResourceDeleteDialogMessage = string.Empty;
        ThemeResourceDeleteChanges = new ObservableCollection<ThemeResourceDeleteChangeViewModel>();
        SelectedThemeResourceDeleteChange = null;
        (ConfirmThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CancelThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private static int CountRemovedLines(string beforeText, string afterText)
    {
        var diff = GetChangedLineRanges(beforeText, afterText);
        return Math.Max(0, diff.BeforeEnd - diff.BeforeStart);
    }

    private static int CountAddedLines(string beforeText, string afterText)
    {
        var diff = GetChangedLineRanges(beforeText, afterText);
        return Math.Max(0, diff.AfterEnd - diff.AfterStart);
    }

    private static string CreateDiffPreview(string beforeText, string afterText)
    {
        var beforeLines = SplitLines(beforeText);
        var afterLines = SplitLines(afterText);
        var diff = GetChangedLineRanges(beforeLines, afterLines);
        var lines = new List<string>();

        for (var i = diff.BeforeStart; i < diff.BeforeEnd; i++)
        {
            lines.Add($"- {beforeLines[i]}");
        }

        for (var i = diff.AfterStart; i < diff.AfterEnd; i++)
        {
            lines.Add($"+ {afterLines[i]}");
        }

        const int maxPreviewLines = 120;
        if (lines.Count > maxPreviewLines)
        {
            lines = lines.Take(maxPreviewLines).ToList();
            lines.Add("... diff truncated ...");
        }

        return lines.Count == 0
            ? "No text changes."
            : string.Join(Environment.NewLine, lines);
    }

    private static (int BeforeStart, int BeforeEnd, int AfterStart, int AfterEnd) GetChangedLineRanges(
        string beforeText,
        string afterText)
    {
        return GetChangedLineRanges(SplitLines(beforeText), SplitLines(afterText));
    }

    private static (int BeforeStart, int BeforeEnd, int AfterStart, int AfterEnd) GetChangedLineRanges(
        IReadOnlyList<string> beforeLines,
        IReadOnlyList<string> afterLines)
    {
        var prefix = 0;
        while (prefix < beforeLines.Count &&
               prefix < afterLines.Count &&
               string.Equals(beforeLines[prefix], afterLines[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        var beforeEnd = beforeLines.Count;
        var afterEnd = afterLines.Count;
        while (beforeEnd > prefix &&
               afterEnd > prefix &&
               string.Equals(beforeLines[beforeEnd - 1], afterLines[afterEnd - 1], StringComparison.Ordinal))
        {
            beforeEnd--;
            afterEnd--;
        }

        return (prefix, beforeEnd, prefix, afterEnd);
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private bool CanApplySelectedThemeResource()
    {
        return SelectedThemeResource is { ResourceType: "ControlTheme" } selectedResource &&
               ControlThemes.FirstOrDefault(theme =>
                   string.Equals(theme.Key, selectedResource.Key, StringComparison.Ordinal) &&
                   string.Equals(theme.FilePath, selectedResource.FilePath, StringComparison.OrdinalIgnoreCase)) is { } theme &&
               CanApplyControlTheme(theme);
    }

    private void ApplySelectedThemeResource()
    {
        if (SelectedThemeResource is not { ResourceType: "ControlTheme" } selectedResource)
        {
            ControlThemeStatus = "Select a ControlTheme resource first.";
            return;
        }

        var theme = ControlThemes.FirstOrDefault(theme =>
            string.Equals(theme.Key, selectedResource.Key, StringComparison.Ordinal) &&
            string.Equals(theme.FilePath, selectedResource.FilePath, StringComparison.OrdinalIgnoreCase));
        if (theme is null)
        {
            ControlThemeStatus = $"{selectedResource.Key} is not a custom ControlTheme.";
            return;
        }

        ApplyControlTheme(theme);
    }

    private void SelectThemeResource(string key)
    {
        SelectThemeResource(key, filePath: null, line: null, themeScope: null);
    }

    private void SelectThemeResource(string key, string? filePath, int? line, string? themeScope)
    {
        SelectedThemeResource =
            FilteredThemeResources.FirstOrDefault(resource =>
                string.Equals(resource.Key, key, StringComparison.Ordinal) &&
                (filePath is null || string.Equals(resource.FilePath, filePath, StringComparison.OrdinalIgnoreCase)) &&
                (line is null || resource.Line == line) &&
                (themeScope is null || string.Equals(resource.ThemeScope, themeScope, StringComparison.OrdinalIgnoreCase))) ??
            FilteredThemeResources.FirstOrDefault(resource => string.Equals(resource.Key, key, StringComparison.Ordinal)) ??
            ThemeResources.FirstOrDefault(resource => string.Equals(resource.Key, key, StringComparison.Ordinal));
    }

    private static bool ThemeResourceScopesConflict(ThemeResourceViewModel left, ThemeResourceViewModel right)
    {
        return string.Equals(left.ThemeScope, right.ThemeScope, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateUniqueThemeResourceKey(string sourceKey)
    {
        var baseKey = sourceKey.EndsWith("Copy", StringComparison.Ordinal)
            ? sourceKey
            : $"{sourceKey}Copy";
        var key = baseKey;
        var index = 2;
        while (ThemeResources.Any(resource => string.Equals(resource.Key, key, StringComparison.Ordinal)))
        {
            key = $"{baseKey}{index}";
            index++;
        }

        return key;
    }

    private void EnterThemeEditScope(InMemoryProjectFile ownerFile, string themeKey)
    {
        _themeEditScope = new ThemeEditScope(
            ownerFile,
            VisualEditorSourceSelectionFilePath,
            VisualEditorSourceSelectionStart,
            VisualEditorSourceSelectionLength,
            VisualEditorSourceSelectionStart,
            themeKey);
        IsThemeEditScopeActive = true;
        ThemeEditScopeBreadcrumb = $"{ownerFile.Path} > {themeKey}";
        (ReturnFromThemeEditScopeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void ReturnFromThemeEditScope()
    {
        if (_themeEditScope is not { } scope)
        {
            return;
        }

        _themeEditScope = null;
        IsThemeEditScopeActive = false;
        ThemeEditScopeBreadcrumb = string.Empty;
        OpenWorkspaceFile(scope.OwnerFile);
        if (!string.IsNullOrWhiteSpace(scope.SelectionFilePath))
        {
            SelectVisualEditorSourceRange(
                scope.SelectionFilePath,
                scope.SelectionStart,
                scope.SelectionLength,
                scope.CaretOffset);
        }

        (ReturnFromThemeEditScopeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private async Task ImportControlThemeFiles()
    {
        if (ActiveProject is not { } project || StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import theme file",
            FileTypeFilter = GetThemeResourceFileTypes(),
            AllowMultiple = true
        });

        if (files.Count == 0)
        {
            return;
        }

        var importedFiles = new List<InMemoryProjectFile>();
        foreach (var file in files)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var xaml = await reader.ReadToEndAsync();
                importedFiles.Add(_solutionFactory.AddImportedThemeResource(project, file.Name, xaml));
            }
            catch (Exception exception)
            {
                ControlThemeStatus = $"Failed to import {file.Name}: {exception.Message}";
                return;
            }
        }

        RefreshWorkspaceAfterThemeFileChanges(importedFiles.FirstOrDefault());
        var importedThemeCount = ControlThemeResourceBuilder.FindCustomThemes(
                importedFiles.Select(static file => (file.Path, file.Text)))
            .Count;
        ControlThemeStatus = importedThemeCount == 0
            ? $"Imported {importedFiles.Count} resource file(s). No custom ControlTheme was found."
            : $"Imported {importedThemeCount} control theme(s) from {importedFiles.Count} file(s).";
    }

    private async Task ExportSelectedControlTheme()
    {
        if (ActiveProject is null ||
            SelectedControlTheme is not { } selectedTheme ||
            ActiveProject.FindFile(selectedTheme.FilePath) is not { } themeFile ||
            StorageProvider is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export theme file",
            FileTypeChoices = GetThemeResourceFileTypes(),
            SuggestedFileName = themeFile.Name,
            DefaultExtension = themeFile.Extension.TrimStart('.') is { Length: > 0 } extension ? extension : "axaml",
            ShowOverwritePrompt = true
        });

        if (file is null)
        {
            return;
        }

        try
        {
            await WriteStorageFileTextAsync(file, themeFile.Text);
            ControlThemeStatus = $"Exported {selectedTheme.Key}.";
        }
        catch (Exception exception)
        {
            ControlThemeStatus = $"Failed to export {selectedTheme.Key}: {exception.Message}";
        }
    }

    private bool CanSaveControlThemeProject()
    {
        return ActiveProject is not null && GetControlThemeProjectFiles().Count > 0;
    }

    private async Task SaveControlThemeProject()
    {
        if (ActiveProject is not { } project || StorageProvider is null)
        {
            return;
        }

        var themeFiles = GetControlThemeProjectFiles();
        if (themeFiles.Count == 0)
        {
            ControlThemeStatus = "No custom theme files to save.";
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save theme project",
            FileTypeChoices = GetThemeProjectFileTypes(),
            SuggestedFileName = $"{project.Name}.xamltheme",
            DefaultExtension = "xamltheme",
            ShowOverwritePrompt = true
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var json = ThemeProjectStorage.Save(
                project.Name,
                themeFiles.Select(static themeFile => (themeFile.Path, themeFile.Text)));
            await WriteStorageFileTextAsync(file, json);
            ControlThemeStatus = $"Saved {themeFiles.Count} theme file(s) to theme project.";
        }
        catch (Exception exception)
        {
            ControlThemeStatus = $"Failed to save theme project: {exception.Message}";
        }
    }

    private async Task LoadControlThemeProject()
    {
        if (ActiveProject is not { } project || StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load theme project",
            FileTypeFilter = GetThemeProjectFileTypes(),
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
            var json = await reader.ReadToEndAsync();
            var themeProject = ThemeProjectStorage.Load(json);
            var loadedFileCount = ApplyControlThemeProject(
                project,
                themeProject,
                $"Theme project: {themeProject.Name}",
                updateSourceCatalog: false);
            ControlThemeStatus = $"Loaded {loadedFileCount} theme file(s) from {themeProject.Name}.";
        }
        catch (Exception exception)
        {
            ControlThemeStatus = $"Failed to load theme project: {exception.Message}";
        }
    }

    private async Task LoadControlThemeFolder()
    {
        if (ActiveProject is null || StorageProvider is null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Load theme folder",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        try
        {
            var source = await ThemeProjectSourceLoader.LoadFromStorageFolderAsync(folder);
            LoadControlThemeSource(source);
            ControlThemeStatus = $"Loaded {FluentControlThemeTemplates.Count} source template(s) from folder.";
        }
        catch (Exception exception)
        {
            ControlThemeStatus = $"Failed to load theme folder: {exception.Message}";
        }
    }

    private void LoadBundledFluentThemeProject()
    {
        if (ActiveProject is null)
        {
            return;
        }

        try
        {
            var source = ThemeProjectSourceLoader.LoadEmbeddedFluentThemeProject();
            LoadControlThemeSource(source);
            ControlThemeStatus = $"Loaded {FluentControlThemeTemplates.Count} bundled Fluent source template(s).";
        }
        catch (Exception exception)
        {
            ControlThemeStatus = $"Failed to load bundled Fluent theme: {exception.Message}";
        }
    }

    private bool CanLoadControlThemeRepository()
    {
        return ActiveProject is not null && !string.IsNullOrWhiteSpace(ControlThemeRepositoryUrl);
    }

    private async Task LoadControlThemeRepository()
    {
        if (ActiveProject is null || string.IsNullOrWhiteSpace(ControlThemeRepositoryUrl))
        {
            return;
        }

        var repositoryUrl = ControlThemeRepositoryUrl.Trim();
        ControlThemeStatus = $"Loading theme repository {repositoryUrl}...";

        try
        {
            var source = await ThemeProjectSourceLoader.LoadFromRemoteGitRepositoryAsync(repositoryUrl);
            LoadControlThemeSource(source);
            ControlThemeStatus = $"Loaded {FluentControlThemeTemplates.Count} source template(s) from repository.";
        }
        catch (Exception exception)
        {
            ControlThemeStatus = $"Failed to load theme repository: {exception.Message}";
        }
    }

    private void LoadControlThemeSource(ThemeProjectSource source)
    {
        _controlThemeCatalog = new FluentControlThemeCatalog(source);
        LoadFluentControlThemeTemplates();
        NotifyControlThemeCommandsChanged();
    }

    private int ApplyControlThemeProject(
        InMemoryProject project,
        ThemeProjectDocument themeProject,
        string sourceDescription,
        bool updateSourceCatalog)
    {
        var loadedFiles = themeProject.Files
            .Select(themeFile => _solutionFactory.AddOrUpdateResource(
                project,
                themeFile.Path,
                themeFile.Text,
                includeInRuntimePreview: !TemplateRequiresRuntimeIsolation(themeFile.Text, project)))
            .ToArray();

        if (updateSourceCatalog)
        {
            _controlThemeCatalog = new FluentControlThemeCatalog(themeProject, sourceDescription);
            LoadFluentControlThemeTemplates();
        }

        RefreshWorkspaceAfterThemeFileChanges(loadedFiles.FirstOrDefault());

        return loadedFiles.Length;
    }

    private IReadOnlyList<InMemoryProjectFile> GetControlThemeProjectFiles()
    {
        if (ActiveProject is not { } project)
        {
            return Array.Empty<InMemoryProjectFile>();
        }

        var customThemeFilePaths = ControlThemeResourceBuilder.FindCustomThemes(
                project.GetXamlFiles()
                    .Where(static file => file.Kind == ProjectFileKind.Resource)
                    .Select(static file => (file.Path, file.Text)))
            .Select(static theme => theme.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (customThemeFilePaths.Count == 0)
        {
            return Array.Empty<InMemoryProjectFile>();
        }

        return project.GetXamlFiles()
            .Where(static file => file.Kind == ProjectFileKind.Resource)
            .OrderBy(static file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void RefreshWorkspaceAfterThemeFileChanges(InMemoryProjectFile? fileToOpen)
    {
        SetSolutionExplorerNodes(Solution);
        RefreshControlThemes();

        if (fileToOpen is not null)
        {
            OpenWorkspaceFile(fileToOpen);
        }
        else if (CanPreviewXamlFile(ActiveXamlFile))
        {
            RunActiveDocument();
        }
    }

    private void NotifyControlThemeCommandsChanged()
    {
        (CreateCustomControlThemeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyControlThemeCommand as RelayCommand<ControlThemeDefinitionViewModel?>)?.NotifyCanExecuteChanged();
        (RemoveControlThemeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (OpenVisualEditorPropertyResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (OpenSelectedControlThemeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (OpenSelectedThemeResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RenameSelectedThemeResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DuplicateSelectedThemeResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DeleteSelectedThemeResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ConfirmThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CancelThemeResourceDeleteCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplySelectedThemeResourceCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ReturnFromThemeEditScopeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyThemeStateSetterCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyThemeTemplatePartSetterCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CreateThemeVariantPreviewCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ImportControlThemeFilesCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (ExportSelectedControlThemeCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SaveControlThemeProjectCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (LoadControlThemeProjectCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (LoadControlThemeFolderCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (LoadBundledFluentThemeProjectCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (LoadControlThemeRepositoryCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        NotifyAnimationCommandsChanged();
    }

    private void NotifyAnimationCommandsChanged()
    {
        (AddAnimationTrackCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (AddAnimationKeyFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (UpdateAnimationKeyFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CommitAnimationKeyFrameEditCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RemoveAnimationKeyFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyAnimationTimelineCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PreviewAnimationTimelineCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyAnimationFrameToTargetCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CaptureAnimationKeyFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DuplicateAnimationKeyFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SelectPreviousAnimationKeyFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SelectNextAnimationKeyFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (NudgeAnimationKeyFrameLeftCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (NudgeAnimationKeyFrameRightCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (NudgeAnimationKeyFrameLeftLargeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (NudgeAnimationKeyFrameRightLargeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SeekAnimationStartCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (SeekAnimationEndCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PlayAnimationTimelineCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (StopAnimationTimelineCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void NotifyAnimationTimelineChanged()
    {
        OnPropertyChanged(nameof(AnimationTimelineTracks));
        NotifyAnimationCommandsChanged();
    }

    private bool TryGetVisualEditingContext(
        out InMemoryProjectFile xamlFile,
        out XamlElementSelector selector)
    {
        xamlFile = ActiveXamlFile!;
        selector = _visualEditorSelectedSelector!;

        if (ActiveXamlFile is null || _visualEditorSelectedSelector is null)
        {
            VisualEditorStatus = "Select a XAML element first.";
            return false;
        }

        return true;
    }

    private bool TryGetSelectedVisualEditorElement(out XamlElementSnapshot element)
    {
        element = null!;

        if (_visualEditorDocument is null || _visualEditorSelectedSelector is null)
        {
            VisualEditorStatus = "Select a XAML element first.";
            return false;
        }

        var selected = _visualEditorDocument.Elements.FirstOrDefault(candidate =>
            Matches(candidate, _visualEditorSelectedSelector));
        if (selected is null)
        {
            VisualEditorStatus = "The selected XAML element is no longer available.";
            return false;
        }

        element = selected;
        return true;
    }

    private bool ShouldSuppressPreviewReloadForDiagnosticsMutation(InMemoryProjectFile file)
    {
        return _isApplyingDiagnosticsPropertyMutation &&
               ReferenceEquals(file, ActiveXamlFile);
    }

    private void SetDiagnosticsPreviewXamlFile(
        InMemoryProjectFile xamlFile,
        string previewSourceXaml,
        bool supportsSourceMutations = true)
    {
        ArgumentNullException.ThrowIfNull(xamlFile);
        ArgumentNullException.ThrowIfNull(previewSourceXaml);

        _diagnosticsPreviewXamlFile = xamlFile;
        _diagnosticsPreviewSourceXamlText = supportsSourceMutations ? previewSourceXaml : null;
        _diagnosticsAcceptedXamlText = supportsSourceMutations ? previewSourceXaml : null;
        _diagnosticsPreviewUsesGeneratedSource = !supportsSourceMutations;
    }

    private void SetDiagnosticsAcceptedXamlText(InMemoryProjectFile xamlFile)
    {
        ArgumentNullException.ThrowIfNull(xamlFile);

        if (ReferenceEquals(_diagnosticsPreviewXamlFile, xamlFile))
        {
            _diagnosticsAcceptedXamlText = xamlFile.Text;
        }
    }

    private void ClearDiagnosticsPreviewXamlFile()
    {
        _diagnosticsPreviewXamlFile = null;
        _diagnosticsPreviewSourceXamlText = null;
        _diagnosticsAcceptedXamlText = null;
        _diagnosticsPreviewUsesGeneratedSource = false;
    }

    private bool TryGetDiagnosticsPreviewXamlFileForEdit(
        [NotNullWhen(true)] out InMemoryProjectFile? xamlFile,
        [NotNullWhen(true)] out string? previewXaml)
    {
        xamlFile = null;
        previewXaml = null;

        if (ActiveXamlFile is not { } activeXamlFile)
        {
            VisualEditorStatus = "No XAML document selected.";
            return false;
        }

        if (_diagnosticsPreviewUsesGeneratedSource)
        {
            VisualEditorStatus = "Diagnostics property edits are not available for generated resource previews.";
            return false;
        }

        if (!ReferenceEquals(_diagnosticsPreviewXamlFile, activeXamlFile) ||
            _diagnosticsPreviewSourceXamlText is null ||
            !string.Equals(_diagnosticsAcceptedXamlText, activeXamlFile.Text, StringComparison.Ordinal))
        {
            VisualEditorStatus = "Diagnostics preview is stale. Run the active XAML document before editing diagnostics properties.";
            return false;
        }

        xamlFile = activeXamlFile;
        previewXaml = _diagnosticsPreviewSourceXamlText;
        return true;
    }

    private bool TryResolveVisualEditorPreviewControl(
        Control control,
        string xaml,
        [NotNullWhen(true)] out XamlDocumentSnapshot? document,
        [NotNullWhen(true)] out XamlElementSnapshot? element,
        out IReadOnlyList<string> diagnostics,
        bool allowTypeFallback = true)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(xaml);

        var visualNode = _visualTreeSnapshotService.Snapshot(control);
        var selection = _visualSelectionService.SelectVisual(
            xaml,
            visualNode,
            allowTypeFallback);
        document = selection.Document;
        element = selection.XamlElement;
        diagnostics = selection.Diagnostics;
        return selection.HasSelection && selection.XamlElement is not null;
    }

    private bool TryResolveDiagnosticsPreviewControl(
        Control control,
        InMemoryProjectFile xamlFile,
        string xaml,
        [NotNullWhen(true)] out XamlDocumentSnapshot? document,
        [NotNullWhen(true)] out XamlElementSnapshot? element,
        out IReadOnlyList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(xamlFile);
        ArgumentNullException.ThrowIfNull(xaml);

        document = null;
        element = null;

        var visualNode = _visualTreeSnapshotService.Snapshot(control);
        var sourcePath = NormalizeXamlSourcePath(visualNode.SourceUri);
        if (sourcePath is null)
        {
            diagnostics = new[]
            {
                "The selected visual does not have XAML source information and cannot be edited from diagnostics."
            };
            return false;
        }

        var activePath = NormalizeXamlSourcePath(xamlFile.Path);
        if (!string.Equals(sourcePath, activePath, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics = new[]
            {
                $"The selected visual belongs to '{sourcePath}', not the active XAML document '{activePath}'."
            };
            return false;
        }

        var selection = _visualSelectionService.SelectVisual(
            xaml,
            visualNode,
            allowTypeFallback: false);
        document = selection.Document;
        element = selection.XamlElement;
        diagnostics = selection.Diagnostics;
        return selection.HasSelection && selection.XamlElement is not null;
    }

    private static string? NormalizeXamlSourcePath(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var path = source.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            path = uri.IsFile
                ? uri.LocalPath
                : uri.AbsolutePath;
        }

        path = Uri.UnescapeDataString(path)
            .Replace('\\', '/')
            .Trim('/');
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private void ApplyVisualEditorMutation(XamlMutationResult result)
    {
        if (ActiveXamlFile is null)
        {
            return;
        }

        if (!result.Success)
        {
            VisualEditorStatus = string.Join(Environment.NewLine, result.Diagnostics);
            LastErrorMessage = VisualEditorStatus;
            return;
        }

        var selectedSelector = _visualEditorSelectedSelector;
        try
        {
            _isApplyingVisualEditorMutation = true;
            ActiveXamlFile.ApplyTextEdit(result.Text);
            _visualEditorDocument = result.Snapshot;
            _visualEditorSelectedSelector = selectedSelector;
            RefreshVisualEditingModel();
        }
        finally
        {
            _isApplyingVisualEditorMutation = false;
        }
    }

    private void LoadVisualEditorToolbox()
    {
        var builder = new ToolboxCatalogBuilder();
        builder.AddContributor(new ExtensionToolboxContributor(ExtensionHost.GetToolboxItemContributions()));
        var catalog = builder.Build(new ToolboxContext(new[]
        {
            typeof(Control).Assembly,
            typeof(MainViewModel).Assembly
        }));

        VisualEditorToolboxItems = new ObservableCollection<ToolboxItemDescriptor>(catalog.Items);
        FilterVisualEditorToolbox();
        SelectedVisualEditorToolboxItem = FilteredVisualEditorToolboxItems.FirstOrDefault(item =>
            string.Equals(item.TypeName, "Button", StringComparison.Ordinal));
    }

    private void FilterVisualEditorToolbox()
    {
        var query = VisualEditorToolboxSearch?.Trim();
        var items = VisualEditorToolboxItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(item =>
                item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.TypeName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredVisualEditorToolboxItems = new ObservableCollection<ToolboxItemDescriptor>(items);
    }

    private static IReadOnlyList<VisualEditorNodeViewModel> BuildVisualEditorNodes(XamlDocumentSnapshot document)
    {
        var nodes = document.Elements
            .Select(static element => new VisualEditorNodeViewModel(element))
            .ToDictionary(static node => GetPathKey(node.Element.Path), StringComparer.Ordinal);
        var roots = new List<VisualEditorNodeViewModel>();

        foreach (var node in nodes.Values.OrderBy(static node => node.Element.Path.Count))
        {
            if (node.Element.Path.Count == 0)
            {
                roots.Add(node);
                continue;
            }

            var parentPath = node.Element.Path.Take(node.Element.Path.Count - 1).ToArray();
            if (nodes.TryGetValue(GetPathKey(parentPath), out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The visual editor structure pane uses ProDataGrid's hierarchical model for design-time XAML metadata.")]
    private static HierarchicalModel<VisualEditorNodeViewModel> CreateVisualEditorStructureModel(
        ObservableCollection<VisualEditorNodeViewModel> roots)
    {
        var model = new HierarchicalModel<VisualEditorNodeViewModel>(
            new HierarchicalOptions<VisualEditorNodeViewModel>
            {
                ChildrenSelector = static node => node.Children,
                IsLeafSelector = static node => node.Children.Count == 0,
                AutoExpandRoot = true,
                MaxAutoExpandDepth = null,
                VirtualizeChildren = false,
                ItemPathSelector = static node => node.Element.Path
            });

        model.SetRoots(roots);
        return model;
    }

    private static IReadOnlyList<VisualEditorStructureRowViewModel> BuildVisualEditorStructureRows(
        IReadOnlyList<VisualEditorNodeViewModel> roots)
    {
        var rows = new List<VisualEditorStructureRowViewModel>();
        foreach (var root in roots)
        {
            AddVisualEditorStructureRows(root, 0, rows);
        }

        return rows;
    }

    private static void AddVisualEditorStructureRows(
        VisualEditorNodeViewModel node,
        int depth,
        ICollection<VisualEditorStructureRowViewModel> rows)
    {
        rows.Add(new VisualEditorStructureRowViewModel(node, depth));

        foreach (var child in node.Children)
        {
            AddVisualEditorStructureRows(child, depth + 1, rows);
        }
    }

    private static VisualEditorNodeViewModel? FindNode(
        IEnumerable<VisualEditorNodeViewModel> roots,
        XamlElementSelector? selector)
    {
        if (selector is null)
        {
            return null;
        }

        foreach (var root in roots)
        {
            if (Matches(root.Element, selector))
            {
                return root;
            }

            var child = FindNode(root.Children, selector);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static bool Matches(XamlElementSnapshot element, XamlElementSelector selector)
    {
        if (!string.IsNullOrWhiteSpace(selector.Name))
        {
            return string.Equals(element.Name, selector.Name, StringComparison.Ordinal);
        }

        if (selector.Path is { } path)
        {
            return GetPathKey(element.Path) == GetPathKey(path);
        }

        return !string.IsNullOrWhiteSpace(selector.TypeName) &&
               string.Equals(element.TypeName, selector.TypeName, StringComparison.Ordinal);
    }

    private static XamlElementSnapshot? FindElement(
        XamlDocumentSnapshot? document,
        XamlElementSelector? selector)
    {
        return document is null || selector is null
            ? null
            : document.Elements.FirstOrDefault(element => Matches(element, selector));
    }

    private static string GetPathKey(IEnumerable<int> path)
    {
        return string.Join("/", path);
    }

    private sealed record ThemeResourceDeletePlan(
        string ResourceKey,
        int UsageCount,
        IReadOnlyList<ThemeResourceDeleteFileChange> Changes)
    {
        public int UpdatedFileCount => Changes.Count;
    }

    private sealed record ThemeResourceDeleteFileChange(
        InMemoryProjectFile File,
        string BeforeText,
        string AfterText,
        ThemeResourceDeleteChangeKind Kind);

    private enum ThemeResourceDeleteChangeKind
    {
        UpdateFile,
        DeleteFile
    }
}
