using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Workbench.Core.Godot;

namespace Workbench.ViewModels;

/// <summary>
/// One downloadable file inside an open release card.
/// </summary>
/// <remarks>
/// The row has four right hand states and only one is ever on: an Install button, a queued
/// label, a bar with a percentage and a cancel, or a muted Installed check. Installed is
/// per version rather than per file, so once a version is here every build of it reads
/// installed whichever processor that row is.
/// </remarks>
public sealed partial class EngineBuildViewModel : ViewModelBase
{
    private readonly Func<EngineBuildViewModel, Task> _install;
    private readonly Action<EngineBuildViewModel> _cancel;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private EngineInstallStage _stage = EngineInstallStage.Done;

    [ObservableProperty]
    private bool _isWorking;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressLabel = string.Empty;

    public EngineBuildViewModel(
        EngineBuild build,
        bool isInstalled,
        Func<EngineBuildViewModel, Task> install,
        Action<EngineBuildViewModel> cancel)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(cancel);

        Build = build;
        _isInstalled = isInstalled;
        _install = install;
        _cancel = cancel;
    }

    public EngineBuild Build { get; }

    public string FileName => Build.FileName;

    public bool IsMono => Build.IsMono;

    /// <summary>True while this row can be started, which is the Install button.</summary>
    public bool CanInstall => !IsInstalled && !IsWorking;

    /// <summary>Waiting for a slot. Several installs run at once and the rest queue.</summary>
    public bool IsQueued => IsWorking && Stage == EngineInstallStage.Queued;

    /// <summary>Running, so the row draws a bar rather than a button.</summary>
    public bool IsRunning => IsWorking && Stage != EngineInstallStage.Queued;

    /// <summary>False while verifying and unpacking, which have no percentage to show.</summary>
    public bool HasProgress => Stage == EngineInstallStage.Downloading;

    [RelayCommand]
    private Task Install() => _install(this);

    [RelayCommand]
    private void Cancel() => _cancel(this);

    /// <summary>What one report from the installer means for this row.</summary>
    public void Report(EngineInstallProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        Stage = progress.Stage;
        Progress = progress.Fraction ?? 0;

        ProgressLabel = progress.Stage switch
        {
            EngineInstallStage.Downloading when progress.Fraction is { } part => $"{part * 100:0}%",
            EngineInstallStage.Downloading => "downloading",
            EngineInstallStage.Verifying => "verifying",
            EngineInstallStage.Extracting => "unpacking",
            EngineInstallStage.Registering => "finishing",
            _ => string.Empty,
        };
    }

    public void Started()
    {
        IsWorking = true;
        Stage = EngineInstallStage.Queued;
        Progress = 0;
        ProgressLabel = string.Empty;
    }

    public void Stopped(bool installed)
    {
        IsWorking = false;
        Progress = 0;
        ProgressLabel = string.Empty;

        if (installed)
        {
            IsInstalled = true;
        }
    }

    partial void OnIsInstalledChanged(bool value) => NotifyState();

    partial void OnIsWorkingChanged(bool value) => NotifyState();

    partial void OnStageChanged(EngineInstallStage value) => NotifyState();

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(IsQueued));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasProgress));
    }
}
