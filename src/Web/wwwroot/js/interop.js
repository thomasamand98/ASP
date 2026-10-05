// Interop JavaScript minimal : uniquement ce que Blazor ne sait pas faire
// lui-même (stockage local, fichiers, fenêtres, focus, validation native,
// écouteurs au niveau du document, mesures de position). Appelé depuis
// Services/Browser.cs ; aucun autre fichier JavaScript dans l'application.
window.horizon = (() => {
  // ---------- Stockage local (préférences de l'utilisateur) ----------
  // Indisponible en navigation privée ou cookies bloqués : lectures vides,
  // écritures ignorées (comme les try/catch du front d'origine).
  const storage = {
    get(key) {
      try {
        return localStorage.getItem(key)
      } catch {
        return null
      }
    },
    set(key, value) {
      try {
        localStorage.setItem(key, value)
      } catch {
        // Préférence non mémorisée.
      }
    },
    remove(key) {
      try {
        localStorage.removeItem(key)
      } catch {
        // Rien à faire.
      }
    },
  }

  // ---------- Thème ----------
  const systemDark = () => window.matchMedia('(prefers-color-scheme: dark)')
  let themeListener = null

  function applyTheme(preference) {
    const dark = preference === 'dark' || (preference === 'system' && systemDark().matches)
    document.documentElement.dataset.theme = dark ? 'dark' : 'light'
    if (themeListener) systemDark().removeEventListener('change', themeListener)
    themeListener = null
    if (preference === 'system') {
      themeListener = () => applyTheme('system')
      systemDark().addEventListener('change', themeListener)
    }
  }

  // ---------- Boîtes natives ----------
  const confirmBox = (message) => window.confirm(message)
  const alertBox = (message) => window.alert(message)

  // ---------- Fichiers ----------
  function blobUrl(bytes, mime) {
    return URL.createObjectURL(new Blob([bytes], mime ? { type: mime } : undefined))
  }

  // Téléchargement d'un fichier généré ou reçu (export Excel, document joint).
  function download(fileName, bytes, mime) {
    const url = blobUrl(bytes, mime)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    setTimeout(() => URL.revokeObjectURL(url), 60_000)
  }

  // Fenêtres ouvertes avant une attente réseau : ouvrir une fenêtre après un
  // `await` n'est plus considéré comme déclenché par le clic par la plupart
  // des navigateurs, qui bloqueraient le popup. Identifiées par un numéro.
  const windows = new Map()
  let nextWindowId = 1

  function openBlankWindow() {
    const handle = window.open('', '_blank')
    if (!handle) return 0
    const id = nextWindowId++
    windows.set(id, handle)
    return id
  }

  // Affiche le fichier dans la fenêtre ouverte d'avance (ou une nouvelle si
  // elle a été bloquée ou fermée).
  function showInWindow(id, bytes, mime) {
    const url = blobUrl(bytes, mime)
    const handle = windows.get(id)
    windows.delete(id)
    if (handle && !handle.closed) handle.location.href = url
    else window.open(url, '_blank')
  }

  function closeWindow(id) {
    windows.get(id)?.close()
    windows.delete(id)
  }

  // Aperçu local d'un fichier choisi (page OCR) ; libéré par revokeUrl.
  const createUrl = (bytes, mime) => blobUrl(bytes, mime)
  const revokeUrl = (url) => URL.revokeObjectURL(url)

  // ---------- Éléments ----------
  const focus = (element) => element?.focus()
  // Bouton qui ouvre le sélecteur de fichiers d'un <input type="file"> masqué ;
  // vider sa valeur permet de re-sélectionner le même fichier après une erreur.
  const click = (element) => element?.click()
  function clearValue(element) {
    if (element) element.value = ''
  }
  function setIndeterminate(element, value) {
    if (element) element.indeterminate = value
  }

  // Contrôle natif des champs (requis, formats) : le navigateur signale le
  // premier champ invalide. `element` est le formulaire ou le contient.
  function reportValidity(element) {
    if (!element) return true
    const form = element instanceof HTMLFormElement ? element : element.querySelector('form')
    return form ? form.reportValidity() : true
  }

  function rect(element) {
    const r = element.getBoundingClientRect()
    return { left: r.left, top: r.top, width: r.width, height: r.height }
  }

  // Première interaction avec une fiche (saisie, clic, touche) : à partir de
  // là, ses modifications comptent (voir UnsavedChanges/UnsavedForm.cs).
  function trackTouch(element, dotnet) {
    if (!element) return
    const events = ['input', 'change', 'click', 'keydown']
    const touch = () => {
      for (const event of events) element.removeEventListener(event, touch, true)
      dotnet.invokeMethod('Touch')
    }
    for (const event of events) element.addEventListener(event, touch, true)
  }

  // ---------- Écouteurs du document ----------
  // Échap et clic en dehors d'un élément (menus, panneaux). Renvoie un
  // numéro à passer à `unlisten`.
  const listeners = new Map()
  let nextListenerId = 1

  function listenEscape(dotnet, capture) {
    const handler = (event) => {
      if (event.key !== 'Escape') return
      if (dotnet.invokeMethod('OnEscape')) event.stopPropagation()
    }
    document.addEventListener('keydown', handler, capture)
    const id = nextListenerId++
    listeners.set(id, () => document.removeEventListener('keydown', handler, capture))
    return id
  }

  function listenOutside(element, dotnet) {
    const handler = (event) => {
      if (!element.contains(event.target)) dotnet.invokeMethodAsync('OnOutside')
    }
    document.addEventListener('pointerdown', handler)
    const id = nextListenerId++
    listeners.set(id, () => document.removeEventListener('pointerdown', handler))
    return id
  }

  function unlisten(id) {
    listeners.get(id)?.()
    listeners.delete(id)
  }

  // Fermeture ou rechargement de l'onglet du navigateur avec une saisie
  // modifiée : seul le message standard du navigateur peut s'afficher.
  function guardUnload(dotnet) {
    window.addEventListener('beforeunload', (event) => {
      if (!dotnet.invokeMethod('HasUnsavedChanges')) return
      event.preventDefault()
      event.returnValue = ''
    })
  }

  // Bloque le défilement de la page (tiroir du menu mobile ouvert).
  function lockScroll(locked) {
    document.body.style.overflow = locked ? 'hidden' : ''
  }

  // Comportements du navigateur que Blazor ne peut pas régler par événement :
  //   - Maj+clic sur une ligne sélectionnable d'un tableau : pas de
  //     surlignage du texte (voir DataTable.razor, data-shift-select) ;
  //   - glisser-déposer des onglets : curseur « déplacer » (data-drag-move).
  document.addEventListener('mousedown', (event) => {
    if (event.shiftKey && event.target.closest?.('[data-shift-select]')) event.preventDefault()
  })
  //   - onglets de fiche : flèches, Début et Fin changent d'onglet sans
  //     faire défiler la page (data-nav-keys, voir FicheTabs.razor) ;
  //   - champ de recherche dans une fiche : Entrée choisit un résultat sans
  //     enregistrer la fiche, Échap referme la recherche (data-enter-select) ;
  document.addEventListener('keydown', (event) => {
    if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key) && event.target.closest?.('[data-nav-keys]')) event.preventDefault()
    if (['Enter', 'Escape'].includes(event.key) && event.target.closest?.('[data-enter-select]')) event.preventDefault()
  })
  document.addEventListener('dragstart', (event) => {
    if (event.target.closest?.('[data-drag-move]')) event.dataTransfer.effectAllowed = 'move'
  })
  document.addEventListener('dragover', (event) => {
    if (event.target.closest?.('[data-drag-move]')) event.dataTransfer.dropEffect = 'move'
  })

  // Taille d'un élément, suivie (graphique du tableau de bord).
  const observers = new Map()
  let nextObserverId = 1

  function observeWidth(element, dotnet) {
    const observer = new ResizeObserver(([entry]) => dotnet.invokeMethodAsync('OnWidth', entry.contentRect.width))
    observer.observe(element)
    const id = nextObserverId++
    observers.set(id, observer)
    return id
  }

  function unobserve(id) {
    observers.get(id)?.disconnect()
    observers.delete(id)
  }

  return {
    storage,
    applyTheme,
    confirm: confirmBox,
    alert: alertBox,
    download,
    openBlankWindow,
    showInWindow,
    closeWindow,
    createUrl,
    revokeUrl,
    focus,
    click,
    clearValue,
    setIndeterminate,
    reportValidity,
    rect,
    trackTouch,
    listenEscape,
    listenOutside,
    unlisten,
    guardUnload,
    lockScroll,
    observeWidth,
    unobserve,
  }
})()
