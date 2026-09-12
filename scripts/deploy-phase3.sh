#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
python "$(dirname -- "${BASH_SOURCE[0]}")/phase3_deploy.py"
