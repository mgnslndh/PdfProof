#!/bin/bash
# Renders brand/png/*.png from the SVGs with headless Chrome (transparent background).
set -e
cd "$(dirname "$0")"
C="/c/Program Files/Google/Chrome/Application/chrome.exe"
W=$(cygpath -m "$PWD"); U="file:///$W"
mkdir -p brand/png
render(){ # svg out w h
  echo "<!doctype html><style>html,body{margin:0;background:transparent}img{display:block;width:${3}px;height:${4}px}</style><img src='$U/brand/$1'>" > brand/_r.html
  "$C" --headless=new --disable-gpu --hide-scrollbars --default-background-color=00000000 --force-device-scale-factor=1 --window-size=$3,$4 --screenshot="$W/brand/png/$2" "$U/brand/_r.html" 2>&1 | grep -o "bytes written.*png$" || true
}
for s in 128 256 512 1024; do render pdfproof-icon.svg pdfproof-icon-$s.png $s $s; done
render pdfproof-logo-dark-text.svg pdfproof-logo-dark-text-800.png 800 215
render pdfproof-logo-light-text.svg pdfproof-logo-light-text-800.png 800 215
render pdfproof-social.svg pdfproof-social-1280x640.png 1280 640
rm brand/_r.html
