using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Remote.Protocol;
using Avalonia.Remote.Protocol.Designer;
using Avalonia.Remote.Protocol.Input;
using Avalonia.Remote.Protocol.Viewport;

namespace XamlPlayground.Services;

public sealed class WorkspaceRemotePreviewService : IDisposable
{
    private const int StopProcessExitTimeoutMilliseconds = 5000;

    private Process? _process;
    private RemotePreviewTcpSession? _session;
    private string? _targetAssemblyPath;

    public event Action<FrameMessage>? FrameReceived;

    public event Action<WorkspaceRemotePreviewError>? ErrorReceived;

    public async Task<WorkspaceRemotePreviewResult> StartOrUpdateAsync(
        string xaml,
        string targetAssemblyPath,
        string xamlFileProjectPath,
        string? workingDirectory)
    {
        if (!File.Exists(targetAssemblyPath))
        {
            return WorkspaceRemotePreviewResult.Fail("Project output assembly not found. Build the project first.");
        }

        var hostPath = Path.Combine(AppContext.BaseDirectory, "XamlPlayground.PreviewerHost.dll");
        if (!File.Exists(hostPath))
        {
            return WorkspaceRemotePreviewResult.Fail("Workspace preview host not found. Build the desktop app first.");
        }

        if (_process is null ||
            _process.HasExited ||
            _session is null ||
            !string.Equals(_targetAssemblyPath, targetAssemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            StopCurrentProcess();
            _targetAssemblyPath = targetAssemblyPath;
            _session = new RemotePreviewTcpSession();
            _session.FrameReceived += frame => FrameReceived?.Invoke(frame);
            _session.ErrorReceived += error => ErrorReceived?.Invoke(error);

            var startInfo = BuildStartInfo(
                hostPath,
                targetAssemblyPath,
                _session.Port,
                workingDirectory);
            try
            {
                var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };
                _process = process;
                process.Exited += (_, _) =>
                {
                    int exitCode;
                    try
                    {
                        exitCode = process.ExitCode;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (InvalidOperationException)
                    {
                        return;
                    }

                    if (exitCode != 0)
                    {
                        ErrorReceived?.Invoke(new WorkspaceRemotePreviewError(
                            $"Workspace preview host exited with code {exitCode}.",
                            null,
                            null,
                            null));
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        ErrorReceived?.Invoke(new WorkspaceRemotePreviewError(e.Data, null, null, null));
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception exception)
            {
                StopCurrentProcess();
                return WorkspaceRemotePreviewResult.Fail($"Workspace preview host failed to start: {exception.Message}");
            }
        }

        var (viewportWidth, viewportHeight) = TryGetDesignSize(xaml);
        await _session.SendUpdateXamlAsync(
            xaml,
            targetAssemblyPath,
            xamlFileProjectPath,
            viewportWidth,
            viewportHeight);
        return WorkspaceRemotePreviewResult.Success();
    }

    public void UpdateViewport(double width, double height, double dpiX, double dpiY)
    {
        _session?.UpdateViewport(width, height, dpiX, dpiY);
    }

    public Task SendPointerMovedAsync(double x, double y, InputModifiers[] modifiers)
    {
        return _session?.SendPointerMovedAsync(x, y, modifiers) ?? Task.CompletedTask;
    }

    public Task SendPointerPressedAsync(double x, double y, MouseButton button, InputModifiers[] modifiers)
    {
        return _session?.SendPointerPressedAsync(x, y, button, modifiers) ?? Task.CompletedTask;
    }

    public Task SendPointerReleasedAsync(double x, double y, MouseButton button, InputModifiers[] modifiers)
    {
        return _session?.SendPointerReleasedAsync(x, y, button, modifiers) ?? Task.CompletedTask;
    }

    public Task SendScrollAsync(double x, double y, double deltaX, double deltaY, InputModifiers[] modifiers)
    {
        return _session?.SendScrollAsync(x, y, deltaX, deltaY, modifiers) ?? Task.CompletedTask;
    }

    public Task SendKeyEventAsync(bool isDown, Avalonia.Input.Key key, Avalonia.Input.PhysicalKey physicalKey, string? keySymbol, InputModifiers[] modifiers)
    {
        return _session?.SendKeyEventAsync(isDown, key, physicalKey, keySymbol, modifiers) ?? Task.CompletedTask;
    }

    public Task SendTextInputAsync(string text)
    {
        return _session?.SendTextInputAsync(text) ?? Task.CompletedTask;
    }

    public void Stop()
    {
        StopCurrentProcess();
        _targetAssemblyPath = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private void StopCurrentProcess()
    {
        _session?.Dispose();
        _session = null;

        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(StopProcessExitTimeoutMilliseconds);
            }
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private static ProcessStartInfo BuildStartInfo(
        string hostPath,
        string targetAssemblyPath,
        int port,
        string? workingDirectory)
    {
        var targetDirectory = Path.GetDirectoryName(targetAssemblyPath) ?? AppContext.BaseDirectory;
        var resolvedWorkingDirectory = ResolveExistingDirectory(workingDirectory) ?? targetDirectory;
        var baseName = Path.GetFileNameWithoutExtension(targetAssemblyPath);
        var targetRuntimeConfig = Path.Combine(targetDirectory, baseName + ".runtimeconfig.json");
        var targetDeps = Path.Combine(targetDirectory, baseName + ".deps.json");
        var hostRuntimeConfig = Path.ChangeExtension(hostPath, ".runtimeconfig.json");
        var hostDeps = Path.ChangeExtension(hostPath, ".deps.json");
        var runtimeConfig = File.Exists(hostRuntimeConfig) ? hostRuntimeConfig : targetRuntimeConfig;
        var depsFile = File.Exists(targetDeps) ? targetDeps : hostDeps;
        var args = string.Join(
            " ",
            new[]
            {
                "exec",
                File.Exists(runtimeConfig) ? $"--runtimeconfig \"{runtimeConfig}\"" : null,
                File.Exists(depsFile) ? $"--depsfile \"{depsFile}\"" : null,
                $"\"{hostPath}\"",
                $"--transport tcp-bson://127.0.0.1:{port}/",
                $"\"{targetAssemblyPath}\""
            }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            WorkingDirectory = resolvedWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static string? ResolveExistingDirectory(string? directory)
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

    private static (double? Width, double? Height) TryGetDesignSize(string xamlText)
    {
        if (string.IsNullOrWhiteSpace(xamlText))
        {
            return (null, null);
        }

        var width = TryMatchDesignDimension(xamlText, "DesignWidth")
                    ?? TryMatchDesignDimension(xamlText, "Width");
        var height = TryMatchDesignDimension(xamlText, "DesignHeight")
                     ?? TryMatchDesignDimension(xamlText, "Height");

        return (width, height);
    }

    private static double? TryMatchDesignDimension(string text, string propertyName)
    {
        var regex = new Regex(
            $"\\b(?:\\w+:)?{propertyName}\\s*=\\s*\"(?<value>[0-9]+(?:\\.[0-9]+)?)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = regex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value;
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : null;
    }
}

public readonly record struct WorkspaceRemotePreviewResult(bool IsSuccess, string? ErrorMessage)
{
    public static WorkspaceRemotePreviewResult Success() => new(true, null);

    public static WorkspaceRemotePreviewResult Fail(string errorMessage) => new(false, errorMessage);
}

public sealed record WorkspaceRemotePreviewError(string Message, int? Line, int? Column, string? FilePath);

internal sealed class RemotePreviewTcpSession : IDisposable
{
    private readonly IDisposable _listener;
    private readonly object _gate = new();
    private IAvaloniaRemoteTransportConnection? _connection;
    private string? _pendingXaml;
    private string? _pendingAssemblyPath;
    private string? _pendingProjectPath;
    private double? _pendingViewportWidth;
    private double? _pendingViewportHeight;
    private double _viewportWidth = 800;
    private double _viewportHeight = 600;
    private string? _currentProjectPath;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Avalonia designer transport uses BSON reflection by design.")]
    public RemotePreviewTcpSession()
    {
        Port = GetFreeTcpPort();
        _listener = new BsonTcpTransport().Listen(IPAddress.Loopback, Port, OnConnected);
    }

    public int Port { get; }

    public event Action<FrameMessage>? FrameReceived;

    public event Action<WorkspaceRemotePreviewError>? ErrorReceived;

    public Task SendUpdateXamlAsync(
        string xaml,
        string assemblyPath,
        string xamlFileProjectPath,
        double? viewportWidth,
        double? viewportHeight)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            if (_connection is null)
            {
                _pendingXaml = xaml;
                _pendingAssemblyPath = assemblyPath;
                _pendingProjectPath = xamlFileProjectPath;
                _pendingViewportWidth = viewportWidth;
                _pendingViewportHeight = viewportHeight;
                return Task.CompletedTask;
            }

            connection = _connection;
            _currentProjectPath = xamlFileProjectPath;
        }

        UpdateViewportIfNeeded(connection, viewportWidth, viewportHeight, 96, 96);
        return connection.Send(new UpdateXamlMessage
        {
            Xaml = xaml,
            AssemblyPath = assemblyPath,
            XamlFileProjectPath = xamlFileProjectPath
        });
    }

    public Task SendPointerMovedAsync(double x, double y, InputModifiers[] modifiers)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }
        if (connection is null)
        {
            return Task.CompletedTask;
        }
        return connection.Send(new PointerMovedEventMessage { X = x, Y = y, Modifiers = modifiers });
    }

    public Task SendPointerPressedAsync(double x, double y, MouseButton button, InputModifiers[] modifiers)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }
        if (connection is null)
        {
            return Task.CompletedTask;
        }
        return connection.Send(new PointerPressedEventMessage { X = x, Y = y, Button = button, Modifiers = modifiers });
    }

    public Task SendPointerReleasedAsync(double x, double y, MouseButton button, InputModifiers[] modifiers)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }
        if (connection is null)
        {
            return Task.CompletedTask;
        }
        return connection.Send(new PointerReleasedEventMessage { X = x, Y = y, Button = button, Modifiers = modifiers });
    }

    public Task SendScrollAsync(double x, double y, double deltaX, double deltaY, InputModifiers[] modifiers)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }
        if (connection is null)
        {
            return Task.CompletedTask;
        }
        return connection.Send(new ScrollEventMessage { X = x, Y = y, DeltaX = deltaX, DeltaY = deltaY, Modifiers = modifiers });
    }

    public Task SendKeyEventAsync(bool isDown, Avalonia.Input.Key key, Avalonia.Input.PhysicalKey physicalKey, string? keySymbol, InputModifiers[] modifiers)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }
        if (connection is null)
        {
            return Task.CompletedTask;
        }
        return connection.Send(new KeyEventMessage
        {
            IsDown = isDown,
            Key = (Key)key,
            PhysicalKey = (PhysicalKey)physicalKey,
            KeySymbol = keySymbol,
            Modifiers = modifiers
        });
    }

    public Task SendTextInputAsync(string text)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }
        if (connection is null)
        {
            return Task.CompletedTask;
        }
        return connection.Send(new TextInputEventMessage { Text = text });
    }

    public void Dispose()
    {
        _listener.Dispose();
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    public void UpdateViewport(double width, double height, double dpiX, double dpiY)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            _pendingViewportWidth = width;
            _pendingViewportHeight = height;
            connection = _connection;
        }

        if (connection is null)
        {
            return;
        }

        UpdateViewportIfNeeded(connection, width, height, dpiX, dpiY);
    }

    private void OnConnected(IAvaloniaRemoteTransportConnection connection)
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = connection;
        }

        connection.OnMessage += OnMessage;
        connection.OnException += (_, exception) => ErrorReceived?.Invoke(new WorkspaceRemotePreviewError(
            $"Workspace preview transport error: {exception.Message}",
            null,
            null,
            _currentProjectPath));
        connection.Start();
        SendPreflightMessages(connection);
        TrySendPendingUpdate(connection);
    }

    private void OnMessage(IAvaloniaRemoteTransportConnection connection, object message)
    {
        switch (message)
        {
            case FrameMessage frame:
                _ = connection.Send(new FrameReceivedMessage
                {
                    SequenceId = frame.SequenceId
                });
                FrameReceived?.Invoke(frame);
                break;
            case RequestViewportResizeMessage resize:
                UpdateViewportIfNeeded(connection, resize.Width, resize.Height, 96, 96);
                break;
            case UpdateXamlResultMessage updateResult:
                if (!string.IsNullOrWhiteSpace(updateResult.Error))
                {
                    var line = TryGetPositiveInt(updateResult, "LineNumber", "Line");
                    var column = TryGetPositiveInt(updateResult, "LinePosition", "Position", "Column");
                    ErrorReceived?.Invoke(new WorkspaceRemotePreviewError(
                        updateResult.Error,
                        line,
                        column,
                        _currentProjectPath));
                }

                break;
        }
    }

    private void TrySendPendingUpdate(IAvaloniaRemoteTransportConnection connection)
    {
        string? xaml;
        string? assemblyPath;
        string? projectPath;
        double? viewportWidth;
        double? viewportHeight;
        lock (_gate)
        {
            xaml = _pendingXaml;
            assemblyPath = _pendingAssemblyPath;
            projectPath = _pendingProjectPath;
            viewportWidth = _pendingViewportWidth;
            viewportHeight = _pendingViewportHeight;
            _pendingXaml = null;
            _pendingAssemblyPath = null;
            _pendingProjectPath = null;
            _pendingViewportWidth = null;
            _pendingViewportHeight = null;
        }

        if (string.IsNullOrWhiteSpace(xaml) ||
            string.IsNullOrWhiteSpace(assemblyPath) ||
            string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        lock (_gate)
        {
            _currentProjectPath = projectPath;
        }

        UpdateViewportIfNeeded(connection, viewportWidth, viewportHeight, 96, 96);
        _ = connection.Send(new UpdateXamlMessage
        {
            Xaml = xaml,
            AssemblyPath = assemblyPath,
            XamlFileProjectPath = projectPath
        });
    }

    private static void SendPreflightMessages(IAvaloniaRemoteTransportConnection connection)
    {
        connection.Send(new ClientSupportedPixelFormatsMessage
        {
            Formats = new[] { PixelFormat.Bgra8888 }
        });

        connection.Send(new ClientRenderInfoMessage
        {
            DpiX = 96,
            DpiY = 96
        });

        SendViewportMessage(connection, 800, 600, 96, 96);
    }

    private void UpdateViewportIfNeeded(
        IAvaloniaRemoteTransportConnection connection,
        double? width,
        double? height,
        double dpiX,
        double dpiY)
    {
        if (width is null || height is null || width.Value <= 0 || height.Value <= 0)
        {
            return;
        }

        if (Math.Abs(_viewportWidth - width.Value) < 0.01 &&
            Math.Abs(_viewportHeight - height.Value) < 0.01)
        {
            return;
        }

        _viewportWidth = width.Value;
        _viewportHeight = height.Value;
        SendViewportMessage(connection, _viewportWidth, _viewportHeight, dpiX, dpiY);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Remote protocol error messages are inspected reflectively to support multiple Avalonia protocol versions.")]
    private static int? TryGetPositiveInt(object source, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(source);
            if (value is int intValue && intValue > 0)
            {
                return intValue;
            }
        }

        return null;
    }

    private static void SendViewportMessage(
        IAvaloniaRemoteTransportConnection connection,
        double width,
        double height,
        double dpiX,
        double dpiY)
    {
        connection.Send(new ClientViewportAllocatedMessage
        {
            Width = width,
            Height = height,
            DpiX = dpiX,
            DpiY = dpiY
        });
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
