import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, dateTime, money, useAuth } from '@platform/shared'
import BarChart from '../../components/BarChart'
import { Badge, Empty, Loading, PageHead, Stat, statusTone } from '../../components/ui'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { addDays, startOfDay } from '../../lib/time'
import { useTenant } from '../../tenant'

export default function Overview() {
  const { hasRole, user } = useAuth()
  return hasRole('Manager') ? <ManagerOverview /> : <StaffOverview name={user?.firstName ?? ''} />
}

function ManagerOverview() {
  const { tenant } = useTenant()
  const [days, setDays] = useState(30)
  const from = addDays(startOfDay(new Date()), -days).toISOString()
  const to = addDays(startOfDay(new Date()), 1).toISOString()
  const summary = useAsync(() => api.analytics.summary(from, to), [days])
  const revenue = useAsync(() => api.analytics.revenueByDay(from, to), [days])
  const top = useAsync(() => api.analytics.topCustomers(8))
  const demo = useAsync(() => api.analytics.demographics())
  const today = useAsync(() => api.bookings.list({ from: startOfDay(new Date()).toISOString(), to: addDays(startOfDay(new Date()), 1).toISOString() }))
  const s = summary.data

  return (
    <>
      <PageHead title="Overview" sub="How the business is doing"
        action={<select value={days} onChange={e => setDays(Number(e.target.value))}>
          <option value={7}>Last 7 days</option><option value={30}>Last 30 days</option><option value={90}>Last 90 days</option></select>} />
      {summary.error && <Notice>{summary.error}</Notice>}
      <div className="kpis">
        <Stat label="Revenue" value={s ? money(s.revenue, tenant?.currency) : '–'} hint="Completed bookings" />
        <Stat label="Bookings" value={s?.totalBookings ?? '–'} hint={s ? Object.entries(s.bookingsByStatus).map(([k, v]) => `${v} ${k}`).join(' · ') : ''} />
        <Stat label="New customers" value={s?.newCustomers ?? '–'} />
        <Stat label="Guests booked" value={s?.uniqueCustomersBooked ?? '–'} hint="Unique customers" />
      </div>

      <div className="panel"><h3>Revenue by day</h3>
        <BarChart data={(revenue.data ?? []).map(d => ({ label: new Date(d.day).toLocaleDateString('en-ZA', { day: 'numeric', month: 'short', timeZone: 'UTC' }), value: d.revenue }))} currency={tenant?.currency} />
      </div>

      <div className="grid-2">
        <div className="panel"><h3>Today</h3>
          {today.loading && <Loading />}
          {today.data?.items.length === 0 && <Empty>No bookings today.</Empty>}
          <ul className="list">{today.data?.items.map(b => (
            <li key={b.id}><div><strong>{b.customerName}</strong> · {b.serviceName}<div className="small muted">{dateTime(b.startsAt, tenant?.timeZone)}</div></div><Badge tone={statusTone(b.status)}>{b.status}</Badge></li>))}</ul>
          <Link to="/portal/bookings" className="link small">Manage bookings →</Link>
        </div>
        <div className="panel"><h3>Top customers</h3>
          <table className="table"><thead><tr><th>Customer</th><th>Spend</th><th>Visits</th></tr></thead>
            <tbody>{top.data?.map(c => <tr key={c.customerId}><td>{c.name}</td><td>{money(c.totalSpend, tenant?.currency)}</td><td>{c.visitCount}</td></tr>)}</tbody></table>
          {top.data?.length === 0 && <Empty>Spend will appear after the first completed booking.</Empty>}
        </div>
      </div>

      <div className="panel"><h3>Who your customers are</h3>
        <table className="table"><thead><tr><th>Age</th><th>Gender</th><th>Customers</th><th>Total spend</th><th>Avg visits</th></tr></thead>
          <tbody>{demo.data?.map(r => <tr key={r.ageBand + r.gender}><td>{r.ageBand}</td><td>{r.gender}</td><td>{r.customers}</td><td>{money(r.totalSpend, tenant?.currency)}</td><td>{r.averageVisits}</td></tr>)}</tbody></table>
        {demo.data?.length === 0 && <Empty>No customers yet.</Empty>}
      </div>
    </>
  )
}

function StaffOverview({ name }: { name: string }) {
  const { tenant } = useTenant()
  const from = startOfDay(new Date()).toISOString()
  const to = addDays(startOfDay(new Date()), 1).toISOString()
  const bookings = useAsync(() => api.bookings.list({ from, to }))
  const shifts = useAsync(() => api.shifts.mine(from, addDays(startOfDay(new Date()), 7).toISOString()))
  return (
    <>
      <PageHead title={`Hello, ${name}`} sub="Your day at a glance" />
      <div className="grid-2">
        <div className="panel"><h3>Today's bookings</h3>
          {bookings.data?.items.length === 0 && <Empty>No bookings today.</Empty>}
          <ul className="list">{bookings.data?.items.map(b => (
            <li key={b.id}><div><strong>{b.customerName}</strong> · {b.serviceName}<div className="small muted">{dateTime(b.startsAt, tenant?.timeZone)}</div></div><Badge tone={statusTone(b.status)}>{b.status}</Badge></li>))}</ul>
        </div>
        <div className="panel"><h3>My next shifts</h3>
          {shifts.data?.length === 0 && <Empty>No shifts this week.</Empty>}
          <ul className="list">{shifts.data?.map(s => <li key={s.id}><span>{dateTime(s.startsAt, tenant?.timeZone)}</span><span className="muted small">→ {dateTime(s.endsAt, tenant?.timeZone)}</span></li>)}</ul>
          <Link to="/portal/schedule" className="link small">Clock in / out →</Link>
        </div>
      </div>
    </>
  )
}
