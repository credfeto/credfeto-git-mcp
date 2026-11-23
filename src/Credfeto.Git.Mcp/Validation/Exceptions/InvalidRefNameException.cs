using System;
using System.ComponentModel;

namespace Credfeto.Git.Mcp.Validation.Exceptions;

[Description("Not a valid git ref name: it must follow git check-ref-format and must not start with '-'")]
public sealed partial class InvalidRefNameException : Exception;
