import { useState, type FormEvent } from 'react'
import { api, dateTime, money, type BookingStatus } from '@platform/shared'
import { Badge, Empty, Loading, Modal, PageHead, Pager, statusTone, useSubmit } from '../../components/ui'
import { Notice, errorMessage } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { addDays, fromLocalInput, startOfDay } from '../../lib/time'
import { useTenant } from '../../tenant'

const NEXT: Record<string, BookingStatus[]> = {
  Pending: ['Confirmed', 'Cancelled'],
  Confirmed: ['Completed', 'NoShow', 'Cancelled'],
}
const RANGES: Record<string, [number, number]> = { 'Today': [0, 1], 'Next 7 days': [0, 7], 'Next 30 days': [0, 30], 'Last 7 days': [-7, 1], 'Last 30 days': [-30, 1] }

export default function Bookings() {
  const { tenant } = useTenant()
  const [range, setRange] = useState('Next 7 days')
  const [status, setStatus] = useState<BookingStatus | ''>('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  const [a, b] = RANGES[range]
  const from = addDays(startOfDay(new Date()), a).toISOString()
  const to = addDays(startOfDay(new Date()), b).toISOString()
  const list = useAsync(() => api.bookings.list({ from, to, status: status || undefined, page }), [range, status, page])

  async function change(id: string, s: BookingStatus) {
    try { await api.bookings.setStatus(id, s); setErr(null); list.reload() } catch (e) { setErr(errorMessage(e)) }
  }

  return (
    <>
      <PageHead title="Bookings" sub="Confirm, complete and create reservations"
        action={<button className="btn sm" onClick={() => setCreating(true)}>New booking</button>} />
      <div className="toolbar">
        <select value={range} onChange={e => { setRange(e.target.value); setPage(1) }}>{Object.keys(RANGES).map(r => <option key={r}>{r}</option>)}</select>
        <select value={status} onChange={e => { setStatus(e.target.value as BookingStatus | ''); setPage(1) }}>
          <option value="">All statuses</option>{['Pending', 'Confirmed', 'Completed', 'Cancelled', 'NoShow'].map(s => <option key={s}>{s}</option>)}</select>
      </div>
      {(err || list.error) && <Notice>{err ?? list.error}</Notice>}
      {list.loading && <Loading />}
      {list.data?.items.length === 0 && <Empty>No bookings in this view.</Empty>}
      {list.data && list.data.items.length > 0 && (
        <table className="table">
          <thead><tr><th>When</th><th>Customer</th><th>Experience</th><th>With</th><th>Price</th><th>Status</th><th /></tr></thead>
          <tbody>{list.data.items.map(x => (
            <tr key={x.id}>
              <td>{dateTime(x.startsAt, tenant?.timeZone)}</td><td>{x.customerName}</td><td>{x.serviceName}</td><td>{x.staffName ?? '—'}</td>
              <td>{money(x.price, tenant?.currency)}</td><td><Badge tone={statusTone(x.status)}>{x.status}</Badge></td>
              <td className="actions">{(NEXT[x.status] ?? []).map(s => <button key={s} className="link" onClick={() => change(x.id, s)}>{s}</button>)}</td>
            </tr>))}</tbody>
        </table>
      )}
      {list.data && <Pager page={page} total={list.data.total} pageSize={list.data.pageSize} onPage={setPage} />}
      <NewBooking open={creating} onClose={() => setCreating(false)} onDone={() => { setCreating(false); list.reload() }} />
    </>
  )
}

function NewBooking({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const customers = useAsync(() => api.customers.list(undefined, 1, 100))
  const services = useAsync(() => api.services.list())
  const staff = useAsync(() => api.staff.public())
  const s = useSubmit()

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const ok = await s.run(() => api.bookings.create({
      customerId: String(f.get('customer')), serviceOfferingId: String(f.get('service')),
      staffMemberId: String(f.get('staff')) || null, startsAt: fromLocalInput(String(f.get('when'))),
      partySize: Number(f.get('party')) || 1, notes: String(f.get('notes')).trim() || null,
    }))
    if (ok) onDone()
  }

  return (
    <Modal open={open} title="New booking" onClose={onClose}>
      <form onSubmit={onSubmit} className="form">
        <label className="fld">Customer<select name="customer" required defaultValue="">
          <option value="" disabled>Choose…</option>{customers.data?.items.map(c => <option key={c.id} value={c.id}>{c.firstName} {c.lastName}</option>)}</select></label>
        <label className="fld">Experience<select name="service" required defaultValue="">
          <option value="" disabled>Choose…</option>{services.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label className="fld">Practitioner<select name="staff" defaultValue=""><option value="">No preference</option>{staff.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <div className="row"><label className="fld">Date &amp; time<input name="when" type="datetime-local" required /></label>
          <label className="fld">Guests<input name="party" type="number" min={1} max={50} defaultValue={1} /></label></div>
        <label className="fld">Notes<textarea name="notes" rows={2} maxLength={500} /></label>
        {s.error && <Notice>{s.error}</Notice>}
        <button className="btn" disabled={s.busy}>Create booking</button>
      </form>
    </Modal>
  )
}
