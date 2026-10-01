import { lazy, Suspense, useEffect, useLayoutEffect, useRef, useState, type ReactElement, type ReactNode } from 'react'
import { Sidebar, type MenuNode } from './Sidebar.js'
import { TabBar } from './TabBar.js'
import { TasksPanel } from './TasksPanel.js'
import { TabsContext, type OpenTabRequest } from './TabsContext.js'
import { ProjectionProvider } from '../components/projection/ProjectionProvider.js'
import { GuardBoundary } from '../components/unsaved-changes/UnsavedChangesProvider.js'
import { useConfirmLeave, useGuardScope } from '../components/unsaved-changes/UnsavedChangesContext.js'
import type { GuardScope } from '../components/unsaved-changes/scope.js'
import './AppLayout.css'
import {
  DashboardIcon,
  CommercialIcon,
  AttelageIcon,
  ProductionIcon,
  PersonnelIcon,
  DocumentaireIcon,
  ConfigurationIcon,
  MenuIcon,
  CloseIcon,
} from './icons.js'
import { Dev } from '../features/enDeveloppement/Dev.js'
import { SocietesPage } from '../features/societes/SocietesPage.js'
import { ContratsPage } from '../features/contrats/ContratsPage.js'
import { ContactsPage } from '../features/contacts/ContactsPage.js'
import { PointsPage } from '../features/points/PointsPage.js'
import { ConditionsPage } from '../features/conditions/ConditionsPage.js'
import { MarchandisesPage } from '../features/marchandises/MarchandisesPage.js'
import { ChauffeursPage } from '../features/chauffeurs/ChauffeursPage.js'
import { VehiculesPage } from '../features/vehicules/VehiculesPage.js'
import { AttelagesPage } from '../features/attelages/AttelagesPage.js'
import { CommandesPage } from '../features/commandes/CommandesPage.js'

// Pages lourdes ou peu fréquentées : chargées à leur première ouverture
// plutôt qu'avec le reste de l'application (voir le <Suspense> plus bas).
const PlanningPage = lazy(() => import('../features/planning/PlanningPage.js').then((m) => ({ default: m.PlanningPage })))
const PersonnelPage = lazy(() => import('../features/personnel/PersonnelPage.js').then((m) => ({ default: m.PersonnelPage })))
const PointagePage = lazy(() => import('../features/pointage/PointagePage.js').then((m) => ({ default: m.PointagePage })))
const EntitePage = lazy(() => import('../features/entite/EntitePage.js').then((m) => ({ default: m.EntitePage })))
const EnumerationsPage = lazy(() => import('../features/enumerations/EnumerationsPage.js').then((m) => ({ default: m.EnumerationsPage })))
const DocumentTemplatesPage = lazy(() =>
  import('../features/documents/DocumentTemplatesPage.js').then((m) => ({ default: m.DocumentTemplatesPage }))
)
const OcrPage = lazy(() => import('../features/ocr/OcrPage.js').then((m) => ({ default: m.OcrPage })))
const PerformancePage = lazy(() => import('../features/performance/PerformancePage.js').then((m) => ({ default: m.PerformancePage })))
const DashboardPage = lazy(() => import('../features/dashboard/DashboardPage.js').then((m) => ({ default: m.DashboardPage })))

type MenuLeafConfig = { id: string; label: string; icon?: ReactNode; render: () => ReactElement }
type MenuGroupConfig = { id: string; label: string; icon?: ReactNode; items: MenuLeafConfig[] }

