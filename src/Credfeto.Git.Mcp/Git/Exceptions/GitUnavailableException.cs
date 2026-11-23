using System;
using System.ComponentModel;

namespace Credfeto.Git.Mcp.Git.Exceptions;

[Description("git could not be started")]
public sealed partial class GitUnavailableException : Exception;
