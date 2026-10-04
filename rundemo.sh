#!/bin/bash
cd C:/mod/work/b35_1353
L=C:/mod/work/skatebird/game/BepInEx/LogOutput.log
powershell -NoProfile -ExecutionPolicy Bypass -File kits/skatebird/check.ps1 -NoGame | tail -3
powershell -NoProfile -ExecutionPolicy Bypass -File kits/skatebird/play.ps1 -Mod C:/mod/work/b35_1353/release -Demo | tail -1
sleep 20
for i in $(seq 1 90); do [ -f $L ] && grep -q SIGF_READY $L && break; sleep 2; done
sleep 2
printf 1 > C:/mod/work/skatebird-rec.txt
echo demo started
