import type { Router } from 'vue-router'

type ClickTrace = {
  ts: string
  route: string
  x: number
  y: number
  tag: string
  id?: string
  classes?: string
  name?: string
  role?: string
  type?: string
  text?: string
}

function pickInteractiveTarget(start: EventTarget | null): HTMLElement | null {
  const node = start instanceof HTMLElement ? start : null
  if (!node) return null
  return node.closest(
    'button, a, input, select, textarea, [role="button"], [role="menuitem"], [data-click-id]'
  )
}

function buildTrace(target: HTMLElement, route: string, event: MouseEvent): ClickTrace {
  const rawText = target.innerText || target.textContent || ''
  const text = rawText.replace(/\s+/g, ' ').trim().slice(0, 120)

  return {
    ts: new Date().toISOString(),
    route,
    x: Math.round(event.clientX),
    y: Math.round(event.clientY),
    tag: target.tagName.toLowerCase(),
    id: target.id || undefined,
    classes: target.className || undefined,
    name: target.getAttribute('name') || undefined,
    role: target.getAttribute('role') || undefined,
    type: target.getAttribute('type') || undefined,
    text: text || undefined,
  }
}

export function installGlobalClickLogger(router: Router) {
  const onClick = (event: MouseEvent) => {
    if (event.button !== 0) return
    const target = pickInteractiveTarget(event.target)
    if (!target) return

    const trace = buildTrace(target, router.currentRoute.value.fullPath, event)
    // Global click trace for debugging user interactions across all features.
    console.debug('[ClickTrace]', trace)
  }

  document.addEventListener('click', onClick, true)
}
