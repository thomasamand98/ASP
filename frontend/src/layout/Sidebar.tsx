import { useState, type ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext.js'
import { useConfirmLeave } from '../components/unsaved-changes/UnsavedChangesContext.js'
import { HorizonLogo } from '../components/HorizonLogo.js'
import { useTheme, type ThemePreference } from '../lib/theme'
import './Sidebar.css'

const THEMES: { id: ThemePreference; label: string; title: string }[] = [
  { id: 'light', label: 'Clair', title: 'Thème clair' },
  { id: 'dark', label: 'Sombre', title: 'Thème sombre' },
  { id: 'system', label: 'Auto', title: 'Suivre le réglage du système' },
]

export type MenuItem = {
  id: string
  label: string
  icon?: ReactNode
}

export type MenuGroup = {
  id: string
  label: string
  icon?: ReactNode
  items: MenuItem[]
}

// Une entrée de menu est soit un groupe déroulable (avec des items),
// soit une entrée simple directement sélectionnable (ex. "Tableau de
// bord") — distinguée par la présence de `items`.
export type MenuNode = MenuItem | MenuGroup

function isGroup(node: MenuNode): node is MenuGroup {
  return 'items' in node
}

type SidebarProps = {
  nodes: MenuNode[]
  activeId: string
  onSelect: (itemId: string) => void
  // Contrôlent le tiroir mobile (voir le media query dans Sidebar.css) ;
  // sans effet en desktop où la sidebar reste toujours visible.
  open: boolean
  onClose: () => void
}

// Menu latéral générique : chaque groupe (ex. "Commercial") est
// déroulable et liste des items (ex. les tables) ; cliquer sur un item
// le sélectionne via onSelect, à charge de l'appelant d'afficher le
// contenu correspondant. Une entrée sans sous-liste (ex. "Tableau de
// bord") se sélectionne directement, sans flèche ni déroulé. En dessous
// de 768px, la sidebar devient un tiroir masqué par défaut (piloté par
// `open`/`onClose`) : sélectionner un item la referme automatiquement.
export function Sidebar({ nodes, activeId, onSelect, open, onClose }: SidebarProps) {
  const { login, canSignOut, signOut } = useAuth()
  // Se déconnecter quitte toutes les saisies ouvertes : la sidebar est hors
  // des onglets, sa zone est celle de toute l'application.
  const confirmLeave = useConfirmLeave()
  const { preference, setPreference } = useTheme()
  const [openGroups, setOpenGroups] = useState<Set<string>>(
    () =>
      new Set(
        nodes.filter((node): node is MenuGroup => isGroup(node) && node.items.some((item) => item.id === activeId)).map((group) => group.id),
      ),
  )

  function toggleGroup(groupId: string) {
    setOpenGroups((prev) => {
      const next = new Set(prev)
      if (next.has(groupId)) next.delete(groupId)
      else next.add(groupId)
      return next
    })
  }

  function handleSelect(itemId: string) {
    onSelect(itemId)
    onClose()
  }

  return (
    <>
      {open && <div className="sidebar-backdrop" onClick={onClose} />}
      <nav className={`sidebar${open ? ' open' : ''}`}>
        <HorizonLogo className="sidebar-logo" />
        {nodes.map((node) => {
          if (!isGroup(node)) {
            return (
              <button
                key={node.id}
                type="button"
                className={`sidebar-leaf${node.id === activeId ? ' active' : ''}`}
                onClick={() => handleSelect(node.id)}
              >
                {node.icon && <span className="sidebar-icon">{node.icon}</span>}
                {node.label}
              </button>
            )
          }

          const isOpen = openGroups.has(node.id)
          return (
            <div key={node.id} className="sidebar-group">
              <button
                type="button"
                className="sidebar-group-title"
                onClick={() => toggleGroup(node.id)}
                aria-expanded={isOpen}
              >
                <span className="sidebar-label">
                  {node.icon && <span className="sidebar-icon">{node.icon}</span>}
                  {node.label}
                </span>
                <span className={`sidebar-chevron${isOpen ? ' open' : ''}`}>▸</span>
              </button>
              {isOpen && (
                <ul className="sidebar-items">
                  {node.items.map((item) => (
                    <li key={item.id}>
                      <button
                        type="button"
                        className={`sidebar-item${item.id === activeId ? ' active' : ''}`}
                        onClick={() => handleSelect(item.id)}
                      >
                        {item.label}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )
        })}

        <div className="sidebar-theme" role="group" aria-label="Thème">
          <span className="sidebar-theme-label">Thème</span>
          <div className="sidebar-theme-options">
            {THEMES.map((theme) => (
              <button
                key={theme.id}
                type="button"
                title={theme.title}
                aria-pressed={preference === theme.id}
                className={preference === theme.id ? 'active' : undefined}
                onClick={() => setPreference(theme.id)}
              >
                {theme.label}
              </button>
            ))}
          </div>
        </div>

        {/* Utilisateur connecté et déconnexion — uniquement quand l'écran de
            connexion est activé (voir auth/AuthProvider.tsx). */}
        {canSignOut && (
          <div className="sidebar-user">
            <span className="sidebar-user-name" title={login}>{login}</span>
            <button type="button" className="sidebar-logout" onClick={async () => { if (await confirmLeave()) await signOut() }}>
              Se déconnecter
            </button>
          </div>
        )}
      </nav>
    </>
  )
}
