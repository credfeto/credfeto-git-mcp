using System;
using System.ComponentModel;

namespace Credfeto.Git.Mcp.Validation.Exceptions;

[Description(
    "Not a valid repository path: it must resolve inside the repository root, outside .git, and must not start with '-' or ':' or contain glob characters, backslashes or control characters"
)]
public sealed partial class InvalidRepositoryPathException : Exception;
