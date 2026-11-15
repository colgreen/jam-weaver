# Local verification and CI

Run the repository verification entry point with PowerShell 7 or later:

```powershell
pwsh -NoProfile -File ./verify.ps1
```

It builds `JamWeaver.sln` and runs both native xUnit executables in order:
`JamWeaver.Core.Tests`, then `JamWeaver.Console.Tests`. The test commands use
`--no-build` against the just-built configuration. Debug is the default; CI uses:

```powershell
pwsh -NoProfile -File ./verify.ps1 -Configuration Release
```

The script resolves the repository root from its own location, so it also works
when invoked by absolute path from another directory. It restores the caller's
working directory on success and failure, stops at the first failing command,
and returns that command's exit code. PowerShell command-resolution or script
errors also stop verification with a nonzero exit code. The success message is
printed only after the build and both runners pass.

Builds run serially with persistent MSBuild and compiler servers disabled. This
keeps verification independent of shared build-process state. The ordinary
`dotnet build` and individual native runner commands remain available for
focused development. Tests use fake MIDI ports; passing does not establish
physical-device behavior, timing, or musical quality.

## SDK, diagnostics, and formatting

`global.json` selects .NET SDK 11.0.100, permits newer servicing patches in the
same 11.0.1xx feature band, and excludes preview SDKs. Changing SDK feature bands
requires an explicit version update. All projects retain their `net11.0` target
and existing pinned package versions.

`Directory.Build.props` enables the SDK analyzers at analysis level 11.0 with
the default rule set and treats build warnings as errors. The current code was
rebuilt under that rule set without diagnostics before making warnings fatal.
This includes compiler and enabled analyzer warnings, including the existing
xUnit analyzers; it adds no analyzer packages or blanket warning suppressions.
Review any proposed diagnostic or analyzer-level expansion before adopting it.

`.editorconfig` describes existing formatting: spaces, four-space code
indentation, two-space project/JSON/YAML indentation, Allman C# braces, retained
single-line blocks/statements, and a file-scoped namespace suggestion.
Markdown trailing whitespace is preserved. It does not normalize line endings
or impose a repository-wide formatting sweep. Editor suggestions are separate
from the build's compiler and code-quality checks; formatting is not a CI gate.

## Verification script regression checks

```powershell
pwsh -NoProfile -File ./tools/test-verify.ps1
```

This dependency-free harness runs the entry point in child PowerShell processes
with a recording `dotnet` fake. It checks Debug/Release argument propagation,
command order, successful completion, exit-code propagation and stopping after
each of the three possible command failures, and working-directory handling.
It requires no .NET build or hardware and cleans up its temporary files.

## GitHub Actions

`.github/workflows/verify.yml` runs on pushes and pull requests on a Windows
hosted runner. It uses commit-pinned checkout and .NET setup actions, installs
the SDK from `global.json`, checks the verification script, and runs the same
Release verification entry point. The token has read-only repository contents
permission, checkout does not retain credentials, and the job has a 15-minute
timeout. There are no publishing or hardware steps.

The workflow is prepared in the repository; its hosted execution must be
confirmed after it is pushed to GitHub. Local verification covers the script
and commands, not hosted-runner provisioning or repository Actions settings.

References: [SDK build/analyzer properties](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props),
[SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json),
and [official .NET setup action](https://github.com/actions/setup-dotnet).

Related: [project practices](../practices.md), [project
structure](../architecture/project-structure.md), and [maintainability
review](../plans/maintainability-review.md).
