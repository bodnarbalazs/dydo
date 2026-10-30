#!/bin/bash
set -euo pipefail
mkdir /source
tar -xf /input/source.tar -C /source
cd /source
node setup-skills.mjs > /evidence/linux-setup.txt 2>&1
export TLA2TOOLS=/input/tla2tools.jar
export JAVA=/usr/bin/java
export STATECHART_DIFFERENTIAL_SEED=415
export XDG_CACHE_HOME=/tmp/dyd415-render
export npm_config_cache=/tmp/dyd415-npm
cd skills/engineering/statechart/scripts
npm install --ignore-scripts --no-audit --no-fund
{
  uname -a
  node --version
  npm --version
  java -version
  sha256sum /input/source.tar "$TLA2TOOLS"
  stat -c '%a %n' statechart test/java.ts
  node -e 'const fs=require("fs");console.log(JSON.stringify({command:require("./package.json").scripts.test,files:fs.readdirSync("test").filter(f=>f.endsWith(".test.ts"))},null,2))'
} > /evidence/linux-environment.txt 2>&1
export NODE_V8_COVERAGE=/tmp/dyd415-linux-v8
set +e
npm test > /evidence/linux-suite.txt 2>&1
verdict=$?
set -e
printf '%s\n' "$verdict" > /evidence/linux-suite.exit
unset NODE_V8_COVERAGE
node /input/filter-v8.cjs /tmp/dyd415-linux-v8 /tmp/maintained-v8 /evidence/linux-coverage-inventory.json
tar -czf /evidence/linux-v8.tar.gz -C /tmp maintained-v8
exit "$verdict"
