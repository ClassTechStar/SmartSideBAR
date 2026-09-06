// JSON 源生成 (AOT/Trim 友好, 方案 §9 风险缓解) —— camelCase 与 Electron config.json 互认。
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
public sealed partial class ConfigJsonContext : JsonSerializerContext;
