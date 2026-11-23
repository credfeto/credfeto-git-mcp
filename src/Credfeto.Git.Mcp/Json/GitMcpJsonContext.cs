using System.Text.Json.Serialization;
using Credfeto.Git.Mcp.Models;

namespace Credfeto.Git.Mcp.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(DiffResponse))]
[JsonSerializable(typeof(FetchResponse))]
[JsonSerializable(typeof(LogResponse))]
[JsonSerializable(typeof(StatusResponse))]
public sealed partial class GitMcpJsonContext : JsonSerializerContext;
