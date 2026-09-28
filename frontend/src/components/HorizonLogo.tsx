import { useId } from 'react'

// Logo Horizon (source : horizon-logo.svg). Intégré en composant plutôt
// qu'en <img> pour que le nom suive la couleur du texte (`currentColor`) et
// reste lisible en thème sombre. Les ids des dégradés sont rendus uniques
// par instance (useId) : deux logos dans la page ne se marchent pas dessus.
// viewBox recadrée sur le dessin (l’original laisse une marge vide à droite).
export function HorizonLogo({ className }: { className?: string }) {
  const uid = useId()
  const id = (name: string) => `${uid}-${name}`

  return (
    <svg className={className} xmlns="http://www.w3.org/2000/svg" viewBox="18 18 426 144" role="img" aria-label="Horizon">
      <defs>
        <linearGradient id={id('sun')} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#FFB547" />
          <stop offset="1" stopColor="#FF5E62" />
        </linearGradient>
        <linearGradient id={id('sea')} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#3B5BDB" />
          <stop offset="1" stopColor="#1B2A5E" />
        </linearGradient>
        <clipPath id={id('badge')}>
          <circle cx="90" cy="90" r="70" />
        </clipPath>
        <clipPath id={id('sky')}>
          <rect x="0" y="0" width="180" height="96" />
        </clipPath>
      </defs>

      {/* Emblème */}
      <g clipPath={`url(#${id('badge')})`}>
        <rect x="0" y="0" width="180" height="180" fill="#FFF4E6" />
        <g clipPath={`url(#${id('sky')})`}>
          <circle cx="90" cy="100" r="42" fill={`url(#${id('sun')})`} />
        </g>
        <rect x="0" y="96" width="180" height="90" fill={`url(#${id('sea')})`} />
        <g fill="#FF8A5B">
          <rect x="62" y="108" width="56" height="5" rx="2.5" />
          <rect x="72" y="121" width="36" height="5" rx="2.5" opacity="0.8" />
          <rect x="81" y="134" width="18" height="5" rx="2.5" opacity="0.6" />
        </g>
      </g>
      <rect x="20" y="94" width="140" height="4" rx="2" fill="#1B2A5E" />

      {/* Nom */}
      <text
        x="190"
        y="110"
        fontFamily="'Inter','Helvetica Neue',Helvetica,Arial,sans-serif"
        fontSize="72"
        fontWeight="700"
        letterSpacing="-2"
        fill="currentColor"
      >
        horizon
      </text>
    </svg>
  )
}
