# credfeto-git-mcp

An MCP server that gives agents a fixed set of git operations with validated inputs, so an agent never composes a git
command line itself. It speaks MCP over stdio and ships as a trimmed Native AOT dotnet tool.

Clients register it under the server name `git`, so its tools appear as `mcp__git__<tool>`.

## Build Status

| Branch  | Status                                                |
|---------|-------------------------------------------------------|
| main    | [![Build: Pre-Release][pre-release-img]][pre-release] |
| release | [![Build: Release][release-img]][release]             |

## Projects

| Project | What it is |
| --- | --- |
| `Credfeto.Git.Mcp` | The server (`credfeto-git-mcp`): a Native AOT executable for `linux-x64`, with a framework-dependent `any` package for every other platform. |
| `Credfeto.Git.Mcp.Tests` | Unit tests, using temporary git repositories isolated from the user's hooks, signing and identity. |

## Tools

`tools/list` returns the tools sorted by name.

| Tool | Parameters | What it does |
| --- | --- | --- |
| `diff` | `base`: `main`, `staged` or `unstaged` (required) | Summarises a diff: each file with its insertions and deletions (at most 200 listed), and the totals. The full patch is written to a file and its path is returned. `main` compares the merge base of `HEAD` and `origin/main` (or the local `main` when there is no `origin/main`) with `HEAD`, so it shows the branch's commits; `staged` compares `HEAD` with the index; `unstaged` compares the index with the work tree (untracked files are not included). |
| `fetch` | none | Fetches `origin`, pruning its remote-tracking refs that are gone from it, and lists the `origin` remote-tracking refs that were added, updated or removed (at most 100 of each). Local branches and the work tree are not changed. |
| `log` | `count`: 1 to 100, default 20 | The most recent commits reachable from `HEAD`, newest first: sha, author name, author date and subject. A branch with no commits yet gives an empty list. |
| `status` | none | The current branch (null when `HEAD` is detached), its upstream with ahead and behind counts, and the changed, renamed, copied, unmerged and untracked entries (at most 200 listed; the full list goes to a file when there are more). |

Every tool answers with JSON. Unknown parameters and values outside the schema are refused before git runs.

## Safety rules

- The server works on one repository only, chosen at startup. No tool takes a repository path.
- No tool takes a ref, path, option or extra-arguments parameter in this release. Later tools that need them use the
  validators already in place:
  - ref names follow the `git check-ref-format --allow-onelevel` rules, must not start with `-`, and must name a ref
    that exists;
  - paths must resolve inside the repository root, must not start with `-` or `:` (pathspec magic), must not contain
    glob characters, backslashes or control characters, must not be inside `.git`, and must not go through a
    symlinked folder.
- git is found on `PATH` and started by absolute path, never through a shell, with `-C <root>`, stdin closed, and the
  repository-locating `GIT_*` variables (such as `GIT_DIR` and `GIT_WORK_TREE`) removed from its environment.
  `--` comes before any path, and diffs never run external diff or textconv programs.
- Error messages are fixed text. When git itself fails, its error output is written to a file and only the file's path
  and git's exit code are returned.

## Configuration

| Variable | Default | Meaning |
| --- | --- | --- |
| `GIT_MCP_REPO_ROOT` | `/workspace/repo` when it exists, else the git root of the start directory | The repository to work on. Must be an absolute path when set; anything else is a startup error. Unset with neither fallback available is a startup error too. |
| `GIT_MCP_OUTPUT_DIR` | `$XDG_STATE_HOME/credfeto-git-mcp/output`, else `~/.local/state/credfeto-git-mcp/output`, else a new private folder in `$TMPDIR` | Where large output (full diffs, long status lists, git error output) is written. Each server process writes into its own `session-<random>` folder (mode 0700, files 0600), and each result replaces the previous one of the same kind. Session folders older than two days are removed at startup. Must be an absolute path when set. |

When serving, the server writes one line to stderr naming the repository root and where it came from; stdout carries
only MCP traffic.

## Command line

```text
credfeto-git-mcp              serve MCP over stdio
credfeto-git-mcp --self-test  build the tools and find git, then exit 0 (or 1 with a reason on stderr)
credfeto-git-mcp --help       show the usage
```

## Registering the server

```json
{
  "mcpServers": {
    "git": {
      "command": "credfeto-git-mcp",
      "env": { "GIT_MCP_REPO_ROOT": "/workspace/repo" }
    }
  }
}
```

## Changelog

View [changelog][changelog].

## Contributing

See [contributing guidelines][contributing].

## Security

See [security policy][security].

## Licence

This project is licensed under the [MIT Licence][licence].

[changelog]: CHANGELOG.md
[contributing]: CONTRIBUTING.md
[licence]: LICENSE
[pre-release-img]: https://github.com/credfeto/credfeto-git-mcp/actions/workflows/build-and-publish-pre-release.yml/badge.svg
[pre-release]: https://github.com/credfeto/credfeto-git-mcp/actions/workflows/build-and-publish-pre-release.yml
[release-img]: https://github.com/credfeto/credfeto-git-mcp/actions/workflows/build-and-publish-release.yml/badge.svg
[release]: https://github.com/credfeto/credfeto-git-mcp/actions/workflows/build-and-publish-release.yml
[security]: SECURITY.md
