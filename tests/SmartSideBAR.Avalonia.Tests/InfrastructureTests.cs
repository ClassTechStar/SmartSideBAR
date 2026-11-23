// MVVM 基座 (M0) 的回归测试: 属性通知、异步命令防重入/异常兜底、State 订阅生命周期。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class InfrastructureTests
{
    private sealed class SampleState : StateBase
    {
        private int _fps;
        private string _title = "";

        public int Fps { get => _fps; set => SetProperty(ref _fps, value); }

        public string Title { get => _title; set => SetProperty(ref _title, value); }

        public void BindConfig(IEventBus bus) =>
            SubscribeConfig(bus, e => Fps = e.Snapshot.Recorder.Fps);
    }

    [Fact]
    public void 属性相等时不发通知()
    {
        var state = new SampleState { Fps = 10 };
        var changed = new List<string?>();
        state.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        state.Fps = 10;

        Assert.Empty(changed);
    }

    [Fact]
    public void 属性变化通知对应属性名()
    {
        var state = new SampleState();
        var changed = new List<string?>();
        state.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        state.Fps = 15;

        Assert.Equal([nameof(SampleState.Fps)], changed);
        Assert.Equal(15, state.Fps);
    }

    [Fact]
    public void 异步命令执行期间禁止重入()
    {
        var gate = new TaskCompletionSource();
        var runs = 0;
        var command = new AsyncRelayCommand(() =>
        {
            runs++;
            return gate.Task;
        });

        command.Execute(null);
        Assert.False(command.CanExecute(null));
        command.Execute(null); // 运行中重复点击应被忽略

        gate.SetResult();
        Assert.True(SpinWait.SpinUntil(() => command.CanExecute(null), 2000));
        Assert.Equal(1, runs);
    }

    [Fact]
    public void 异步命令异常走onError而不是崩进程()
    {
        var errors = new List<Exception>();
        var command = new AsyncRelayCommand(
            () => Task.FromException(new InvalidOperationException("boom")),
            onError: errors.Add);

        command.Execute(null);

        Assert.True(SpinWait.SpinUntil(() => errors.Count == 1, 2000));
        Assert.IsType<InvalidOperationException>(errors[0]);
        Assert.True(SpinWait.SpinUntil(() => command.CanExecute(null), 2000));
    }

    [Fact]
    public void StateBase释放后退订配置事件()
    {
        var bus = new EventBus();
        var state = new SampleState();
        state.BindConfig(bus);

        bus.Publish(new ConfigChanged(new SidekickConfig { Recorder = new RecorderConfig { Fps = 42 } }));
        Assert.Equal(42, state.Fps);

        state.Dispose();
        bus.Publish(new ConfigChanged(new SidekickConfig { Recorder = new RecorderConfig { Fps = 7 } }));
        Assert.Equal(42, state.Fps); // 已退订, 不再更新
    }
}
