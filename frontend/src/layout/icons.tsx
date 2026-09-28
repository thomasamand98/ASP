import type { ReactNode, SVGProps } from 'react'

// Icônes de menu : traits fins, coins arrondis, une par entrée
// principale du menu (groupes et entrées directes). `currentColor`
// permet à chaque icône de suivre la couleur du texte du bouton
// (normal / survol / actif) sans réglage supplémentaire.
function IconBase({ children, ...props }: SVGProps<SVGSVGElement> & { children: ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="1em"
      height="1em"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      {children}
    </svg>
  )
}

export function DashboardIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M4 18a8 8 0 0 1 16 0" />
      <path d="M12 18 15.5 13" strokeWidth={2.1} />
      <circle cx="12" cy="18" r="1.3" fill="currentColor" stroke="none" />
    </IconBase>
  )
}

export function CommercialIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5Z" />
      <path d="M14 3v5h5" />
      <path d="M9 13h6" />
      <path d="M9 17h6" />
    </IconBase>
  )
}

export function AttelageIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M3 16V7a1 1 0 0 1 1-1h8v10" />
      <path d="M12 10h4.5l3.5 3v3" />
      <circle cx="7" cy="17" r="2" />
      <circle cx="17" cy="17" r="2" />
    </IconBase>
  )
}

export function ProductionIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M3 20V9l6 4V9l6 4V6l6 4v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1Z" />
      <path d="M7 20v-4" />
      <path d="M12 20v-4" />
      <path d="M17 20v-4" />
    </IconBase>
  )
}

export function PersonnelIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <circle cx="12" cy="8" r="3.5" />
      <path d="M5 20a7 7 0 0 1 14 0" />
    </IconBase>
  )
}

export function DocumentaireIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <rect x="4" y="4" width="16" height="4" rx="1" />
      <rect x="4" y="10.5" width="16" height="4" rx="1" />
      <rect x="4" y="17" width="16" height="4" rx="1" />
    </IconBase>
  )
}

export function ConfigurationIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M20 12 16 5 8 5 4 12 8 19 16 19Z" />
      <circle cx="12" cy="12" r="3.2" />
    </IconBase>
  )
}

export function MenuIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M4 6h16" />
      <path d="M4 12h16" />
      <path d="M4 18h16" />
    </IconBase>
  )
}

export function CloseIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M6 6l12 12" />
      <path d="M18 6 6 18" />
    </IconBase>
  )
}

// Bandeau « Tâches à faire » (voir TasksPanel.tsx).
export function TasksIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <IconBase {...props}>
      <path d="M9 5h10" />
      <path d="M9 12h10" />
      <path d="M9 19h10" />
      <path d="m3.5 5 1.2 1.2L7 4" />
      <path d="m3.5 12 1.2 1.2L7 11" />
      <path d="m3.5 19 1.2 1.2L7 18" />
    </IconBase>
  )
}
