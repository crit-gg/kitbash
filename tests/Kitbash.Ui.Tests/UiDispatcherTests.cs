using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Kitbash.Ui.Tests;

/// <summary>
/// The way back onto the thread that owns the views. A timer or a directory watch raises
/// its event anywhere, so this is what stands between one and a bound property.
/// </summary>
public class UiDispatcherTests
{
    [Fact]
    public void TheDispatcherIsRegisteredOnce()
    {
        using var provider = new ServiceCollection()
            .AddKitbashDispatcher()
            .AddKitbashDispatcher()
            .BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IUiDispatcher>();

        Assert.IsType<AvaloniaUiDispatcher>(dispatcher);
        Assert.Same(dispatcher, provider.GetRequiredService<IUiDispatcher>());
    }

    [Fact]
    public void AnAppCanSubstituteItsOwn()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IUiDispatcher, HereAndNow>()
            .AddKitbashDispatcher()
            .BuildServiceProvider();

        Assert.IsType<HereAndNow>(provider.GetRequiredService<IUiDispatcher>());
    }

    [AvaloniaFact]
    public void WorkPostedFromAnotherThreadLandsOnTheUiThread()
    {
        var ui = Thread.CurrentThread.ManagedThreadId;
        var ran = 0;

        IUiDispatcher dispatcher = new AvaloniaUiDispatcher();

        Task.Run(() => dispatcher.Post(() => ran = Thread.CurrentThread.ManagedThreadId)).Wait();

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ui, ran);
    }

    /// <summary>Runs the action where it was called, which is what a test wants.</summary>
    private sealed class HereAndNow : IUiDispatcher
    {
        public void Post(Action action) => action();
    }
}
