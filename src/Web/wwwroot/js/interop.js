// ce que Blazor ne sait pas faire tout seul, appelé depuis Services/Browser.cs
window.horizon = (() => {
  // localStorage KO en navigation privée, on ignore
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
      }
    },
    remove(key) {
      try {
        localStorage.removeItem(key)
      } catch {
      }
    },
  }

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

  const confirmBox = (message) => window.confirm(message)
  const alertBox = (message) => window.alert(message)

  function blobUrl(bytes, mime) {
    return URL.createObjectURL(new Blob([bytes], mime ? { type: mime } : undefined))
  }

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

  // ouvertes avant l'await, sinon popup bloquée
  const windows = new Map()
  let nextWindowId = 1

  function openBlankWindow() {
    const handle = window.open('', '_blank')
    if (!handle) return 0
    const id = nextWindowId++
    windows.set(id, handle)
    return id
  }

  // nouvelle fenêtre si celle ouverte d'avance a été bloquée ou fermée
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

  // aperçu OCR, à libérer avec revokeUrl
  const createUrl = (bytes, mime) => blobUrl(bytes, mime)
  const revokeUrl = (url) => URL.revokeObjectURL(url)

  const copyText = (text) => navigator.clipboard.writeText(text)

  const focus = (element) => element?.focus()
  // ouvre le sélecteur d'un input file masqué
  const click = (element) => element?.click()
  function clearValue(element) {
    if (element) element.value = ''
  }
  function setIndeterminate(element, value) {
    if (element) element.indeterminate = value
  }

  // element = le form ou un parent
  function reportValidity(element) {
    if (!element) return true
    const form = element instanceof HTMLFormElement ? element : element.querySelector('form')
    return form ? form.reportValidity() : true
  }

  function rect(element) {
    const r = element.getBoundingClientRect()
    return { left: r.left, top: r.top, width: r.width, height: r.height }
  }

  // première interaction, voir UnsavedForm.cs
  function trackTouch(element, dotnet) {
    if (!element) return
    const events = ['input', 'change', 'click', 'keydown']
    const touch = () => {
      for (const event of events) element.removeEventListener(event, touch, true)
      dotnet.invokeMethod('Touch')
    }
    for (const event of events) element.addEventListener(event, touch, true)
  }

  // renvoie un id pour unlisten
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

  // seul le message standard du navigateur s'affiche
  function guardUnload(dotnet) {
    window.addEventListener('beforeunload', (event) => {
      if (!dotnet.invokeMethod('HasUnsavedChanges')) return
      event.preventDefault()
      event.returnValue = ''
    })
  }

  function lockScroll(locked) {
    document.body.style.overflow = locked ? 'hidden' : ''
  }

  // comportements natifs non réglables depuis Blazor, pilotés par les attributs data-*
  document.addEventListener('mousedown', (event) => {
    if (event.shiftKey && event.target.closest?.('[data-shift-select]')) event.preventDefault()
  })
  document.addEventListener('keydown', (event) => {
    if (['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key) && event.target.closest?.('[data-nav-keys]')) event.preventDefault()
    if (['Enter', 'Escape'].includes(event.key) && event.target.closest?.('[data-enter-select]')) event.preventDefault()
    // Espace sans défilement de la page (carte du planning)
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

  // synchrone pendant un drag
  const rectSync = (element) => rect(element)

  function scrollTop(element, top) {
    element?.scrollTo({ top })
  }

  // capture pour garder le pointeur même hors de la poignée
  function capturePointer(element, pointerId) {
    try {
      element?.setPointerCapture(pointerId)
    } catch {
      // déjà relâché
    }
  }

  const observers = new Map()
  let nextObserverId = 1

  function observeWidth(element, dotnet) {
    const observer = new ResizeObserver(([entry]) => dotnet.invokeMethodAsync('OnWidth', entry.contentRect.width))
    observer.observe(element)
    const id = nextObserverId++
    observers.set(id, observer)
    return id
  }

  // mesure dans un <span>, pour les étiquettes d'axe du graphique
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


  // éditeur de modèles : DOM manipulé directement, sinon le curseur saute à chaque rendu
  const docEditor = (() => {
    const DRAG_MIME = 'application/x-merge-field'
    const zones = new Map() // key -> { el, dotnet, lastRange }
    let selectionDotnet = null

    // en délégation : setData n'est dispo que dans le vrai dragstart natif
    document.addEventListener('dragstart', (e) => {
      const source = e.target.closest?.('[data-merge-drag]')
      if (!source) return
      const raw = source.getAttribute('data-merge-drag')
      e.dataTransfer.setData(DRAG_MIME, raw)
      try {
        const payload = JSON.parse(raw)
        e.dataTransfer.setData('text/plain', payload.type === 'field' ? `{{${payload.path}}}` : `{{#${payload.groupId}}}`)
      } catch {
        // payload invalide, on continue sans texte brut
      }
      e.dataTransfer.effectAllowed = 'copy'
    })

    // span non éditable avec une croix de suppression
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

    // la ligne modèle sera dupliquée par élément de data-repeat-list à la fusion
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

    // caretRangeFromPoint (Chrome/Safari), sinon caretPositionFromPoint (Firefox)
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

    // point tombé dans un jeton : on insère juste après pour ne pas le couper
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

    // blocs toujours au premier niveau, un <div> dans un <p> serait "corrigé" n'importe comment
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

    // pour les boutons + de la palette, sinon fin de zone
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

    // une seule instance, l'éditeur n'est jamais ouvert deux fois
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

    // rempli par Chromium à l'impression (.pageNumber/.totalPages)
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

    // pas de rangeFromPoint dans un textarea, on insère au curseur côté C#
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
