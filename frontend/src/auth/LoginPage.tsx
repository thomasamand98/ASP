import { useState, type FormEvent } from 'react'
import { HorizonLogo } from '../components/HorizonLogo.js'
import './LoginPage.css'

type Props = {
  onLogin: (login: string, password: string) => Promise<void>
}

// Écran de connexion affiché par AuthProvider tant que l'utilisateur n'est
// pas identifié. Les messages d'erreur (identifiants manquants, trop de
// tentatives...) sont ceux du serveur, affichés tels quels.
export function LoginPage({ onLogin }: Props) {
  const [login, setLogin] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await onLogin(login, password)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Connexion impossible')
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit}>
        <HorizonLogo className="login-logo" />
        <h1 className="login-title">Connexion</h1>
        <p className="login-subtitle">Identifiez-vous pour accéder à l’application.</p>

        <label className="field login-field">
          Identifiant
          <input value={login} onChange={(e) => setLogin(e.target.value)} autoComplete="username" autoFocus required />
        </label>
        <label className="field login-field">
          Mot de passe
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {error && (
          <p className="alert-error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" className="btn primary login-button" disabled={submitting}>
          {submitting ? 'Connexion...' : 'Se connecter'}
        </button>
      </form>
    </div>
  )
}
