import { ChiffreAffairesCard } from './ChiffreAffairesCard.js'

// Tableau de bord (onglet ouvert par défaut, voir layout/AppLayout.tsx).
export function DashboardPage() {
  return (
    <div>
      <div className="page-header">
        <h2>Tableau de bord</h2>
      </div>
      <ChiffreAffairesCard />
    </div>
  )
}
