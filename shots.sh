#!/bin/bash
# shots.sh N gap : N game screenshots, gap seconds apart (min 3 per showcase call)
cd C:/mod/work/b35_1353
for i in $(seq 1 $1); do powershell -NoProfile -ExecutionPolicy Bypass -File C:/mod/repo/machine/showcase/showcase.ps1 -Game -Seconds 3 | tail -1 | sed 's/.*: //'; sleep $2; done
