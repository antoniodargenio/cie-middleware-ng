#!/bin/sh
# Versioning SemVer (https://semver.org): MAJOR.MINOR.PATCH.BUILD
#   uso: ./bump-version.sh major|minor|patch
# MAJOR/MINOR/PATCH seguono le regole SemVer (major azzera minor e patch,
# minor azzera patch). BUILD invece non si azzera mai: lo incrementa da solo
# l'hook .githooks/pre-commit a ogni commit.
set -e
cd "$(git rev-parse --show-toplevel)"
IFS=. read -r ma mi pa bu < version.txt
case "$1" in
  major) ma=$((ma + 1)); mi=0; pa=0 ;;
  minor) mi=$((mi + 1)); pa=0 ;;
  patch) pa=$((pa + 1)) ;;
  *) echo "uso: $0 major|minor|patch" >&2; exit 1 ;;
esac
new="$ma.$mi.$pa.$bu"
printf '%s\n' "$new" > version.txt
sed -b -i "s/\(PRODUCT_VERSION \"\)[0-9.]*\(\"\)/\1$new\2/" Setup/CIE-Middleware-NG.nsi
python .githooks/set-rc-version.py "$new" || python3 .githooks/set-rc-version.py "$new"
echo "Versione: $new (il build, $bu, sale al prossimo commit)"
