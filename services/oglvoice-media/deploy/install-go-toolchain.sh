#!/usr/bin/env bash
# SPDX-License-Identifier: MPL-2.0
# Install an isolated, pinned Go toolchain for the OGLVoice media bridge.
# Official checksums: https://go.dev/dl/
set -euo pipefail

VERSION=1.26.10
INSTALL_ROOT="${OGLVOICE_GO_INSTALL_ROOT:-/opt/oglvoice/toolchains}"
TARGET="${INSTALL_ROOT}/go${VERSION}"

case "$(uname -m)" in
  x86_64|amd64)
    GOARCH=amd64
    SHA256=ccfe7ae822d2c2f41f4fe7c956199967a4dac22d0ee0d2b2bfdb440aab3b20c5
    ;;
  aarch64|arm64)
    GOARCH=arm64
    SHA256=3f4227ce7b206a513e794f1393adb473a07b46e94d18bd15d1c684bbb7319797
    ;;
  *)
    echo "Unsupported architecture: $(uname -m) (expected x86_64 or aarch64)" >&2
    exit 1
    ;;
esac

if [[ -e "$TARGET" || -L "$TARGET" ]]; then
  if [[ -x "$TARGET/bin/go" ]] && "$TARGET/bin/go" version | grep -Fq "go${VERSION} "; then
    echo "Already installed: $("$TARGET/bin/go" version)"
    exit 0
  fi
  echo "Refusing to overwrite existing $TARGET" >&2
  exit 1
fi

for command in curl sha256sum tar mktemp; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "Missing prerequisite: $command" >&2
    exit 1
  fi
done

ARCHIVE="go${VERSION}.linux-${GOARCH}.tar.gz"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

curl --fail --location --retry 3 --output "$WORK/$ARCHIVE" "https://go.dev/dl/$ARCHIVE"
printf '%s  %s\n' "$SHA256" "$WORK/$ARCHIVE" | sha256sum --check --status || {
  echo "Checksum mismatch: $ARCHIVE — refusing to install." >&2
  exit 1
}

tar -xzf "$WORK/$ARCHIVE" -C "$WORK"
mkdir -p "$INSTALL_ROOT"
mv "$WORK/go" "$TARGET"
"$TARGET/bin/go" version
echo "Use $TARGET/bin/go build -tags nolibopusfile -trimpath -o /tmp/oglvoice-media ."
