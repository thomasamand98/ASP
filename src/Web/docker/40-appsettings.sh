#!/bin/sh
# réécrit appsettings.json depuis API_URL / LOGIN_SCREEN, rien si aucune des deux
set -eu

if [ -z "${API_URL+x}" ] && [ -z "${LOGIN_SCREEN+x}" ]; then
  exit 0
fi

case "${LOGIN_SCREEN:-false}" in
  true | 1) login_screen=true ;;
  *) login_screen=false ;;
esac

# échappe \ et " pour rester du JSON valide
api_url=$(printf '%s' "${API_URL:-}" | sed 's/\\/\\\\/g; s/"/\\"/g')

printf '{\n  "ApiUrl": "%s",\n  "LoginScreen": %s\n}\n' "$api_url" "$login_screen" > /usr/share/nginx/html/appsettings.json
