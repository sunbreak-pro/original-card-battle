#!/usr/bin/env bash
# Drives the Unity Editor through uloop to capture the trial's screens (see README.md beside this file).
#   capture.sh shot <name>           capture the game view to $PARTS_SHOTS/shots/<name>.png (1920x1080)
#   capture.sh peek <name>           capture at 40% to $PARTS_SHOTS/peeks/<name>.png, to check state cheaply
#   capture.sh run '<C# code>'       run C# in the Editor and print the result
#   capture.sh click <x> <y>         click at stage px (1920x1080, top-left origin)
#   capture.sh move <x> <y>          move the pointer
#   capture.sh drag <x0> <y0> <x1> <y1>
#   capture.sh key <Key>             tap a key (Unity InputSystem Key names: Enter, Escape, J, C, Digit1 ...)
#   capture.sh focus                 bring the Editor to the front
#   capture.sh wait <ms>
SP="${PARTS_SHOTS:?set PARTS_SHOTS to the folder that receives the captures}"
cd "${UNITY_PROJECT:-C:/Users/user/Unity/RPG-by-card}" || exit 1
quiet() { grep -E '"Result": "[^"]|"ErrorMessage": "[^"]|error CS' | cut -c1-600 | head -6; }
# Play Mode is throttled while the Editor is unfocused, so every action brings it to the front first.
front() { uloop focus-window >/dev/null 2>&1; }
capture() { # <scale> <dest file>
  tmp="$SP/tmp/$(date +%s%N)"; mkdir -p "$tmp" "$(dirname "$2")"
  front
  uloop screenshot --capture-mode rendering --resolution-scale "$1" --output-directory "$tmp" > "$tmp/last.json" 2>&1
  f=$(ls "$tmp"/*.png 2>/dev/null | head -1)
  if [ -n "$f" ]; then mv "$f" "$2" && echo "saved $(basename "$2")"; else echo "no image"; head -20 "$tmp/last.json"; fi
}
cmd="$1"; shift
case "$cmd" in
  shot)  capture 1 "$SP/shots/$1.png" ;;
  peek)  capture 0.4 "$SP/peeks/$1.png" ;;
  run)   uloop execute-dynamic-code --code "$1" 2>&1 | quiet ;;
  click) front; uloop execute-dynamic-code --code "Trial.Boot.TrialProbe.Click($1f, $2f);" 2>&1 | quiet ;;
  move)  front; uloop execute-dynamic-code --code "Trial.Boot.TrialProbe.Move($1f, $2f);" 2>&1 | quiet ;;
  drag)  front; uloop execute-dynamic-code --code "Trial.Boot.TrialProbe.Drag($1f, $2f, $3f, $4f, 350f);" 2>&1 | quiet ;;
  key)   front; uloop execute-dynamic-code --code "Trial.Boot.TrialProbe.KeyTap(\"$1\");" 2>&1 | quiet ;;
  focus) front ;;
  wait)  node -e "setTimeout(()=>{}, $1)" ;;
esac
