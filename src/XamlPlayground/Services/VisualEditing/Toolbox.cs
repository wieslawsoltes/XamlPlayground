using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;

namespace XamlPlayground.Services.VisualEditing;

public sealed record ToolboxItemDescriptor(
    string Id,
    string DisplayName,
    string Category,
    string TypeName,
    string XmlNamespace,
    string AssemblyName,
    string DefaultXaml,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ToolboxContext(
    IReadOnlyList<Assembly> Assemblies,
    string DefaultXmlNamespace = "https://github.com/avaloniaui");

public interface IToolboxContributor
{
    IEnumerable<ToolboxItemDescriptor> GetItems(ToolboxContext context);
}

public sealed class ToolboxCatalog
{
    public ToolboxCatalog(IEnumerable<ToolboxItemDescriptor> items)
    {
        Items = items
            .OrderBy(static item => item.Category, StringComparer.Ordinal)
            .ThenBy(static item => item.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<ToolboxItemDescriptor> Items { get; }
}

public sealed class ToolboxCatalogBuilder
{
    private readonly List<IToolboxContributor> _contributors = new();

    public ToolboxCatalogBuilder()
    {
        AddContributor(new ReflectionToolboxContributor());
    }

    public void AddContributor(IToolboxContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        _contributors.Add(contributor);
    }

    public ToolboxCatalog Build(ToolboxContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = _contributors
            .SelectMany(contributor => contributor.GetItems(context))
            .GroupBy(static item => item.Id, StringComparer.Ordinal)
            .Select(static group => group.First());

        return new ToolboxCatalog(items);
    }
}

public sealed class ReflectionToolboxContributor : IToolboxContributor
{
    public IEnumerable<ToolboxItemDescriptor> GetItems(ToolboxContext context)
    {
        foreach (var assembly in context.Assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (!IsToolboxControl(type))
                {
                    continue;
                }

                yield return CreateItem(type, context.DefaultXmlNamespace);
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic toolbox discovery is an explicit visual-editor extension point.")]
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type is not null)!;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Toolbox discovery inspects public constructors only to decide whether a control can be instantiated by XAML.")]
    private static bool IsToolboxControl(Type type)
    {
        return typeof(Control).IsAssignableFrom(type) &&
               !type.IsAbstract &&
               type.IsPublic &&
               type.GetConstructor(Type.EmptyTypes) is not null;
    }

    private static string GetDefaultAttributes(Type type)
    {
        if (typeof(Button).IsAssignableFrom(type))
        {
            return $" Content=\"{type.Name}\"";
        }
        if (typeof(CheckBox).IsAssignableFrom(type))
        {
            return $" Content=\"{type.Name}\"";
        }
        if (typeof(RadioButton).IsAssignableFrom(type))
        {
            return $" Content=\"{type.Name}\"";
        }
        if (typeof(Label).IsAssignableFrom(type))
        {
            return $" Content=\"{type.Name}\"";
        }
        if (typeof(TextBlock).IsAssignableFrom(type))
        {
            return $" Text=\"{type.Name}\"";
        }
        if (typeof(TextBox).IsAssignableFrom(type))
        {
            return $" Text=\"{type.Name}\"";
        }
        if (type.Name == "HeaderedContentControl" || (type.BaseType != null && type.BaseType.Name == "HeaderedContentControl"))
        {
            return $" Header=\"{type.Name}\"";
        }
        if (type.Name == "TabItem")
        {
            return " Header=\"TabItem\"";
        }
        if (type.Name == "Expander")
        {
            return " Header=\"Expander\"";
        }
        return string.Empty;
    }

    private static ToolboxItemDescriptor CreateItem(Type type, string defaultXmlNamespace)
    {
        var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        var xmlNamespace = IsAvaloniaControl(type)
            ? defaultXmlNamespace
            : $"clr-namespace:{type.Namespace};assembly={assemblyName}";
        var elementName = type.Name;
        var attributes = GetDefaultAttributes(type);
        var defaultXaml = IsAvaloniaControl(type)
            ? $"<{elementName}{attributes} />"
            : $"<local:{elementName}{attributes} />";

        return new ToolboxItemDescriptor(
            $"{type.AssemblyQualifiedName}",
            SplitName(elementName),
            GetCategory(type),
            elementName,
            xmlNamespace,
            assemblyName,
            defaultXaml,
            new Dictionary<string, string>
            {
                ["clrType"] = type.FullName ?? type.Name
            });
    }

    private static bool IsAvaloniaControl(Type type)
    {
        return string.Equals(type.Namespace, "Avalonia.Controls", StringComparison.Ordinal) ||
               type.Namespace?.StartsWith("Avalonia.Controls.", StringComparison.Ordinal) == true;
    }

    private static string GetCategory(Type type)
    {
        if (typeof(Panel).IsAssignableFrom(type))
        {
            return "Layout";
        }

        if (typeof(Button).IsAssignableFrom(type) ||
            type.Name.Contains("TextBox", StringComparison.Ordinal) ||
            type.Name.Contains("Picker", StringComparison.Ordinal))
        {
            return "Input";
        }

        if (typeof(TextBlock).IsAssignableFrom(type) ||
            type.Name.Contains("Image", StringComparison.Ordinal))
        {
            return "Display";
        }

        return "Controls";
    }

    private static string SplitName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var result = new List<char>(name.Length + 8) { name[0] };
        for (var i = 1; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current) && !char.IsUpper(name[i - 1]))
            {
                result.Add(' ');
            }

            result.Add(current);
        }

        return new string(result.ToArray());
    }
}
