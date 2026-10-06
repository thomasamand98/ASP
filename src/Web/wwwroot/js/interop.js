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

  // Presse-papier (boutons « Copier » de la page Test OCR).
  const copyText = (text) => navigator.clipboard.writeText(text)

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
  //   - onglets de fiche : flèches, Début et Fin changent d'onglet sans
  //     faire défiler la page (data-nav-keys, voir FicheTabs.razor) ;
  //   - champ de recherche dans une fiche : Entrée choisit un résultat sans
  //     enregistrer la fiche, Échap referme la recherche (data-enter-select) ;
  //   - glisser-déposer (onglets, planning) : effet autorisé et donnée du
  //     glisser posés au départ (data-drag-effect, data-drag-id), effet de
  //     dépôt pendant le survol (data-drop-effect).
  document.addEventListener('mousedown', (event) => {
    if (event.shiftKey && event.target.closest?.('[data-shift-select]')) event.preventDefault()
  })
  document.addEventListener('keydown', (event) => {
    if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key) && event.target.closest?.('[data-nav-keys]')) event.preventDefault()
    if (['Enter', 'Escape'].includes(event.key) && event.target.closest?.('[data-enter-select]')) event.preventDefault()
    // Élément activable au clavier (carte du planning) : Espace ne fait pas
    // défiler la page.
    if (['Enter', ' '].includes(event.key) && event.target.matches?.('[data-activate-keys]')) event.preventDefault()
  })
  document.addEventListener('dragstart', (event) => {
    const source = event.target.closest?.('[data-drag-effect]')
    if (!source) return
    event.dataTransfer.effectAllowed = source.dataset.dragEffect
    if (source.dataset.dragId) event.dataTransfer.setData('text/plain', source.dataset.dragId)
  })
  document.addEventListener('dragover', (event) => {
    const target = event.target.closest?.('[data-drop-effect]')
    if (target) event.dataTransfer.dropEffect = target.dataset.dropEffect
  })

  // Position d'un élément, lue de façon synchrone pendant un glisser.
  const rectSync = (element) => rect(element)

  function scrollTop(element, top) {
    element?.scrollTo({ top })
  }

  // Redimensionnement d'une carte du planning : le pointeur reste capturé
  // par la poignée même s'il en sort.
  function capturePointer(element, pointerId) {
    try {
      element?.setPointerCapture(pointerId)
    } catch {
      // Pointeur déjà relâché.
    }
  }

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

  // Taille [largeur, hauteur] de chaque texte dans la police du document
  // (graphique du tableau de bord : étiquettes d'axe qui tiennent côte à
  // côte), mesurée dans un <span> comme le faisait la bibliothèque de
  // graphiques d'origine.
  function measureTexts(texts, fontSize) {
    const span = document.createElement('span')
    span.setAttribute('aria-hidden', 'true')
    Object.assign(span.style, { position: 'absolute', top: '-20000px', left: '0', padding: '0', margin: '0', border: 'none', whiteSpace: 'pre', fontSize: fontSize + 'px' })
    document.body.appendChild(span)
    const sizes = texts.flatMap((text) => {
      span.textContent = text
      const { width, height } = span.getBoundingClientRect()
      return [width, height]
    })
    span.remove()
    return sizes
  }

  function unobserve(id) {
    observers.get(id)?.disconnect()
    observers.delete(id)
  }


  // ---------- Éditeur de modèles de document ----------
  // Manipulation directe du DOM des trois zones contentEditable (plutôt que
  // de recalculer leur HTML via Blazor à chaque frappe) : c'est le seul
  // moyen d'y insérer un nœud sans faire sauter le curseur/la sélection de
  // l'utilisateur à chaque rendu. Voir Features/Documents/DocumentTemplateEditor.razor.
  const docEditor = (() => {
    const DRAG_MIME = 'application/x-merge-field'
    const zones = new Map() // key -> { el, dotnet, lastRange }
    let selectionDotnet = null

    // Amorce le glisser-déposer depuis la palette : posé en délégation sur
    // tout le document (plutôt qu'un écouteur par ligne de palette, que
    // Blazor ne peut de toute façon pas câbler lui-même — DataTransfer.setData
    // n'existe que depuis le vrai gestionnaire natif 'dragstart'). Chaque
    // ligne glissable porte son payload tout prêt dans `data-merge-drag`
    // (voir MergeFieldPalette.razor).
    document.addEventListener('dragstart', (e) => {
      const source = e.target.closest?.('[data-merge-drag]')
      if (!source) return
      const raw = source.getAttribute('data-merge-drag')
      e.dataTransfer.setData(DRAG_MIME, raw)
      try {
        const payload = JSON.parse(raw)
        e.dataTransfer.setData('text/plain', payload.type === 'field' ? `{{${payload.path}}}` : `{{#${payload.groupId}}}`)
      } catch {
        // Payload invalide : le glisser-déposer continue sans texte brut.
      }
      e.dataTransfer.effectAllowed = 'copy'
    })

    // Jeton de fusion inséré dans le texte : un <span> non éditable portant
    // le chemin du champ, avec une croix de suppression.
    function createChipElement(field) {
      const chip = document.createElement('span')
      chip.className = field.kind === 'image' ? 'dte-chip dte-chip-image' : 'dte-chip'
      chip.contentEditable = 'false'
      chip.setAttribute('data-merge-field', field.path)
      chip.setAttribute('data-kind', field.kind)
      if (field.kind === 'image') {
        const icon = document.createElement('span')
        icon.className = 'dte-chip-image-icon'
        icon.setAttribute('aria-hidden', 'true')
        icon.textContent = '🖼'
        chip.append(icon)
      }
      const label = document.createElement('span')
      label.className = 'dte-chip-label'
      label.textContent = field.label
      chip.append(label)
      const remove = document.createElement('button')
      remove.type = 'button'
      remove.className = 'dte-chip-remove'
      remove.setAttribute('data-chip-remove', '')
      remove.setAttribute('aria-label', `Retirer le champ ${field.label}`)
      remove.textContent = '×'
      chip.append(remove)
      return chip
    }

    // Bloc répétable (tableau) : une ligne d'en-tête + une ligne « modèle »
    // contenant un jeton par colonne (déjà résolues côté C#, voir
    // MergeFieldGroup.Columns). `data-repeat-list` porte le chemin de la
    // collection — au moment de la fusion, la ligne modèle sera dupliquée
    // une fois par élément de la liste.
    function createRepeatBlockElement(group) {
      const columns = group.columns
      if (!columns || columns.length === 0) return null

      const wrapper = document.createElement('div')
      wrapper.className = 'dte-repeat'
      wrapper.setAttribute('data-repeat-list', group.listPath)
      wrapper.setAttribute('data-repeat-label', group.label)

      const tag = document.createElement('div')
      tag.className = 'dte-repeat-tag'
      tag.contentEditable = 'false'
      const tagLabel = document.createElement('span')
      tagLabel.textContent = `↻ Répété pour chaque « ${group.label} »`
      tag.append(tagLabel)
      const tagRemove = document.createElement('button')
      tagRemove.type = 'button'
      tagRemove.className = 'dte-repeat-remove'
      tagRemove.setAttribute('data-repeat-remove', '')
      tagRemove.setAttribute('aria-label', `Supprimer le bloc « ${group.label} »`)
      tagRemove.textContent = '× Supprimer le bloc'
      tag.append(tagRemove)

      const table = document.createElement('table')
      table.className = 'dte-table'
      const thead = document.createElement('thead')
      const headRow = document.createElement('tr')
      for (const field of columns) {
        const th = document.createElement('th')
        th.textContent = field.label
        headRow.append(th)
      }
      thead.append(headRow)
      const tbody = document.createElement('tbody')
      const bodyRow = document.createElement('tr')
      bodyRow.className = 'dte-repeat-row'
      for (const field of columns) {
        const td = document.createElement('td')
        td.append(createChipElement(field))
        bodyRow.append(td)
      }
      tbody.append(bodyRow)
      table.append(thead, tbody)
      wrapper.append(tag, table)
      return wrapper
    }

    // Position de dépose dans le texte (insertion « en ligne », pour un
    // jeton simple) : caretRangeFromPoint (Chrome/Safari) puis repli sur
    // caretPositionFromPoint (Firefox), non standard à des degrés divers
    // mais couvrant les navigateurs de bureau visés par cet outil interne.
    function rangeFromPoint(x, y) {
      if (typeof document.caretRangeFromPoint === 'function') return document.caretRangeFromPoint(x, y)
      if (typeof document.caretPositionFromPoint === 'function') {
        const pos = document.caretPositionFromPoint(x, y)
        if (!pos) return null
        const range = document.createRange()
        range.setStart(pos.offsetNode, pos.offset)
        range.collapse(true)
        return range
      }
      return null
    }

    // Un Range obtenu par point peut tomber au milieu du texte d'un jeton
    // existant (les chips restent des nœuds texte pour le calcul de
    // position, contentEditable="false" empêche seulement l'édition, pas le
    // pointage) : y insérer directement casserait le chip en deux morceaux
    // imbriqués l'un dans l'autre. On ramène alors le point d'insertion
    // juste après ce chip.
    function escapeChipRange(range) {
      const container = range.startContainer
      const el = container.nodeType === Node.TEXT_NODE ? container.parentElement : container
      const chip = el?.closest('.dte-chip')
      if (!chip || !chip.parentNode) return range
      const escaped = document.createRange()
      escaped.setStartAfter(chip)
      escaped.collapse(true)
      return escaped
    }

    // Insère un nœud « en ligne » (jeton de fusion) à l'endroit précis d'un
    // Range, puis replace le curseur juste après.
    function insertInlineNodeAtRange(node, inputRange) {
      const range = escapeChipRange(inputRange)
      range.deleteContents()
      range.insertNode(node)
      const after = document.createRange()
      after.setStartAfter(node)
      after.collapse(true)
      const selection = document.getSelection()
      selection?.removeAllRanges()
      selection?.addRange(after)
    }

    // Insère un nœud « bloc » (tableau répétable, tableau libre, saut de
    // page) : toujours en enfant direct du canvas, jamais au milieu d'un
    // paragraphe — une <div>/<table> insérée via un Range arbitraire
    // produirait du HTML imbriqué invalide (bloc dans un <p>) que le
    // navigateur « corrige » de façon imprévisible. Si `nearPoint` est
    // fourni, le bloc est inséré juste après le bloc de premier niveau situé
    // sous ce point ; sinon il est ajouté à la fin du document.
    function insertBlockNode(canvas, node, nearPoint) {
      let anchor = null
      if (nearPoint) {
        const el = document.elementFromPoint(nearPoint.x, nearPoint.y)
        let candidate = el instanceof Element ? el : null
        while (candidate && candidate.parentElement !== canvas) candidate = candidate.parentElement
        anchor = candidate && candidate.parentElement === canvas ? candidate : null
      }
      if (anchor) anchor.after(node)
      else canvas.append(node)
    }

    // Dernière position connue du curseur dans la zone (mémorisée par
    // onSelectionChange ci-dessous), pour que les boutons « + » de la
    // palette (sans souris sur le canvas) sachent où insérer un champ — à
    // défaut (zone jamais sélectionnée), insère en fin de zone.
    function currentRange(key) {
      const zone = zones.get(key)
      if (!zone) return null
      if (zone.lastRange && zone.el.contains(zone.lastRange.commonAncestorContainer)) return zone.lastRange
      const range = document.createRange()
      range.selectNodeContents(zone.el)
      range.collapse(false)
      return range
    }

    function onSelectionChange() {
      const selection = document.getSelection()
      if (!selection || selection.rangeCount === 0) return
      const range = selection.getRangeAt(0)
      for (const [key, zone] of zones) {
        if (zone.el.contains(range.commonAncestorContainer)) {
          zone.lastRange = range.cloneRange()
          selectionDotnet?.invokeMethodAsync('OnActiveZoneChanged', key)
          break
        }
      }
    }

    // Enregistre le canvas d'une zone (en-tête/contenu/pied) : glisser-
    // déposer depuis la palette, suppression d'un jeton/bloc par clic
    // délégué, et notification de modification/focus vers Blazor.
    function registerZone(el, key, dotnet) {
      zones.set(key, { el, dotnet, lastRange: null })
      el.addEventListener('dragover', (e) => {
        if (!e.dataTransfer.types.includes(DRAG_MIME)) return
        e.preventDefault()
        e.dataTransfer.dropEffect = 'copy'
      })
      el.addEventListener('drop', (e) => {
        const raw = e.dataTransfer.getData(DRAG_MIME)
        if (!raw) return
        e.preventDefault()
        dotnet.invokeMethodAsync('OnZoneDrop', key, raw, e.clientX, e.clientY)
      })
      el.addEventListener('click', (e) => {
        const target = e.target
        const chipRemove = target.closest?.('[data-chip-remove]')
        if (chipRemove) {
          chipRemove.closest('.dte-chip')?.remove()
          dotnet.invokeMethodAsync('OnZoneChanged', key)
          return
        }
        const repeatRemove = target.closest?.('[data-repeat-remove]')
        if (repeatRemove) {
          const block = repeatRemove.closest('.dte-repeat')
          const label = block?.getAttribute('data-repeat-label') ?? 'ce bloc'
          if (block && confirmBox(`Supprimer le bloc répétable « ${label} » ?`)) {
            block.remove()
            dotnet.invokeMethodAsync('OnZoneChanged', key)
          }
        }
      })
      el.addEventListener('input', () => dotnet.invokeMethodAsync('OnZoneChanged', key))
      el.addEventListener('focus', () => dotnet.invokeMethodAsync('OnZoneFocus', key))
    }

    function unregisterZone(key) {
      zones.delete(key)
    }

    // Écouteur global de changement de sélection, pour détecter dans quelle
    // zone se trouve le curseur (voir onSelectionChange) — une seule
    // instance active à la fois (l'éditeur n'est jamais ouvert deux fois).
    function trackSelection(dotnet) {
      selectionDotnet = dotnet
      document.addEventListener('selectionchange', onSelectionChange)
    }

    function untrackSelection() {
      document.removeEventListener('selectionchange', onSelectionChange)
      selectionDotnet = null
    }

    function setZoneHtml(el, html) {
      el.innerHTML = html
    }

    function getZoneHtml(el) {
      return el.innerHTML
    }

    function exec(key, command, value) {
      const zone = zones.get(key)
      zone?.el.focus()
      document.execCommand(command, false, value ?? null)
    }

    function insertField(key, field) {
      const zone = zones.get(key)
      if (!zone) return
      zone.el.focus()
      const range = currentRange(key)
      if (range) insertInlineNodeAtRange(createChipElement(field), range)
    }

    function insertFieldAtPoint(key, field, x, y) {
      const zone = zones.get(key)
      const range = rangeFromPoint(x, y)
      if (!zone || !range) return
      zone.el.focus()
      insertInlineNodeAtRange(createChipElement(field), range)
    }

    function insertGroup(key, group) {
      const zone = zones.get(key)
      const node = createRepeatBlockElement(group)
      if (!zone || !node) return
      zone.el.focus()
      insertBlockNode(zone.el, node)
    }

    function insertGroupAtPoint(key, group, x, y) {
      const zone = zones.get(key)
      const node = createRepeatBlockElement(group)
      if (!zone || !node) return
      insertBlockNode(zone.el, node, { x, y })
    }

    function insertFreeTable(key) {
      const zone = zones.get(key)
      if (!zone) return
      const table = document.createElement('table')
      table.className = 'dte-table dte-table-free'
      const tbody = document.createElement('tbody')
      for (let row = 0; row < 2; row++) {
        const tr = document.createElement('tr')
        for (let col = 0; col < 2; col++) {
          const td = document.createElement('td')
          td.innerHTML = '<br>'
          tr.append(td)
        }
        tbody.append(tr)
      }
      table.append(tbody)
      insertBlockNode(zone.el, table)
    }

    function insertPageBreak(key) {
      const zone = zones.get(key)
      if (!zone) return
      const marker = document.createElement('div')
      marker.className = 'dte-page-break'
      marker.contentEditable = 'false'
      marker.innerHTML = '<span>Saut de page</span>'
      insertBlockNode(zone.el, marker)
    }

    // Numéro de page (en-tête/pied uniquement) : pas un champ de fusion — un
    // texte littéral que Chromium remplit lui-même à l'impression (classes
    // spéciales .pageNumber/.totalPages). Inséré comme un bloc non éditable,
    // au même titre qu'un jeton, pour ne pas être modifié par erreur.
    function insertPageNumber(key) {
      const zone = zones.get(key)
      if (!zone) return
      const node = document.createElement('span')
      node.className = 'dte-chip dte-chip-pagenum'
      node.contentEditable = 'false'
      node.innerHTML = 'Page <span class="pageNumber"></span> / <span class="totalPages"></span>'
      zone.el.focus()
      const range = currentRange(key)
      if (range) insertInlineNodeAtRange(node, range)
    }

    // Glisser-déposer depuis la palette pendant l'édition du code HTML brut
    // (voir DocumentTemplateEditor.razor, codeZone) : pas d'équivalent de
    // rangeFromPoint pour un <textarea> (son contenu n'est pas du DOM
    // adressable par point), donc insertion à la position du curseur —
    // transmise à Blazor pour que l'insertion dans la chaîne reste en C#.
    function registerCodeZone(el, key, dotnet) {
      el.addEventListener('dragover', (e) => {
        if (!e.dataTransfer.types.includes(DRAG_MIME)) return
        e.preventDefault()
        e.dataTransfer.dropEffect = 'copy'
      })
      el.addEventListener('drop', (e) => {
        const raw = e.dataTransfer.getData(DRAG_MIME)
        if (!raw) return
        e.preventDefault()
        dotnet.invokeMethodAsync('OnCodeDrop', key, raw, el.selectionStart ?? 0, el.selectionEnd ?? 0)
      })
    }

    function getTextareaSelection(el) {
      return [el.selectionStart ?? 0, el.selectionEnd ?? 0]
    }

    function focusAndSelect(el, start, end) {
      el.focus()
      el.setSelectionRange(start, end)
    }

    return {
      registerZone,
      unregisterZone,
      registerCodeZone,
      trackSelection,
      untrackSelection,
      setZoneHtml,
      getZoneHtml,
      exec,
      insertField,
      insertFieldAtPoint,
      insertGroup,
      insertGroupAtPoint,
      insertFreeTable,
      insertPageBreak,
      insertPageNumber,
      getTextareaSelection,
      focusAndSelect,
    }
  })()

  return {
    docEditor,
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
    copyText,
    focus,
    click,
    clearValue,
    setIndeterminate,
    reportValidity,
    rect,
    rectSync,
    scrollTop,
    capturePointer,
    trackTouch,
    listenEscape,
    listenOutside,
    unlisten,
    guardUnload,
    lockScroll,
    observeWidth,
    measureTexts,
    unobserve,
  }
})()
