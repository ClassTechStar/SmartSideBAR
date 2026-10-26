// JSON 源生成 (AOT/Trim 友好, 方案 §9 风险缓解), camelCase 与 Electron config.json 互认。
using System.Text.Json.Serialization;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Configuration;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SidekickConfig))]
[JsonSerializable(typeof(List<LinkItem>))]
[JsonSerializable(typeof(IReadOnlyList<LinkItem>))]
[JsonSerializable(typeof(List<Reminder>))]
[JsonSerializable(typeof(IReadOnlyList<Reminder>))]
[JsonSerializable(typeof(OobeState))]
[JsonSerializable(typeof(AppearanceConfig))]
[JsonSerializable(typeof(FloatBallConfig))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
// 数组/集合：ConfigService.Set<T> 序列化的是调用方的 T，未登记的集合类型会抛
// NotSupportedException（写 policy.disabledModules / links 时必踩）。
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(LinkItem[]))]
[JsonSerializable(typeof(Reminder[]))]
public sealed partial class ConfigJsonContext : JsonSerializerContext;
