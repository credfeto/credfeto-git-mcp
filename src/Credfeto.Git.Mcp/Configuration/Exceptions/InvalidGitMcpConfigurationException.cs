using System;
using System.ComponentModel;

namespace Credfeto.Git.Mcp.Configuration.Exceptions;

[Description("The git MCP server configuration is not valid")]
public sealed partial class InvalidGitMcpConfigurationException : Exception;
