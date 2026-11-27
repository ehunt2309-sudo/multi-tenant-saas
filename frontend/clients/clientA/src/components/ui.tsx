import { useEffect, useRef, useState, type ReactNode } from 'react'
import { errorMessage } from './Status'

/** Native <dialog>: focus trapping, Escape-to-close and backdrop come for free. */
export function Modal({ open, title, onClose, children, wide }: { open: boolean; title: string; onClose: () => void; children: ReactNode; wide?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const d = ref.current
    if (!d) return
    if (open && !d.open) d.showModal()
    if (!open && d.open) d.close()
  }, [open])
  return (
    <dialog ref={ref} className={`modal ${wide ? 'wide' : ''}`} onClose={onClose}
            onClick={e => { if (e.target === ref.current) onClose() }}>
      {open && (
        <div className="modal-body">
          <header><h3>{title}</h3><button className="icon-btn" onClick={onClose} aria-label="Close">×</button></header>
          {children}
        </div>
      )}
    </dialog>
  )
}

export function Badge({ tone = 'neutral', children }: { tone?: 'neutral' | 'ok' | 'warn' | 'bad' | 'gold'; children: ReactNode }) {
  return <span className={`badge ${tone}`}>{children}</span>
}

export const statusTone = (s: string) =>
  s === 'Confirmed' ? 'gold' : s === 'Completed' ? 'ok' : s === 'Cancelled' || s === 'NoShow' ? 'bad' : 'warn'

export const Loading = () => <p className="muted">Loading…</p>
export const Empty = ({ children }: { children: ReactNode }) => <p className="muted empty">{children}</p>

export function Stat({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return (
    <div className="stat">
      <p className="stat-label">{label}</p>
      <p className="stat-value">{value}</p>
      {hint && <p className="stat-hint">{hint}</p>}
    </div>
  )
}

export function PageHead({ title, sub, action }: { title: string; sub?: string; action?: ReactNode }) {
  return (
    <div className="page-head">
      <div><h1>{title}</h1>{sub && <p className="muted">{sub}</p>}</div>
      {action}
    </div>
  )
}

export function Pager({ page, total, pageSize, onPage }: { page: number; total: number; pageSize: number; onPage: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  if (pages <= 1) return null
  return (
    <div className="pager">
      <button className="btn ghost sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <span className="muted small">Page {page} of {pages}</span>
      <button className="btn ghost sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button>
    </div>
  )
}

/** Wraps a save action with busy + error state. `run` resolves true on success. */
export function useSubmit() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function run(fn: () => Promise<unknown>): Promise<boolean> {
    setBusy(true); setError(null)
    try { await fn(); return true }
    catch (e) { setError(errorMessage(e)); return false }
    finally { setBusy(false) }
  }
  return { busy, error, run, clear: () => setError(null) }
}

/** Deterministic "photograph" stand-in: layered gold light on dark stone. Swap for real imagery later. */
export function Art({ seed, label, tall }: { seed: string; label?: string; tall?: boolean }) {
  let h = 0
  for (const c of seed) h = (h * 31 + c.charCodeAt(0)) >>> 0
  const x = 20 + (h % 60), y = 15 + ((h >> 3) % 55), x2 = 80 - (h % 50)
  return (
    <div className={`art ${tall ? 'tall' : ''}`} aria-hidden="true"
         style={{ background: `radial-gradient(70% 60% at ${x}% ${y}%, color-mix(in srgb, var(--accent) 38%, transparent), transparent 62%), radial-gradient(50% 50% at ${x2}% 90%, color-mix(in srgb, var(--accent) 16%, transparent), transparent 70%), linear-gradient(160deg, #1d1c1a, #0f0f10)` }}>
      <span>{label ?? seed.slice(0, 1)}</span>
    </div>
  )
}
