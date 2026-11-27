import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, dateTime, money, useAuth } from '@platform/shared'
import { Loading } from '../../components/ui'
import { Notice, errorMessage } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { dayInput } from '../../lib/time'
import { useTenant } from '../../tenant'

const OPEN = 9 * 60, CLOSE = 18 * 60   // demo opening hours: 09:00-18:00

function slotsFor(date: string, duration: number): string[] {
  const out: string[] = []
  const now = Date.now()
  for (let m = OPEN; m + duration <= CLOSE; m += 30) {
    const t = `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`
    if (new Date(`${date}T${t}`).getTime() > now) out.push(t)
  }
  return out
}

const validEmail = (v: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim())

type Confirmed = { serviceName: string; startsAt: string; guest: boolean; email?: string }

export default function Reserve() {
  const { tenant } = useTenant()
  const { user } = useAuth()
  const [params] = useSearchParams()
  const isCustomer = user?.role === 'Customer'
  const isGuest = !user
  const services = useAsync(() => api.services.list())
  const staff = useAsync(() => api.staff.public())
  // Members only: their past reservations tell us who they like.
  const history = useAsync(() => (isCustomer ? api.bookings.mine() : Promise.resolve(null)), [isCustomer])

  const [serviceId, setServiceId] = useState(params.get('service') ?? '')
  const [staffId, setStaffId] = useState('')
  const [staffTouched, setStaffTouched] = useState(false)
  const [date, setDate] = useState('')
  const [time, setTime] = useState('')
  const [party, setParty] = useState(1)
  const [notes, setNotes] = useState('')
  const [first, setFirst] = useState('')
  const [last, setLast] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState<Confirmed | null>(null)

  // "Your usual": the practitioner this member has booked most often (cancelled visits don't count).
  const usualId = useMemo(() => {
    const counts = new Map<string, number>()
    for (const b of history.data?.items ?? [])
      if (b.staffMemberId && b.status !== 'Cancelled') counts.set(b.staffMemberId, (counts.get(b.staffMemberId) ?? 0) + 1)
    return [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? ''
  }, [history.data])

  useEffect(() => {
    if (usualId && !staffTouched) setStaffId(usualId)
  }, [usualId, staffTouched])

  const service = services.data?.find(s => s.id === serviceId)
  const slots = useMemo(() => (service && date ? slotsFor(date, service.durationMinutes) : []), [service, date])
  const detailsOk = !isGuest || (first.trim() && last.trim() && validEmail(email))
  const ready = !!service && !!date && !!time && !!detailsOk
  const canSubmit = ready && (isGuest || isCustomer)
  const here = `/reserve${serviceId ? `?service=${serviceId}` : ''}`
  const startsAt = () => new Date(`${date}T${time}`).toISOString()

  async function confirm() {
    if (!canSubmit) return
    setBusy(true); setError(null)
    try {
      if (isGuest) {
        const g = await api.bookings.createGuest({
          serviceOfferingId: serviceId, startsAt: startsAt(), partySize: party, notes: notes.trim() || null,
          firstName: first.trim(), lastName: last.trim(), email: email.trim(), phone: phone.trim() || null,
        })
        setDone({ serviceName: g.serviceName, startsAt: g.startsAt, guest: true, email: email.trim() })
      } else {
        const b = await api.bookings.create({
          serviceOfferingId: serviceId, staffMemberId: staffId || null,
          startsAt: startsAt(), partySize: party, notes: notes.trim() || null,
        })
        setDone({ serviceName: b.serviceName, startsAt: b.startsAt, guest: false })
      }
    } catch (e) { setError(errorMessage(e)) } finally { setBusy(false) }
  }

  if (done) {
    return (
      <main className="page-top"><div className="wrap narrow center confirm">
        <div className="tick" aria-hidden="true">✓</div>
        <h1 className="display sm">Your request is in</h1>
        <p className="muted">{done.serviceName} · {dateTime(done.startsAt, tenant?.timeZone)}</p>
        {done.guest ? (
          <>
            <p className="muted">We'll confirm shortly by email to {done.email}.</p>
            <div className="locked" style={{ textAlign: 'left', marginTop: '2rem' }}>
              <strong>Next time, make it yours</strong>
              <span className="small muted">Create a free account to choose your practitioner, keep a history of your visits and earn Inner Circle rewards. We'll remember who you love.</span>
              <p style={{ margin: '.6rem 0 0' }}><Link to="/login?mode=register" className="btn ghost sm">Create an account</Link></p>
            </div>
          </>
        ) : (
          <>
            <p className="muted">We'll confirm shortly. You can manage this reservation from your account.</p>
            <Link to="/account" className="btn">View my reservations</Link>
          </>
        )}
      </div></main>
    )
  }

  return (
    <main className="page-top">
      <div className="wrap reserve">
        <div>
          <p className="eyebrow">Reserve</p>
          <h1 className="display sm">Your reservation</h1>

          <h2 className="step"><span>1</span> Choose an experience</h2>
          {services.loading && <Loading />}
          {services.error && <Notice>{services.error}</Notice>}
          <div className="choices">
            {services.data?.map(s => (
              <button key={s.id} type="button" className={`choice ${serviceId === s.id ? 'selected' : ''}`} onClick={() => { setServiceId(s.id); setTime('') }}>
                <strong>{s.name}</strong>
                <span className="small muted">{s.durationMinutes} min · {money(s.price, tenant?.currency)}</span>
              </button>
            ))}
          </div>

          <h2 className="step"><span>2</span> Date &amp; time</h2>
          <label className="fld narrow-fld">Date
            <input type="date" min={dayInput(new Date())} value={date} onChange={e => { setDate(e.target.value); setTime('') }} />
          </label>
          {service && date && (
            slots.length === 0
              ? <p className="muted">No more times available on this day.</p>
              : <div className="slots">{slots.map(t => <button key={t} type="button" className={`slot ${time === t ? 'selected' : ''}`} onClick={() => setTime(t)}>{t}</button>)}</div>
          )}
          {!service && <p className="small muted">Choose an experience first to see available times.</p>}

          {!isGuest && (
            <>
              <h2 className="step"><span>3</span> Your practitioner</h2>
              <div className="choices">
                <button type="button" className={`choice ${staffId === '' ? 'selected' : ''}`} onClick={() => { setStaffId(''); setStaffTouched(true) }}><strong>No preference</strong><span className="small muted">First available</span></button>
                {staff.data?.map(m => (
                  <button key={m.id} type="button" className={`choice ${staffId === m.id ? 'selected' : ''}`} onClick={() => { setStaffId(m.id); setStaffTouched(true) }}>
                    <strong>{m.name}</strong><span className="small muted">{m.jobTitle}</span>
                    {m.id === usualId && <span className="tag">Your usual</span>}
                  </button>
                ))}
              </div>
            </>
          )}

          <h2 className="step"><span>{isGuest ? 3 : 4}</span> {isGuest ? 'Your details' : 'Details'}</h2>
          {isGuest && (
            <div className="form" style={{ marginBottom: '1rem' }}>
              <div className="row">
                <label className="fld">First name<input autoComplete="given-name" value={first} onChange={e => setFirst(e.target.value)} maxLength={100} /></label>
                <label className="fld">Last name<input autoComplete="family-name" value={last} onChange={e => setLast(e.target.value)} maxLength={100} /></label>
              </div>
              <div className="row">
                <label className="fld">Email<input type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} maxLength={256} /></label>
                <label className="fld">Phone (optional)<input type="tel" autoComplete="tel" value={phone} onChange={e => setPhone(e.target.value)} maxLength={32} /></label>
              </div>
            </div>
          )}
          <div className="row">
            <label className="fld">Guests<input type="number" min={1} max={isGuest ? 10 : 50} value={party} onChange={e => setParty(Number(e.target.value) || 1)} /></label>
          </div>
          <label className="fld">Notes (allergies, preferences)<textarea rows={3} maxLength={500} value={notes} onChange={e => setNotes(e.target.value)} /></label>

          {isGuest && (
            <div className="locked" style={{ marginTop: '2rem' }}>
              <strong>Choose your practitioner</strong>
              <span className="small muted">Members pick who looks after them, and we remember your favourites for every visit after.</span>
              <p style={{ margin: '.6rem 0 0' }}><Link to={`/login?mode=register&next=${encodeURIComponent(here)}`} className="btn ghost sm">Create an account</Link> <span className="small muted">&nbsp;or continue as a guest.</span></p>
            </div>
          )}
        </div>

        <aside className="summary">
          <p className="eyebrow">Summary</p>
          <p>{service ? <strong>{service.name}</strong> : <span className="muted">No experience chosen</span>}</p>
          <p className="small muted">{isGuest ? 'Practitioner assigned by the venue' : staffId ? staff.data?.find(m => m.id === staffId)?.name : 'No preference'}</p>
          <p className="small muted">{date && time ? dateTime(startsAt(), tenant?.timeZone) : 'Date and time not chosen'}</p>
          {service && <p className="gold">{money(service.price, tenant?.currency)}</p>}
          {error && <Notice>{error}</Notice>}
          {isGuest && <p className="small muted">Already a member? <Link to={`/login?next=${encodeURIComponent(here)}`}>Sign in</Link></p>}
          {user && !isCustomer && <Notice kind="ok">Staff book on behalf of customers from the <Link to="/portal/bookings">portal</Link>.</Notice>}
          <button className="btn" disabled={!canSubmit || busy} onClick={confirm}>{busy ? 'Reserving…' : 'Request reservation'}</button>
        </aside>
      </div>
    </main>
  )
}
