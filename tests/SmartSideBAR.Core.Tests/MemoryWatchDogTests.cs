// 内存看门狗 (P1-2): 连续多次超限才判定重启, 避免抖动误杀。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class MemoryWatchDogTests
{
    private static MemorySample S(long bytes) => new(bytes, DateTimeOffset.UnixEpoch);

    [Fact]
    public void 单次超限不触发()
    {
        var dog = new MemoryWatchDog(MemoryWatchDog.FromMegabytes(800), requiredBreaches: 3);
        Assert.False(dog.Push(S(900L * 1024 * 1024)));
    }

    [Fact]
    public void 连续三次超限才触发()
    {
        var limit = MemoryWatchDog.FromMegabytes(800);
        var dog = new MemoryWatchDog(limit, requiredBreaches: 3);
        Assert.False(dog.Push(S(limit + 1)));
        Assert.False(dog.Push(S(limit + 1)));
        Assert.True(dog.Push(S(limit + 1)));
    }

    [Fact]
    public void 中途回落则重新计数()
    {
        var limit = MemoryWatchDog.FromMegabytes(800);
        var dog = new MemoryWatchDog(limit, requiredBreaches: 3);
        dog.Push(S(limit + 1));
        dog.Push(S(limit - 1)); // 回落
        dog.Push(S(limit + 1));
        Assert.False(dog.Push(S(limit + 1)));
    }

    [Fact]
    public void Reset_清空采样()
    {
        var limit = MemoryWatchDog.FromMegabytes(800);
        var dog = new MemoryWatchDog(limit, requiredBreaches: 2);
        dog.Push(S(limit + 1));
        dog.Reset();
        Assert.Empty(dog.Samples);
    }

    [Fact]
    public void FromMegabytes_换算正确()
        => Assert.Equal(800L * 1024 * 1024, MemoryWatchDog.FromMegabytes(800));
}
