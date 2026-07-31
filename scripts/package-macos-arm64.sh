#!/usr/bin/env bash
set -euo pipefail
IFS=$'\n\t'
umask 022

project="${1:?project path required}"
version="${2:?version required}"
dist_dir="${3:?dist directory required}"
repository_root="$(git rev-parse --show-toplevel)"
asset_name="Sophie-Desktop-Pet-macOS-arm64.zip"

[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || {
  echo "Version must match X.Y.Z." >&2
  exit 1
}

case "$dist_dir" in
  /*) ;;
  *) dist_dir="$repository_root/$dist_dir" ;;
esac

work_root="$(mktemp -d "${TMPDIR:-/tmp}/sophie-package.XXXXXX")"
publish_dir="$work_root/publish"
app="$work_root/Sophie Desktop Pet.app"
contents="$app/Contents"
macos_dir="$contents/MacOS"
resources_dir="$contents/Resources"
zip_path="$dist_dir/$asset_name"
sha_path="$zip_path.sha256"

test ! -e "$zip_path"
test ! -e "$sha_path"
mkdir -p "$publish_dir" "$macos_dir" "$resources_dir" "$dist_dir"

assembly_name="$(
  dotnet msbuild "$project" -nologo -v:q \
    -getProperty:AssemblyName | tr -d '\r' | tail -n 1
)"
test -n "$assembly_name"

dotnet publish "$project" \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --no-restore \
  --output "$publish_dir" \
  -p:PublishSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -p:ContinuousIntegrationBuild=true

test -x "$publish_dir/$assembly_name"
test "$(lipo -archs "$publish_dir/$assembly_name")" = "arm64"

/usr/bin/ditto "$publish_dir" "$macos_dir"
/usr/bin/ditto \
  "$repository_root/packaging/AppIcon.icns" \
  "$resources_dir/AppIcon.icns"
/usr/bin/ditto \
  "$repository_root/packaging/Info.plist" \
  "$contents/Info.plist"

/usr/bin/plutil -replace CFBundleExecutable \
  -string "$assembly_name" "$contents/Info.plist"
/usr/bin/plutil -replace CFBundleShortVersionString \
  -string "$version" "$contents/Info.plist"
/usr/bin/plutil -replace CFBundleVersion \
  -string "$version" "$contents/Info.plist"
/usr/bin/plutil -lint "$contents/Info.plist"

while IFS= read -r -d '' library; do
  /usr/bin/codesign --force --sign - --timestamp=none "$library"
done < <(find "$macos_dir" -type f -name '*.dylib' -print0)

/usr/bin/codesign \
  --force --sign - --timestamp=none \
  "$macos_dir/$assembly_name"
/usr/bin/codesign \
  --force --sign - --timestamp=none \
  "$app"
/usr/bin/codesign --verify --deep --strict --verbose=2 "$app"

/usr/bin/ditto -c -k --keepParent "$app" "$zip_path"
/usr/bin/unzip -t "$zip_path"

verify_root="$(mktemp -d "${TMPDIR:-/tmp}/sophie-verify.XXXXXX")"
/usr/bin/ditto -x -k -- "$zip_path" "$verify_root"
verified_app="$verify_root/Sophie Desktop Pet.app"
/usr/bin/codesign --verify --deep --strict --verbose=2 "$verified_app"
test "$(
  /usr/bin/plutil -extract CFBundleShortVersionString \
    raw -o - "$verified_app/Contents/Info.plist"
)" = "$version"
test "$(lipo -archs \
  "$verified_app/Contents/MacOS/$assembly_name")" = "arm64"

(
  cd "$dist_dir"
  LC_ALL=C LANG=C /usr/bin/shasum -a 256 "$asset_name" \
    > "$asset_name.sha256"
)

printf 'Created %s\n' "$zip_path"
printf 'Created %s\n' "$sha_path"
