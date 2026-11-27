import { useState, type FormEvent } from 'react'
import { api, dateTime, money, useAuth, type CustomerInput } from '@platform/shared'
import { Badge, Empty, Loading, statusTone, useSubmit } from '../../components/ui'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useTenant } from '../../tenant'

type Tab = 'overview' | 'bookings' | 'rewards' | 'profile'

export default function Account() {
  const { tenant } = useTenant()
  const { user } = useAuth()
  const [tab, setTab] = useState<Tab>('overview')
  const loyalty = useAsync(() => api.loyalty.me())
  const history = useAsync(() => api.loyalty.myHistory())
  const bookings = useAsync(() => api.bookings.mine())
  const customer = useAsync(() => api.customers.me())
  const save = useSubmit()
  const pw = useSubmit()
  const [msg, setMsg] = useState<string | null>(null)

  const acct = loyalty.data
  const tierProgress = acct && acct.pointsToNextTier != null
    ? Math.min(100, Math.round((acct.lifetimePoints / (acct.lifetimePoints + acct.pointsToNextTier)) * 100)) : 100
  const upcoming = bookings.data?.items.filter(b => ['Pending', 'Confirmed'].includes(b.status) && new Date(b.startsAt) > new Date()) ?? []

  async function cancel(id: string) {
    await save.run(async () => { await api.bookings.cancel(id); bookings.reload() })
  }

  async function onProfile(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const body: CustomerInput = {
      firstName: String(f.get('firstName')).trim(), lastName: String(f.get('lastName')).trim(),
      phone: String(f.get('phone')).trim() || null, dateOfBirth: String(f.get('dob')) || null,
      gender: String(f.get('gender')) || null, marketingConsent: f.get('consent') === 'on',
    }
    if (await save.run(() => api.customers.updateMe(body))) { setMsg('Your details were saved.'); customer.reload() }
  }

  async function onPassword(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const form = e.currentTarget
    if (await pw.run(() => api.auth.changePassword(String(f.get('current')), String(f.get('next'))))) { setMsg('Password changed. Other devices have been signed out.'); form.reset() }
  }

  return (
    <main className="page-top">
      <div className="wrap">
        <p className="eyebrow">My account</p>
        <h1 className="display sm">Welcome, {user?.firstName}</h1>
        <div className="tabs" role="tablist">
          {(['overview', 'bookings', 'rewards', 'profile'] as Tab[]).map(t => (
            <button key={t} role="tab" aria-selected={tab === t} className={tab === t ? 'on' : ''} onClick={() => { setTab(t); setMsg(null) }}>{t}</button>
          ))}
        </div>
        {msg && <Notice kind="ok">{msg}</Notice>}

        {tab === 'overview' && (
          <div className="grid-2">
            <section className="panel">
              <p className="eyebrow">{acct?.tier ?? '…'} member</p>
              <p className="big">{acct?.pointsBalance.toLocaleString() ?? '–'} <small>points</small></p>
              <div className="progress"><span style={{ width: `${tierProgress}%` }} /></div>
              <p className="small muted">{acct?.nextTier ? `${acct.pointsToNextTier?.toLocaleString()} points to ${acct.nextTier}` : 'You have reached our highest tier.'}</p>
            </section>
            <section className="panel">
              <p className="eyebrow">Next reservation</p>
              {upcoming[0]
                ? <><p><strong>{upcoming[0].serviceName}</strong></p><p className="muted">{dateTime(upcoming[0].startsAt, tenant?.timeZone)}</p><Badge tone={statusTone(upcoming[0].status)}>{upcoming[0].status}</Badge></>
                : <Empty>Nothing booked. Treat yourself.</Empty>}
            </section>
          </div>
        )}

        {tab === 'bookings' && (
          <section>
            {save.error && <Notice>{save.error}</Notice>}
            {bookings.loading && <Loading />}
            {bookings.data?.items.length === 0 && <Empty>No reservations yet.</Empty>}
            <ul className="list">
              {bookings.data?.items.map(b => (
                <li key={b.id}>
                  <div><strong>{b.serviceName}</strong><div className="small muted">{dateTime(b.startsAt, tenant?.timeZone)} · {money(b.price, tenant?.currency)}</div></div>
                  <div className="actions"><Badge tone={statusTone(b.status)}>{b.status}</Badge>
                    {['Pending', 'Confirmed'].includes(b.status) && <button className="link" onClick={() => cancel(b.id)}>Cancel</button>}</div>
                </li>
              ))}
            </ul>
          </section>
        )}

        {tab === 'rewards' && (
          <section>
            {history.data?.items.length === 0 && <Empty>Your points will appear here after your first visit.</Empty>}
            <ul className="list">
              {history.data?.items.map(t => (
                <li key={t.id}><div><strong>{t.reason}</strong><div className="small muted">{dateTime(t.createdAt, tenant?.timeZone)}</div></div>
                  <span className={t.points > 0 ? 'gold' : 'muted'}>{t.points > 0 ? '+' : ''}{t.points}</span></li>
              ))}
            </ul>
          </section>
        )}

        {tab === 'profile' && customer.data && (
          <div className="grid-2">
            <form onSubmit={onProfile} className="form" key={customer.data.id + customer.data.lastName}>
              <h3>Your details</h3>
              <div className="row">
                <label className="fld">First name<input name="firstName" defaultValue={customer.data.firstName} required /></label>
                <label className="fld">Last name<input name="lastName" defaultValue={customer.data.lastName} required /></label>
              </div>
              <label className="fld">Phone<input name="phone" defaultValue={customer.data.phone ?? ''} /></label>
              <div className="row">
                <label className="fld">Date of birth<input name="dob" type="date" defaultValue={customer.data.dateOfBirth ?? ''} /></label>
                <label className="fld">Gender<select name="gender" defaultValue={customer.data.gender ?? ''}><option value="">Prefer not to say</option><option>Female</option><option>Male</option><option>Non-binary</option></select></label>
              </div>
              <label className="check"><input name="consent" type="checkbox" defaultChecked={customer.data.marketingConsent} /> Send me news and offers</label>
              {save.error && <Notice>{save.error}</Notice>}
              <button className="btn" disabled={save.busy}>Save</button>
            </form>
            <form onSubmit={onPassword} className="form">
              <h3>Change password</h3>
              <label className="fld">Current password<input name="current" type="password" required autoComplete="current-password" /></label>
              <label className="fld">New password (12+ characters)<input name="next" type="password" required minLength={12} autoComplete="new-password" /></label>
              {pw.error && <Notice>{pw.error}</Notice>}
              <button className="btn ghost" disabled={pw.busy}>Change password</button>
            </form>
          </div>
        )}
      </div>
    </main>
  )
}
