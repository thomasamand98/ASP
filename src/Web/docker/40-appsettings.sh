#!/bin/sh
# Lancé par l'image nginx avant le démarrage du serveur : écrit la
# configuration du front (wwwroot/appsettings.json) à partir des variables du
# conteneur, pour qu'une même image serve plusieurs environnements.
#   API_URL       URL de l'API (vide : nom d'hôte de la page + port 3000)
#   LOGIN_SCREEN  true pour afficher l'écran de connexion (défaut : false)
# Sans aucune de ces variables, le fichier publié est gardé tel quel.
set -eu

if [ -z "${API_URL+x}" ] && [ -z "${LOGIN_SCREEN+x}" ]; then
  exit 0
fi

case "${LOGIN_SCREEN:-false}" in
  true | 1) login_screen=true ;;
  *) login_screen=false ;;
esac

# Guillemets et barres obliques inverses échappés pour rester du JSON valide.
api_url=$(printf '%s' "${API_URL:-}" | sed 's/\\/\\\\/g; s/"/\\"/g')

printf '{\n  "ApiUrl": "%s",\n  "LoginScreen": %s\n}\n' "$api_url" "$login_screen" > /usr/share/nginx/html/appsettings.json
