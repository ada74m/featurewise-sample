#!/usr/bin/env bash
# Deploys your own environment from your working tree: builds a package as package.sh does, then applies
# DevEnvironment, which uses it. Arguments go to terraform apply; name_prefix is required, such as
# -var name_prefix=adam.
set -euo pipefail
cd "$(dirname "$0")"

./package.sh dev out
cd DevEnvironment
terraform init -input=false
terraform apply "$@"
