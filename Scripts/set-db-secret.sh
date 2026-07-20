#!/bin/bash
# set-db-secret.sh — macOS variant. Stores the PostgreSQL password in
# ~/Library/Application Support/QuadroApp/db.secret with owner-only permissions.
set -euo pipefail

DATA_DIR="$HOME/Library/Application Support/QuadroApp"
mkdir -p "$DATA_DIR"
PATH_FILE="$DATA_DIR/db.secret"

read -r -s -p "PostgreSQL wachtwoord voor gebruiker 'quadro': " PW
echo
printf '%s' "$PW" > "$PATH_FILE"
chmod 600 "$PATH_FILE"
echo "Secret opgeslagen in $PATH_FILE (permissies 600)."
echo "Zet in appsettings.json: Password=__SECRET__"
