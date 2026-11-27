import { useState } from 'react'
import { api, money, useAuth } from '@platform/shared'
import { Empty, Loading, PageHead } from '../../components/ui'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { addDays, dayInput, startOfDay } from '../../lib/time'
import { useTenant } from '../../tenant'

export default function Hours() {
  const { tenant } = useTenant()
  const { hasRole } = useAuth()
  const [from, setFrom] = useState(dayInput(addDays(startOfDay(new Date()), -14)))
  const [to, setTo] = useState(dayInput(addDays(startOfDay(new Date()), 1)))
  const f = new Date(`${from}T00:00`).toISOString(), t = new Date(`${to}T00:00`).toISOString()
  const report = useAsync(() => (hasRole('Manager') ? api.shifts.hours(f, t) : api.shifts.myHours(f, t)), [from, to])
  const rows = report.data ?? []
  const total = rows.reduce((a, r) => ({ w: a.w + r.workedHours, s: a.s + r.scheduledHours, e: a.e + r.earned }), { w: 0, s: 0, e: 0 })

  return (
    <>
      <PageHead title="Hours & pay" sub="Based on clock-in and clock-out times (max 62 days)" />
      <div className="toolbar">
        <label className="fld inline-fld">From<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label className="fld inline-fld">To<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
      </div>
      {report.error && <Notice>{report.error}</Notice>}
      {report.loading && <Loading />}
      {rows.length === 0 && !report.loading && <Empty>No shifts in this period.</Empty>}
      {rows.length > 0 && (
        <table className="table">
          <thead><tr><th>Staff</th><th>Scheduled (h)</th><th>Worked (h)</th><th>Earned</th></tr></thead>
          <tbody>
            {rows.map(r => <tr key={r.staffMemberId}><td>{r.staffName}</td><td>{r.scheduledHours}</td><td>{r.workedHours}</td><td>{money(r.earned, tenant?.currency)}</td></tr>)}
            <tr className="total"><td>Total</td><td>{total.s.toFixed(2)}</td><td>{total.w.toFixed(2)}</td><td>{money(total.e, tenant?.currency)}</td></tr>
          </tbody>
        </table>
      )}
    </>
  )
}
