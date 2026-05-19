using System.Collections.Generic;

namespace XamlPlayground.Extensions;

public sealed class BuiltInExtensionProvider : IExtensionProvider
{
    public const string ExtensionId = "xamlplayground.builtin";

    private static readonly ExtensionManifest s_manifest = CreateManifestCore();

    public static ExtensionManifest Manifest => s_manifest;

    public IEnumerable<ExtensionDescriptor> GetExtensions()
    {
        yield return new ExtensionDescriptor(Manifest, isBuiltIn: true);
    }

    private static ExtensionManifest CreateManifestCore()
    {
        return new ExtensionManifest(
            new ExtensionIdentity(
                ExtensionId,
                "XamlPlayground Built-ins",
                "1.0.0",
                "XamlPlayground",
                "First-party extension manifest for XamlPlayground contribution points."),
            new[]
            {
                ExtensionActivationEvents.OnStartupFinished
            },
            CreateContributions());
    }

    private static ExtensionContributions CreateContributions()
    {
        return new ExtensionContributions(
            commands: new[]
            {
                new CommandContribution("xaml.openSample", "Open Sample", "XAML"),
                new CommandContribution("xaml.openFile", "Open XAML File", "XAML"),
                new CommandContribution("xaml.saveFile", "Save XAML File", "XAML"),
                new CommandContribution("xaml.compilePreview", "Compile Preview", "Preview"),
                new CommandContribution("xaml.formatDocument", "Format Document", "Editor"),
                new CommandContribution("csharp.openFile", "Open C# File", "C#"),
                new CommandContribution("csharp.saveFile", "Save C# File", "C#"),
                new CommandContribution("file.new", "New File", "File"),
                new CommandContribution("workspace.newProject", "New Project", "Workspace"),
                new CommandContribution("workspace.addUserControl", "Add User Control", "Workspace"),
                new CommandContribution("workspace.addResourceDictionary", "Add Resource Dictionary", "Workspace"),
                new CommandContribution("workspace.importSolution", "Import Solution", "Workspace"),
                new CommandContribution("workspace.openFolder", "Open Folder", "Workspace"),
                new CommandContribution("workspace.openMsBuild", "Open MSBuild Workspace", "Workspace"),
                new CommandContribution("workspace.exportSolution", "Export Solution", "Workspace"),
                new CommandContribution("workspace.exportStandardSolutionFolder", "Export Standard Solution Folder", "Workspace"),
                new CommandContribution("workspace.saveFile", "Save Workspace File", "Workspace"),
                new CommandContribution("workspace.saveAll", "Save All Workspace Files", "Workspace"),
                new CommandContribution("workbench.toggleTheme", "Toggle Theme", "Workbench"),
                new CommandContribution("designer.refresh", "Refresh Designer", "Designer"),
                new CommandContribution("designer.applyProperty", "Apply Property", "Designer"),
                new CommandContribution("designer.openPropertyResource", "Open Property Resource", "Designer"),
                new CommandContribution("designer.insertToolboxItem", "Insert Toolbox Item", "Designer"),
                new CommandContribution("designer.wrapSelection", "Wrap Selection", "Designer"),
                new CommandContribution("designer.unwrapSelection", "Unwrap Selection", "Designer"),
                new CommandContribution("designer.duplicateElement", "Duplicate Element", "Designer"),
                new CommandContribution("designer.deleteElement", "Delete Element", "Designer"),
                new CommandContribution("theme.openResourceEditor", "Open Resource Editor", "Theme"),
                new CommandContribution("theme.openControlThemeEditor", "Open Control Theme Editor", "Theme"),
                new CommandContribution("theme.createControlTheme", "Create Control Theme", "Theme"),
                new CommandContribution("theme.applyControlTheme", "Apply Control Theme", "Theme"),
                new CommandContribution("theme.removeControlTheme", "Remove Control Theme", "Theme"),
                new CommandContribution("theme.openSelectedControlTheme", "Open Selected Control Theme", "Theme"),
                new CommandContribution("theme.openSelectedResource", "Open Selected Resource", "Theme"),
                new CommandContribution("theme.renameSelectedResource", "Rename Selected Resource", "Theme"),
                new CommandContribution("theme.duplicateSelectedResource", "Duplicate Selected Resource", "Theme"),
                new CommandContribution("theme.deleteSelectedResource", "Delete Selected Resource", "Theme"),
                new CommandContribution("theme.applySelectedResource", "Apply Selected Resource", "Theme"),
                new CommandContribution("theme.createVariantPreview", "Create Variant Preview", "Theme"),
                new CommandContribution("theme.importControlThemeFiles", "Import Control Theme Files", "Theme"),
                new CommandContribution("theme.exportSelectedControlTheme", "Export Selected Control Theme", "Theme"),
                new CommandContribution("theme.saveProject", "Save Theme Project", "Theme"),
                new CommandContribution("theme.loadProject", "Load Theme Project", "Theme"),
                new CommandContribution("theme.loadFolder", "Load Theme Folder", "Theme"),
                new CommandContribution("theme.loadBundledFluentProject", "Load Bundled Fluent Theme Project", "Theme"),
                new CommandContribution("theme.loadRepository", "Load Theme Repository", "Theme"),
                new CommandContribution("animation.openTimeline", "Open Timeline", "Animation"),
                new CommandContribution("animation.addTrack", "Add Track", "Animation"),
                new CommandContribution("animation.addKeyFrame", "Add Key Frame", "Animation"),
                new CommandContribution("animation.updateKeyFrame", "Update Key Frame", "Animation"),
                new CommandContribution("animation.removeKeyFrame", "Remove Key Frame", "Animation"),
                new CommandContribution("animation.applyTimeline", "Apply Timeline", "Animation"),
                new CommandContribution("animation.previewTimeline", "Preview Timeline", "Animation"),
                new CommandContribution("animation.captureKeyFrame", "Capture Key Frame", "Animation"),
                new CommandContribution("animation.playTimeline", "Play Timeline", "Animation"),
                new CommandContribution("animation.stopTimeline", "Stop Timeline", "Animation"),
                new CommandContribution("animation.seekStart", "Seek Start", "Animation"),
                new CommandContribution("animation.seekEnd", "Seek End", "Animation"),
                new CommandContribution("diagnostics.openCombinedTree", "Open Diagnostics Tree", "Diagnostics"),
                new CommandContribution("diagnostics.refreshInspectors", "Refresh Inspectors", "Diagnostics"),
                new CommandContribution("diagnostics.openStyleSource", "Open Style Source", "Diagnostics"),
                new CommandContribution("diagnostics.openBindingSource", "Open Binding Source", "Diagnostics"),
                new CommandContribution("diagnostics.openResourceSource", "Open Resource Source", "Diagnostics"),
                new CommandContribution("diagnostics.applyStyleEditor", "Apply Style Editor", "Diagnostics"),
                new CommandContribution("diagnostics.createStyleFromSelectedElement", "Create Style From Selection", "Diagnostics"),
                new CommandContribution("diagnostics.buildStyleSelector", "Build Style Selector", "Diagnostics"),
                new CommandContribution("diagnostics.refreshStylePreview", "Refresh Style Preview", "Diagnostics"),
                new CommandContribution("diagnostics.buildBindingMarkup", "Build Binding Markup", "Diagnostics"),
                new CommandContribution("diagnostics.applyBindingEditor", "Apply Binding Editor", "Diagnostics"),
                new CommandContribution("diagnostics.applyResourceEditor", "Apply Resource Editor", "Diagnostics"),
                new CommandContribution("diagnostics.createResource", "Create Resource", "Diagnostics"),
                new CommandContribution("workbench.restoreDockTools", "Restore Dock Tools", "Workbench"),
                new CommandContribution("workbench.toggleTool.SolutionExplorer", "Toggle Solution Explorer", "Workbench"),
                new CommandContribution("workbench.toggleTool.MsBuildWorkspace", "Toggle MSBuild Workspace", "Workbench"),
                new CommandContribution("workbench.toggleTool.VisualStructure", "Toggle Structure", "Workbench"),
                new CommandContribution("workbench.toggleTool.VisualToolbox", "Toggle Toolbox", "Workbench"),
                new CommandContribution("workbench.toggleTool.Preview", "Toggle Preview", "Workbench"),
                new CommandContribution("workbench.toggleTool.VisualProperties", "Toggle Properties", "Workbench"),
                new CommandContribution("workbench.toggleTool.VisualAnimations", "Toggle Animations", "Workbench"),
                new CommandContribution("workbench.toggleTool.StylesInspector", "Toggle Styles", "Workbench"),
                new CommandContribution("workbench.toggleTool.BindingsInspector", "Toggle Bindings", "Workbench"),
                new CommandContribution("workbench.toggleTool.ResourcesInspector", "Toggle Resources", "Workbench"),
                new CommandContribution("workbench.toggleTool.ControlThemes", "Toggle Themes", "Workbench"),
                new CommandContribution("workbench.toggleTool.AnimationTimelineSheet", "Toggle Timeline", "Workbench"),
                new CommandContribution("workbench.toggleTool.StyleEditor", "Toggle Style Editor", "Workbench"),
                new CommandContribution("workbench.toggleTool.BindingEditor", "Toggle Binding Editor", "Workbench"),
                new CommandContribution("workbench.toggleTool.ResourceEditor", "Toggle Resource Editor", "Workbench"),
                new CommandContribution("workbench.toggleTool.DiagnosticsCombinedTree", "Toggle Combined Tree", "Workbench"),
                new CommandContribution("workbench.toggleTool.DiagnosticsLogicalTree", "Toggle Logical Tree", "Workbench"),
                new CommandContribution("workbench.toggleTool.DiagnosticsVisualTree", "Toggle Visual Tree", "Workbench"),
                new CommandContribution("workbench.toggleTool.DiagnosticsEvents", "Toggle Events", "Workbench"),
                new CommandContribution("workbench.toggleTool.DiagnosticsResources", "Toggle Diagnostic Resources", "Workbench"),
                new CommandContribution("workbench.toggleTool.DiagnosticsAssets", "Toggle Assets", "Workbench"),
                new CommandContribution("workbench.toggleTool.Errors", "Toggle Errors", "Workbench"),
                new CommandContribution("workbench.applyPerspective.Default", "Default Workspace", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Wysiwyg", "WYSIWYG Editor", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Structure", "Structure Focus", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Diagnostics", "Dev Tools Diagnostics", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Animation", "Animation Editing", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Theme", "Theme Editing", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Bindings", "Bindings and Resources", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Code", "Code and Errors", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Preview", "Preview Review", "Perspective"),
                new CommandContribution("workbench.applyPerspective.Professional", "Professional Designer", "Perspective")
            },
            menus: new[]
            {
                new MenuContribution("menubar.file", "workspace.openFolder", "1_workspace", order: 10),
                new MenuContribution("menubar.file", "workspace.openMsBuild", "1_workspace", order: 20),
                new MenuContribution("menubar.xaml", "xaml.openSample", "1_samples", order: 10),
                new MenuContribution("editor/title", "xaml.compilePreview", "1_preview", order: 10),
                new MenuContribution("editor/context", "xaml.formatDocument", "2_edit", order: 20),
                new MenuContribution("toolbox/theme", "theme.openResourceEditor", "1_tools", order: 10),
                new MenuContribution("toolbox/theme", "theme.openControlThemeEditor", "1_tools", order: 20),
                new MenuContribution("toolbox/animation", "animation.openTimeline", "1_tools", order: 10),
                new MenuContribution("toolbox/diagnostics", "diagnostics.openCombinedTree", "1_tools", order: 10)
            },
            views: new[]
            {
                new ViewToolContribution("SolutionExplorer", "Solution Explorer", "left"),
                new ViewToolContribution("MsBuildWorkspace", "MSBuild Workspace", "left"),
                new ViewToolContribution("VisualStructure", "Structure", "left"),
                new ViewToolContribution("VisualToolbox", "Toolbox", "left"),
                new ViewToolContribution("Preview", "Preview", "preview"),
                new ViewToolContribution("VisualProperties", "Properties", "right"),
                new ViewToolContribution("VisualAnimations", "Animations", "right"),
                new ViewToolContribution("StylesInspector", "Styles", "right"),
                new ViewToolContribution("BindingsInspector", "Bindings", "right"),
                new ViewToolContribution("ResourcesInspector", "Resources", "right"),
                new ViewToolContribution("ControlThemes", "Themes", "right"),
                new ViewToolContribution("AnimationTimelineSheet", "Timeline", "bottom"),
                new ViewToolContribution("StyleEditor", "Style Editor", "bottom"),
                new ViewToolContribution("BindingEditor", "Binding Editor", "bottom"),
                new ViewToolContribution("ResourceEditor", "Resource Editor", "bottom"),
                new ViewToolContribution("DiagnosticsCombinedTree", "Combined Tree", "bottom"),
                new ViewToolContribution("DiagnosticsLogicalTree", "Logical Tree", "bottom"),
                new ViewToolContribution("DiagnosticsVisualTree", "Visual Tree", "bottom"),
                new ViewToolContribution("DiagnosticsEvents", "Events", "bottom"),
                new ViewToolContribution("DiagnosticsResources", "Resources", "bottom"),
                new ViewToolContribution("DiagnosticsAssets", "Assets", "bottom"),
                new ViewToolContribution("Errors", "Errors", "bottom")
            },
            perspectives: new[]
            {
                new PerspectiveContribution(
                    "Default",
                    "Default Workspace",
                    new[]
                    {
                        "Preview",
                        "SolutionExplorer",
                        "MsBuildWorkspace",
                        "VisualStructure",
                        "VisualProperties",
                        "VisualToolbox",
                        "VisualAnimations",
                        "StylesInspector",
                        "BindingsInspector",
                        "ResourcesInspector",
                        "ControlThemes",
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
                    }),
                new PerspectiveContribution(
                    "Wysiwyg",
                    "WYSIWYG Editor",
                    new[]
                    {
                        "VisualToolbox",
                        "SolutionExplorer",
                        "VisualStructure",
                        "Preview",
                        "VisualProperties",
                        "StylesInspector",
                        "ResourcesInspector",
                        "ControlThemes",
                        "VisualAnimations",
                        "AnimationTimelineSheet",
                        "StyleEditor",
                        "Errors"
                    }),
                new PerspectiveContribution(
                    "Structure",
                    "Structure Focus",
                    new[]
                    {
                        "VisualStructure",
                        "SolutionExplorer",
                        "Preview",
                        "VisualProperties",
                        "BindingsInspector",
                        "ResourcesInspector",
                        "StylesInspector",
                        "DiagnosticsCombinedTree",
                        "DiagnosticsLogicalTree",
                        "DiagnosticsVisualTree",
                        "Errors"
                    }),
                new PerspectiveContribution(
                    "Diagnostics",
                    "Dev Tools Diagnostics",
                    new[]
                    {
                        "DiagnosticsCombinedTree",
                        "DiagnosticsLogicalTree",
                        "DiagnosticsVisualTree",
                        "Preview",
                        "DiagnosticsEvents",
                        "DiagnosticsResources",
                        "DiagnosticsAssets",
                        "Errors",
                        "MsBuildWorkspace"
                    }),
                new PerspectiveContribution(
                    "Animation",
                    "Animation Editing",
                    new[]
                    {
                        "VisualToolbox",
                        "VisualStructure",
                        "SolutionExplorer",
                        "Preview",
                        "VisualAnimations",
                        "VisualProperties",
                        "StylesInspector",
                        "AnimationTimelineSheet",
                        "StyleEditor",
                        "Errors"
                    }),
                new PerspectiveContribution(
                    "Theme",
                    "Theme Editing",
                    new[]
                    {
                        "ControlThemes",
                        "SolutionExplorer",
                        "Preview",
                        "StylesInspector",
                        "ResourcesInspector",
                        "VisualProperties",
                        "VisualStructure",
                        "StyleEditor",
                        "ResourceEditor",
                        "Errors"
                    }),
                new PerspectiveContribution(
                    "Bindings",
                    "Bindings and Resources",
                    new[]
                    {
                        "VisualStructure",
                        "SolutionExplorer",
                        "Preview",
                        "BindingsInspector",
                        "ResourcesInspector",
                        "VisualProperties",
                        "StylesInspector",
                        "BindingEditor",
                        "ResourceEditor",
                        "DiagnosticsEvents",
                        "Errors"
                    }),
                new PerspectiveContribution(
                    "Code",
                    "Code and Errors",
                    new[]
                    {
                        "SolutionExplorer",
                        "MsBuildWorkspace",
                        "Preview",
                        "VisualStructure",
                        "BindingsInspector",
                        "ResourcesInspector",
                        "Errors",
                        "DiagnosticsEvents",
                        "DiagnosticsResources"
                    }),
                new PerspectiveContribution(
                    "Preview",
                    "Preview Review",
                    new[]
                    {
                        "SolutionExplorer",
                        "Preview",
                        "VisualProperties",
                        "VisualStructure",
                        "StylesInspector",
                        "ResourcesInspector",
                        "Errors"
                    }),
                new PerspectiveContribution(
                    "Professional",
                    "Professional Designer",
                    new[]
                    {
                        "SolutionExplorer",
                        "VisualStructure",
                        "VisualToolbox",
                        "VisualProperties",
                        "StylesInspector",
                        "BindingsInspector",
                        "ResourcesInspector",
                        "ControlThemes",
                        "VisualAnimations",
                        "Preview",
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
                    })
            },
            previewProviders: new[]
            {
                new PreviewProviderContribution("preview.xaml.instant", "Instant XAML Preview", new[] { "*.axaml", "*.xaml" }, priority: 200),
                new PreviewProviderContribution("preview.xaml.isolated", "Isolated XAML Preview", new[] { "*.axaml", "*.xaml" }, priority: 100),
                new PreviewProviderContribution("preview.workspace.msbuild", "MSBuild Workspace Preview", new[] { "*.csproj", "*.sln" }, priority: 50)
            },
            editorFeatures: new[]
            {
                new EditorFeatureContribution("editor.xaml.completion", "XAML Completion", "xaml", "completion"),
                new EditorFeatureContribution("editor.xaml.quickInfo", "XAML Quick Info", "xaml", "hover"),
                new EditorFeatureContribution("editor.xaml.contextActions", "XAML Context Actions", "xaml", "codeAction"),
                new EditorFeatureContribution("editor.xaml.inlinePreview", "Inline Preview", "xaml", "inline"),
                new EditorFeatureContribution("editor.csharp.completion", "C# Completion", "csharp", "completion"),
                new EditorFeatureContribution("editor.csharp.quickInfo", "C# Quick Info", "csharp", "hover")
            },
            animationProviders: new[]
            {
                new AnimationProviderContribution("animation.timeline", "Timeline Animation Editor", "style"),
                new AnimationProviderContribution("animation.transitions", "Transition Editor", "control"),
                new AnimationProviderContribution("animation.keyframes", "Key Frame Editor", "property")
            },
            themeResourceEditors: new[]
            {
                new ThemeResourceEditorContribution("theme.resources", "Resource Dictionary Editor", "resourceDictionary"),
                new ThemeResourceEditorContribution("theme.controlThemes", "Control Theme Editor", "controlTheme"),
                new ThemeResourceEditorContribution("theme.palette", "Palette Editor", "palette"),
                new ThemeResourceEditorContribution("theme.resourceReferences", "Resource Reference Picker", "resourceReference")
            },
            workspaceFeatures: new[]
            {
                new WorkspaceFeatureContribution("workspace.inMemory", "In-memory Workspace", "workspaceProvider"),
                new WorkspaceFeatureContribution("workspace.msbuild", "MSBuild Workspace", "workspaceProvider"),
                new WorkspaceFeatureContribution("workspace.standardSolutionExport", "Standard Solution Export", "storageProvider")
            },
            diagnosticProviders: new[]
            {
                new DiagnosticProviderContribution("diagnostics.xaml", "XAML Diagnostics", "xaml"),
                new DiagnosticProviderContribution("diagnostics.bindings", "Binding Diagnostics", "binding"),
                new DiagnosticProviderContribution("diagnostics.resources", "Resource Diagnostics", "resource"),
                new DiagnosticProviderContribution("diagnostics.devTools", "DevTools Diagnostics", "runtime")
            },
            visualEditorFeatures: new[]
            {
                new VisualEditorFeatureContribution("designer.toolbox", "Visual Toolbox", "toolbox"),
                new VisualEditorFeatureContribution("designer.structure", "Visual Structure", "structure"),
                new VisualEditorFeatureContribution("designer.properties", "Visual Properties", "propertyEditor"),
                new VisualEditorFeatureContribution("designer.mutations", "XAML Mutation Engine", "documentMutation"),
                new VisualEditorFeatureContribution("designer.resourcePicker", "Resource Picker", "resourceReference")
            },
            toolboxItems: new[]
            {
                new ToolboxItemContribution(
                    "builtin.toolbox.snippet.gridTwoRows",
                    "Grid: 2 Rows",
                    "Layout snippets",
                    "GridSnippet",
                    "https://github.com/avaloniaui",
                    string.Empty,
                    "<Grid RowDefinitions=\"Auto,*\"><TextBlock Text=\"Header\" /><Border Grid.Row=\"1\" /></Grid>",
                    new Dictionary<string, string> { ["kind"] = "snippet" }),
                new ToolboxItemContribution(
                    "builtin.toolbox.snippet.stackPanelVertical",
                    "StackPanel: Vertical",
                    "Layout snippets",
                    "StackPanelSnippet",
                    "https://github.com/avaloniaui",
                    string.Empty,
                    "<StackPanel Spacing=\"8\"><TextBlock Text=\"Item\" /><Button Content=\"Action\" /></StackPanel>",
                    new Dictionary<string, string> { ["kind"] = "snippet" }),
                new ToolboxItemContribution(
                    "builtin.toolbox.snippet.resourceButton",
                    "Button: Resource Background",
                    "Resource snippets",
                    "ResourceButtonSnippet",
                    "https://github.com/avaloniaui",
                    string.Empty,
                    "<Button Content=\"Action\" Background=\"{DynamicResource AccentBrush}\" />",
                    new Dictionary<string, string> { ["kind"] = "snippet" })
            });
    }
}
