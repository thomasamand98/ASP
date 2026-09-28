import { useEffect, useState } from 'react'

// Thème clair/sombre de toute l'application. Le thème appliqué est porté par
// l'attribut `data-theme` de <html>, que styles/tokens.css lit pour choisir
// ses jetons de couleur. « system » suit le réglage du système d'exploitation,
// y compris quand il change pendant que l'application est ouverte.
// Le script en tête de index.html applique le même choix avant le premier
// rendu (pas de flash de l'autre thème au chargement) : garder la même clé.
export type ThemePreference = 'light' | 'dark' | 'system'

const STORAGE_KEY = 'theme'
const systemDark = () => window.matchMedia('(prefers-color-scheme: dark)')

function readPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored === 'light' || stored === 'dark') return stored
  } catch {
    // Stockage indisponible (navigation privée, cookies bloqués) : défaut.
  }
  return 'system'
}

function applyTheme(preference: ThemePreference) {
  const dark = preference === 'dark' || (preference === 'system' && systemDark().matches)
  document.documentElement.dataset.theme = dark ? 'dark' : 'light'
}

export function useTheme() {
  const [preference, setPreference] = useState<ThemePreference>(readPreference)

  useEffect(() => {
    applyTheme(preference)
    try {
      if (preference === 'system') localStorage.removeItem(STORAGE_KEY)
      else localStorage.setItem(STORAGE_KEY, preference)
    } catch {
      // Préférence non mémorisée, mais appliquée pour cette session.
    }
    if (preference !== 'system') return
    const media = systemDark()
    const onChange = () => applyTheme('system')
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [preference])

  return { preference, setPreference }
}
