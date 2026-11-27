import { lazy, Suspense, useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import { gsap } from 'gsap'
import { api, dateOnly, money } from '@platform/shared'
import { Art } from '../../components/ui'
import { Words } from '../../components/Words'
import { useAsync } from '../../components/useAsync'
import { prefersReducedMotion, useReveal } from '../../lib/motion'
import { useTenant } from '../../tenant'

const HeroScene = lazy(() => import('../../components/HeroScene'))

const TIERS = [
  { name: 'Bronze', from: '0', perk: 'Earn points on every visit' },
  { name: 'Silver', from: '1 000', perk: 'Priority booking and a birthday gift' },
  { name: 'Gold', from: '5 000', perk: 'Complimentary upgrades and late openings' },
  { name: 'Platinum', from: '15 000', perk: 'Private suites and personal concierge' },
]

export default function Home() {
  const { tenant, content } = useTenant()
  const root = useRef<HTMLElement>(null)
  const services = useAsync(() => api.services.list())
  const promos = useAsync(() => api.promotions.active())
  const staff = useAsync(() => api.staff.public())
  const settled = !services.loading && !promos.loading && !staff.loading

  // Hero intro: words rise out of a mask, then the supporting text and buttons follow.
  useEffect(() => {
    if (prefersReducedMotion()) return
    const ctx = gsap.context(() => {
      gsap.timeline({ defaults: { ease: 'power4.out' } })
        .from('.hero .eyebrow', { opacity: 0, y: 16, duration: 0.9, delay: 0.2 })
        .from('.hero .word-in', { yPercent: 115, duration: 1.4, stagger: 0.14 }, '-=0.5')
        .from('.hero .lead', { opacity: 0, y: 24, duration: 1 }, '-=0.8')
        .from('.hero .hero-actions > *', { opacity: 0, y: 20, duration: 0.9, stagger: 0.12 }, '-=0.7')
        .from('.scroll-cue', { opacity: 0, duration: 1 }, '-=0.4')
    }, root)
    return () => ctx.revert()
  }, [])

  useReveal(root, [settled])

  const featured = (services.data ?? []).slice(0, 5)
  const ribbon = (services.data ?? []).map(s => s.name)
  const offers = (promos.data ?? []).slice(0, 6)

  return (
    <main ref={root}>
      <section className="hero" data-hero>
        {!prefersReducedMotion() && <Suspense fallback={null}><HeroScene /></Suspense>}
        <div className="hero-inner">
          <p className="eyebrow">{tenant?.industry === 'Spa' ? 'Spa & Wellness' : tenant?.industry}</p>
          <h1 className="display"><Words text={tenant?.name ?? ''} /></h1>
          <p className="lead">{content.tagline}</p>
          <div className="hero-actions">
            <Link to="/reserve" className="btn">Reserve your experience</Link>
            <Link to="/experiences" className="btn ghost">Discover</Link>
          </div>
        </div>
        <div className="scroll-cue" aria-hidden="true"><span /></div>
      </section>

      <section className="section statement">
        <div className="wrap narrow center">
          <div className="rule" data-line />
          <p className="statement-text" data-scrub><Words text={content.about} /></p>
          <div className="rule" data-line />
        </div>
      </section>

      <div className="pin-host">
        <section className="seq" data-sequence>
          <div className="seq-item"><p className="eyebrow">First</p><h2 className="display">Arrive.</h2><p className="lead">Leave the noise at the door.</p></div>
          <div className="seq-item"><p className="eyebrow">Then</p><h2 className="display">Unwind.</h2><p className="lead">Hours soften. Nothing is hurried.</p></div>
          <div className="seq-item"><p className="eyebrow">Always</p><h2 className="display">Return.</h2><p className="lead">You will want to come back.</p></div>
          <div className="seq-bar" aria-hidden="true" />
        </section>
      </div>

      {settled && ribbon.length > 0 && (
        <div className="marquee" aria-hidden="true" data-marquee>
          <div className="marquee-inner">
            <div className="marquee-track">
              {[...ribbon, ...ribbon, ...ribbon, ...ribbon].map((n, i) => <span key={i}>{n}<i>✦</i></span>)}
            </div>
            <div className="marquee-track outline">
              {[...ribbon, ...ribbon, ...ribbon, ...ribbon].reverse().map((n, i) => <span key={i}>{n}<i>✦</i></span>)}
            </div>
          </div>
        </div>
      )}

      {settled && featured.length > 0 && (
        <div className="pin-host">
        <section className="hs" data-hscroll>
          <div className="hs-track">
            <div className="hs-intro">
              <p className="eyebrow">Signature experiences</p>
              <h2 className="display sm" data-words><Words text="Crafted for stillness" /></h2>
              <p className="muted">Each ritual is composed slowly, one sense at a time.</p>
              <p className="hs-hint small">Keep scrolling <span aria-hidden="true">→</span></p>
            </div>
            {featured.map((s, i) => (
              <Link key={s.id} to={`/reserve?service=${s.id}`} className="hs-card">
                <div className="plate"><Art seed={s.name} label={String(i + 1).padStart(2, '0')} tall /></div>
                <h3>{s.name}</h3>
                <p className="muted small">{s.description}</p>
                <p className="small gold">{s.durationMinutes} minutes · {money(s.price, tenant?.currency)}</p>
              </Link>
            ))}
            <div className="hs-end">
              <h3>There is more to discover.</h3>
              <Link to="/experiences" className="btn ghost">View all experiences</Link>
            </div>
          </div>
        </section>
        </div>
      )}

      <div className="pin-host">
      <section className="expand" data-expand>
        <div className="expand-frame"><Art seed={tenant?.name ?? 'sanctuary'} /></div>
        <div className="expand-copy">
          <p className="eyebrow">The sanctuary</p>
          <h2 className="display sm">Every detail, considered.</h2>
          <Link to="/reserve" className="btn">Reserve your experience</Link>
        </div>
      </section>
      </div>

      {settled && offers.length > 0 && (
        <section className="section dim">
          <div className="wrap">
            <p className="eyebrow" data-slide="left">Current offers</p>
            <h2 className="display sm" data-words><Words text="Reserved for this season" /></h2>
            <div className="grid-3" data-stagger>
              {offers.map(p => (
                <article key={p.id} className="offer-card">
                  <p className="offer-figure">
                    {p.discountType === 'Percentage' ? <>{p.discountValue}<small>%</small></>
                      : p.discountType === 'FixedAmount' ? money(p.discountValue, tenant?.currency)
                      : <span className="offer-gift">Gift</span>}
                  </p>
                  <h3>{p.title}</h3>
                  <p className="muted">{p.description}</p>
                  <p className="small muted">Until {dateOnly(p.endsAt, tenant?.timeZone)}</p>
                </article>
              ))}
            </div>
          </div>
        </section>
      )}

      {settled && (staff.data?.length ?? 0) > 0 && (
        <section className="section">
          <div className="wrap">
            <p className="eyebrow" data-slide="left">Our practitioners</p>
            <h2 className="display sm" data-words><Words text="In skilled hands" /></h2>
            <div className="team" data-stagger>
              {staff.data!.map(m => (
                <div key={m.id} className="team-card">
                  <div className="monogram">{m.name.split(' ').map(p => p[0]).slice(0, 2).join('')}</div>
                  <h3>{m.name}</h3>
                  <p className="small muted">{m.jobTitle}</p>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      <section className="section dim">
        <div className="wrap">
          <p className="eyebrow" data-slide="left">The Inner Circle</p>
          <h2 className="display sm" data-words><Words text="Every visit, rewarded" /></h2>
          <div className="grid-4" data-stagger>
            {TIERS.map(t => (
              <div key={t.name} className="tier-card">
                <h3>{t.name}</h3>
                <p className="small gold">{t.from} points</p>
                <p className="small muted">{t.perk}</p>
              </div>
            ))}
          </div>
          <p className="center" data-zoom><Link to="/login" className="btn">Join the Inner Circle</Link></p>
        </div>
      </section>

      <section className="section">
        <div className="wrap visit">
          <div data-slide="left">
            <p className="eyebrow">Visit us</p>
            <h2 className="display sm">{content.address || 'Find us soon'}</h2>
            {content.phone && <p className="muted">{content.phone}</p>}
            {content.email && <p className="muted">{content.email}</p>}
            <Link to="/reserve" className="btn">Reserve a time</Link>
          </div>
          <div data-slide="right">
            <p className="eyebrow">Opening hours</p>
            {(content.hours.length ? content.hours : ['Daily by reservation']).map(h => <p key={h} className="muted">{h}</p>)}
          </div>
        </div>
      </section>
    </main>
  )
}
