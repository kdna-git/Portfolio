#!/usr/bin/env bash
# Builds a project and surfaces Avalonia XAML diagnostics, which the SDK otherwise
# reports without failing the build.
set -o pipefail
proj="$1"
rm -rf "$(dirname "$proj")/obj" "$(dirname "$proj")/bin" 2>/dev/null
dotnet build "$proj" -v n --nologo 2>&1 | grep -E "AVLN|: error|Advertencia\(s\)|Errores|Compilación" | grep -v "AVLN5001" | sort -u
