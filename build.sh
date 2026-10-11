#!/usr/bin/env bash
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --target=Test or --configuration=Debug.
# build.ps1 does the same in PowerShell.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Run from the repository root so dotnet uses this repository's global.json (SDK pin), wherever the caller is.
# The subshell keeps the caller's directory unchanged, even if this script is sourced.
(cd "$script_dir" && exec dotnet run --project "build/Build/Build.csproj" -- "$@")
