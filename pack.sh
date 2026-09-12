#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

VERSION="${1-}"
if [ -z "$VERSION" ]; then
    read -r -p "Enter package version: " VERSION
fi

# Strip leading "v" if entered, e.g. v1.2.3 -> 1.2.3
VERSION="${VERSION#[vV]}"

if [ -z "$VERSION" ]; then
    echo "No version entered." >&2
    exit 1
fi

dotnet restore Ani.Reader.slnx
dotnet build Ani.Reader.slnx -c Release --no-restore -p:Version="$VERSION"
dotnet test --project Ani.Reader.Test/Ani.Reader.Test.csproj -c Release --no-build
dotnet pack Ani.Reader/Ani.Reader.csproj -c Release --no-build -p:Version="$VERSION" -o ./artifacts

echo
echo "Packed Ani.Reader $VERSION into ./artifacts"
