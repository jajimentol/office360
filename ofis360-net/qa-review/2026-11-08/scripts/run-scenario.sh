#!/usr/bin/env bash
# Kullanim: ./run-scenario.sh <ad> <vus> <sure_s> <ramp_s> <think_ms>
set -e
NAME=$1; VUS=$2; DUR=$3; RAMP=$4; THINK=$5
node monitor.mjs --name=$NAME --seconds=$((DUR + RAMP / 2 + 15)) > ../results/monitor-$NAME.log 2>&1 &
MON=$!
node load.mjs --name=$NAME --vus=$VUS --duration=$DUR --ramp=$RAMP --think=$THINK | tee ../results/load-$NAME.log
wait $MON
cat ../results/monitor-$NAME.log
