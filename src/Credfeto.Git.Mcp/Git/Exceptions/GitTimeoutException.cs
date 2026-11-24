using System;
using System.ComponentModel;

namespace Credfeto.Git.Mcp.Git.Exceptions;

[Description("git did not finish in time and was stopped")]
public sealed partial class GitTimeoutException : Exception;