// Menu de navigation : une entrée sans `items` (ex. "Tableau de bord")
// est directement sélectionnable. "Commercial" regroupe les tables
// métier — pour ajouter un thème, ajoute un groupe ici ; pour ajouter
// une table à un thème existant, ajoute un item à son tableau `items`.
// L'icône n'est affichée que sur les entrées principales, pas sur les
// items d'un groupe déroulé.
const MENU: (MenuLeafConfig | MenuGroupConfig)[] = [
  { id: 'dashboard', label: 'Tableau de bord', icon: <DashboardIcon />, render: () => <DashboardPage /> },
  {
    id: 'commercial',
    label: 'Commercial',
    icon: <CommercialIcon />,
    items: [
      { id: 'societes', label: 'Société', render: () => <SocietesPage /> },
      { id: 'contrats', label: 'Contrats / Offre de prix', render: () => <ContratsPage /> },
      { id: 'contacts', label: 'Contacts', render: () => <ContactsPage /> },
      { id: 'point', label: 'Point', render: () => <PointsPage /> },
      { id: 'conditions', label: "Conditions d'exécution", render: () => <ConditionsPage /> },
      { id: 'marchandises', label: 'Marchandises', render: () => <MarchandisesPage /> },
    ],
  },
  {
    id: 'attelage',
    label: 'Gestion attelage',
    icon: <AttelageIcon />,
    items: [
      { id: 'chauffeurs', label: 'Chauffeurs', render: () => <ChauffeursPage /> },
      { id: 'vehicules', label: 'Véhicules', render: () => <VehiculesPage /> },
      { id: 'attelages', label: 'Attelages', render: () => <AttelagesPage /> },
    ]
  },
  {
    id: 'production',
    label: 'Production',
    icon: <ProductionIcon />,
    items: [
      { id: 'commandes', label: 'Commandes', render: () => <CommandesPage /> },
      { id: 'planning', label: 'Planning', render: () => <PlanningPage /> },
      { id: 'executions', label: 'Exécutions', render: () => <Dev /> },
    ]
  },
  {
    id: 'personnel',
    label: 'Gestion personnel',
    icon: <PersonnelIcon />,
    items: [
      { id: 'personnel', label: 'Personnel', render: () => <PersonnelPage /> },
      { id: 'pointage', label: 'Pointage', render: () => <PointagePage /> },
    ]
  },
  { id: 'documentaire', label: 'Gestion documentaire', icon: <DocumentaireIcon />, render: () => <DocumentTemplatesPage /> },
  {
    id: 'configuration',
    label: 'Configuration',
    icon: <ConfigurationIcon />,
    items: [
      { id: 'general', label: 'Général', render: () => <Dev /> },
      { id: 'societe', label: 'Société', render: () => <EntitePage /> },
      { id: 'bdd', label: 'Base de données', render: () => <Dev /> },
      { id: 'comptabilite', label: 'Comptabilité', render: () => <Dev /> },
      { id: 'enumerations', label: 'Enumérations', render: () => <EnumerationsPage /> },
      { id: 'outils', label: 'Outils', render: () => <Dev /> },
      { id: 'ocr', label: 'Test OCR', render: () => <OcrPage /> },
      { id: 'performance', label: 'Performance', render: () => <PerformancePage /> },
      { id: 'donnees', label: 'Données brutes', render: () => <Dev /> },
      { id: 'import-export', label: 'Import/Export', render: () => <Dev /> },
    ]
  },
]

const MENU_NODES: MenuNode[] = MENU

const MENU_ITEMS: MenuLeafConfig[] = MENU.flatMap((node) => ('items' in node ? node.items : [node]))

const ACTIVE_MENU_STORAGE_KEY = 'mvc-template:activeMenuId'
const OPEN_TABS_STORAGE_KEY = 'mvc-template:openTabs'
const DEFAULT_TAB_ID = 'dashboard'

// Liste des onglets ouverts lors du dernier passage — filtrée sur les
// entrées de menu qui existent toujours, jamais vide (retombe sur le
// Tableau de bord).
function getInitialOpenTabIds(): string[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(OPEN_TABS_STORAGE_KEY) ?? 'null')
    const valid = Array.isArray(stored) ? stored.filter((id): id is string => typeof id === 'string' && MENU_ITEMS.some((item) => item.id === id)) : []
    return valid.length > 0 ? valid : [DEFAULT_TAB_ID]
  } catch {
    return [DEFAULT_TAB_ID]
  }
}

