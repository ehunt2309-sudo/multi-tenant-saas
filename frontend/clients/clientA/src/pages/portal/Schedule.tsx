import { useState, type FormEvent } from 'react'
import { api, useAuth, type Shift } from '@platform/shared'
import { Badge, Empty, Loading, Modal, PageHead, useSubmit } from '../../components/ui'
import { Notice, errorMessage } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { addDays, dayInput, fromLocalInput, hhmm, startOfWeek } from '../../lib/time'
import { useTenant } from '../../tenant'

export default function Schedule() {
  const { tenant } = useTenant()
  const { hasRole } = useAuth()
  const isManager = hasRole('Manager')
  const [week, setWeek] = useState(startOfWeek(new Date()))
  const [view, setView] = useState<'all' | 'mine'>(isManager ? 'all' : 'mine')
  const [adding, setAdding] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  const from = week.toISOString(), to = addDays(week, 7).toISOString()
  const shifts = useAsync(() => (view === 'all' ? api.shifts.list(from, to) : api.shifts.mine(from, to)), [week, view])

  const byDay = new Map<string, Shift[]>()
  shifts.data?.forEach(s => { const k = dayInput(new Date(s.startsAt)); byDay.set(k, [...(byDay.get(k) ?? []), s]) })
  const days = Array.from({ length: 7 }, (_, i) => addDays(week, i))

  async function act(fn: () => Promise<unknown>) {
    try { await fn(); setErr(null); shifts.reload() } catch (e) { setErr(errorMessage(e)) }
  }

  return (
    <>
      <PageHead title="Schedule" sub={isManager ? 'Plan the week and manage shifts' : 'Your shifts. Clock in when you arrive.'}
        action={isManager && <button className="btn sm" onClick={() => setAdding(true)}>Add shift</button>} />
      <div className="toolbar">
        <button className="btn ghost sm" onClick={() => setWeek(addDays(week, -7))}>← Previous</button>
        <strong>{week.toLocaleDateString('en-ZA', { day: 'numeric', month: 'long' })} – {addDays(week, 6).toLocaleDateString('en-ZA', { day: 'numeric', month: 'long' })}</strong>
        <button className="btn ghost sm" onClick={() => setWeek(addDays(week, 7))}>Next →</button>
        <button className="btn ghost sm" onClick={() => setWeek(startOfWeek(new Date()))}>This week</button>
        {isManager && <select value={view} onChange={e => setView(e.target.value as 'all' | 'mine')}><option value="all">All staff</option><option value="mine">Only mine</option></select>}
      </div>
      {(err || shifts.error) && <Notice>{err ?? shifts.error}</Notice>}
      {shifts.loading && <Loading />}
      <div className="week">
        {days.map(d => {
          const list = byDay.get(dayInput(d)) ?? []
          return (
            <section key={d.toISOString()} className="day">
              <h4>{d.toLocaleDateString('en-ZA', { weekday: 'short', day: 'numeric' })}</h4>
              {list.length === 0 && <p className="small muted">—</p>}
              {list.map(s => (
                <div key={s.id} className="shift">
                  <strong>{hhmm(s.startsAt, tenant?.timeZone)}–{hhmm(s.endsAt, tenant?.timeZone)}</strong>
                  <span className="small">{s.staffName}</span>
                  {s.clockOutAt ? <Badge tone="ok">Worked</Badge> : s.clockInAt ? <Badge tone="gold">On shift</Badge> : null}
                  <div className="actions">
                    {view === 'mine' && !s.clockInAt && <button className="link" onClick={() => act(() => api.shifts.clockIn(s.id))}>Clock in</button>}
                    {view === 'mine' && s.clockInAt && !s.clockOutAt && <button className="link" onClick={() => act(() => api.shifts.clockOut(s.id))}>Clock out</button>}
                    {isManager && !s.clockInAt && <button className="link danger" onClick={() => confirm('Delete this shift?') && act(() => api.shifts.remove(s.id))}>Delete</button>}
                  </div>
                </div>
              ))}
            </section>
          )
        })}
      </div>
      {shifts.data?.length === 0 && <Empty>No shifts this week.</Empty>}
      {isManager && <AddShift open={adding} onClose={() => setAdding(false)} onDone={() => { setAdding(false); shifts.reload() }} defaultDay={week} />}
      <p className="small muted">All times are shown in venue time ({tenant?.timeZone}).</p>
    </>
  )
}

function AddShift({ open, onClose, onDone, defaultDay }: { open: boolean; onClose: () => void; onDone: () => void; defaultDay: Date }) {
  const staff = useAsync(() => api.staff.list())
  const s = useSubmit()
  const d = dayInput(defaultDay)
  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (await s.run(() => api.shifts.create({
      staffMemberId: String(f.get('staff')), startsAt: fromLocalInput(String(f.get('start'))),
      endsAt: fromLocalInput(String(f.get('end'))), notes: String(f.get('notes')).trim() || null,
    }))) onDone()
  }
  return (
    <Modal open={open} title="Add shift" onClose={onClose}>
      <form onSubmit={onSubmit} className="form">
        <label className="fld">Staff member<select name="staff" required defaultValue="">
          <option value="" disabled>Choose…</option>{staff.data?.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.firstName} {x.lastName}</option>)}</select></label>
        <div className="row"><label className="fld">Starts<input name="start" type="datetime-local" required defaultValue={`${d}T09:00`} /></label>
          <label className="fld">Ends<input name="end" type="datetime-local" required defaultValue={`${d}T17:00`} /></label></div>
        <label className="fld">Notes<input name="notes" maxLength={500} /></label>
        {s.error && <Notice>{s.error}</Notice>}
        <button className="btn" disabled={s.busy}>Add shift</button>
      </form>
    </Modal>
  )
}