// Dernier onglet actif — doit faire partie des onglets ouverts ci-dessus,
// sinon on retombe sur le premier d'entre eux.
function getInitialActiveId(openTabIds: string[]): string {
  const stored = localStorage.getItem(ACTIVE_MENU_STORAGE_KEY)
  if (stored && openTabIds.includes(stored)) return stored
  return openTabIds[0]
}

// Zone de saisie d'un onglet (voir components/unsaved-changes/) : fermer
// l'onglet vérifie d'abord les fiches modifiées qu'il contient, y compris
// celles ouvertes dans une page tableau.
function TabBoundary({ id, scopes, children }: { id: string; scopes: Map<string, GuardScope>; children: ReactNode }) {
  const scope = useGuardScope()
  useEffect(() => {
    scopes.set(id, scope)
    return () => {
      if (scopes.get(id) === scope) scopes.delete(id)
    }
  }, [id, scope, scopes])
  return <GuardBoundary scope={scope}>{children}</GuardBoundary>
}

export function AppLayout() {
  // Onglets ouverts façon navigateur (voir TabBar.tsx) : cliquer sur une
  // entrée du menu déjà ouverte l'active simplement, sinon un nouvel
  // onglet est ajouté à la suite — chaque page ouverte reste montée en
  // arrière-plan (juste masquée, voir le rendu plus bas) pour conserver
  // son état (filtres, pagination, ligne sélectionnée...) au changement
  // d'onglet.
  const [openTabIds, setOpenTabIds] = useState(getInitialOpenTabIds)
  const [activeId, setActiveId] = useState(() => getInitialActiveId(openTabIds))
  const [mobileOpen, setMobileOpen] = useState(false)
  // Onglets ouverts dynamiquement (mode "Onglet" de CrudPage, voir
  // view-modes/TabMode.tsx) : une fiche par ligne double-cliquée, en plus
  // des entrées fixes du menu ci-dessus. Ne survivent pas à un rechargement
  // (leur contenu capture des callbacks liés à la session en cours) —
  // `getInitialOpenTabIds` les filtre déjà en ne gardant que les ids
  // présents dans MENU_ITEMS.
  const [dynamicTabs, setDynamicTabs] = useState<Record<string, { label: string; content: ReactNode }>>({})
  // Le contenu d'un onglet dynamique est capturé une fois pour toutes (voir
  // openOrActivateTab) : ses callbacks (ex. le bouton Annuler d'une fiche)
  // referment alors pour toujours le `closeTab` de CE rendu-là. Sans cette
  // ref, `handleCloseTab` lirait un `activeId` figé au moment où l'onglet a
  // été ouvert au lieu de l'onglet réellement actif au moment du clic.
  const activeIdRef = useRef(activeId)
  useLayoutEffect(() => {
    activeIdRef.current = activeId
  }, [activeId])
  const [tabScopes] = useState(() => new Map<string, GuardScope>())
  const confirmLeave = useConfirmLeave()

  // Bloque le scroll de la page pendant que le tiroir mobile est ouvert.
  useEffect(() => {
    if (!mobileOpen) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [mobileOpen])

  useEffect(() => {
    localStorage.setItem(OPEN_TABS_STORAGE_KEY, JSON.stringify(openTabIds))
  }, [openTabIds])

  useEffect(() => {
    localStorage.setItem(ACTIVE_MENU_STORAGE_KEY, activeId)
  }, [activeId])

  // Sélection depuis le menu : ajoute l'onglet s'il n'est pas déjà ouvert,
  // puis l'active dans tous les cas (jamais de doublon).
  function handleSelect(itemId: string) {
    setOpenTabIds((prev) => (prev.includes(itemId) ? prev : [...prev, itemId]))
    setActiveId(itemId)
  }

  // Ferme un onglet (de menu ou dynamique) : si ce n'était pas l'onglet
  // actif, rien d'autre à faire. Sinon, bascule sur son voisin (précédent,
  // sinon suivant) ou, s'il ne reste plus rien, rouvre le Tableau de bord.
  // Sert aussi de `closeTab` du TabsContext (voir plus bas) — un onglet
  // dynamique retire en plus son contenu de `dynamicTabs`. Une saisie
  // modifiée dans l'onglet déclenche d'abord « Enregistrer / Annuler les
  // modifications », sauf avec `force`.
  async function handleCloseTab(itemId: string, options?: { force?: boolean }) {
    const scope = tabScopes.get(itemId)
    if (!options?.force && scope && !(await confirmLeave(scope))) return
    setOpenTabIds((prev) => {
      const index = prev.indexOf(itemId)
      const remaining = prev.filter((id) => id !== itemId)
      if (itemId !== activeIdRef.current) return remaining
      const fallbackId = remaining[index - 1] ?? remaining[index] ?? DEFAULT_TAB_ID
      activeIdRef.current = fallbackId
      setActiveId(fallbackId)
      return remaining.includes(fallbackId) ? remaining : [...remaining, fallbackId]
    })
    setDynamicTabs((prev) => {
      if (!(itemId in prev)) return prev
      const next = { ...prev }
      delete next[itemId]
      return next
    })
  }

  // Glisser-déposer d'un onglet (voir TabBar.tsx) : remplace simplement
  // l'ordre, l'onglet actif ne change pas.
  function handleReorderTabs(nextIds: string[]) {
    setOpenTabIds(nextIds)
  }

  // Ouvre la fiche d'un enregistrement dans son propre onglet (mode
  // "Onglet" de CrudPage, voir view-modes/TabMode.tsx), ou réactive celui
  // déjà ouvert pour cet id sans toucher à son contenu (pour ne pas perdre
  // la saisie en cours si on redouble-clique la même ligne).
  function openOrActivateTab({ id, label, content }: OpenTabRequest) {
    setDynamicTabs((prev) => (id in prev ? prev : { ...prev, [id]: { label, content } }))
    setOpenTabIds((prev) => (prev.includes(id) ? prev : [...prev, id]))
    setActiveId(id)
  }

  function getTabLabel(id: string): string {
    return dynamicTabs[id]?.label ?? MENU_ITEMS.find((item) => item.id === id)?.label ?? id
  }

  function getTabContent(id: string): ReactNode {
    return dynamicTabs[id]?.content ?? MENU_ITEMS.find((item) => item.id === id)?.render() ?? null
  }

  const openTabs = openTabIds.map((id) => ({ id, label: getTabLabel(id) }))

  return (
    <TabsContext.Provider value={{ openOrActivateTab, closeTab: (id, options) => void handleCloseTab(id, options) }}>
      <ProjectionProvider>
        <div className="app-layout">
          <button
            type="button"
            className="app-layout-menu-toggle"
            onClick={() => setMobileOpen((prev) => !prev)}
            aria-label={mobileOpen ? 'Fermer le menu' : 'Ouvrir le menu'}
            aria-expanded={mobileOpen}
          >
            {mobileOpen ? <CloseIcon /> : <MenuIcon />}
          </button>
          <Sidebar
            nodes={MENU_NODES}
            activeId={activeId}
            onSelect={handleSelect}
            open={mobileOpen}
            onClose={() => setMobileOpen(false)}
          />
          <div className="app-content">
            <TabBar tabs={openTabs} activeId={activeId} onSelect={setActiveId} onClose={(id) => void handleCloseTab(id)} onReorder={handleReorderTabs} />
            <div className="app-content-page">
              {openTabIds.map((id) => (
                <div key={id} hidden={id !== activeId}>
                  <TabBoundary id={id} scopes={tabScopes}>
                    <Suspense fallback={<p className="app-content-loading">Chargement...</p>}>{getTabContent(id)}</Suspense>
                  </TabBoundary>
                </div>
              ))}
            </div>
          </div>
          <TasksPanel />
        </div>
      </ProjectionProvider>
    </TabsContext.Provider>
  )
}
