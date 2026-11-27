import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { gsap } from 'gsap'
import { useAuth } from '@platform/shared'
import { useTenant } from '../tenant'
import { prefersReducedMotion, useMagnetic, useReveal, useSmoothScroll } from '../lib/motion'
import { Words } from './Words'

export default function PublicLayout() {
  const { tenant, content } = useTenant()
  const { user, logout, hasRole } = useAuth()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [solid, setSolid] = useState(false)
  const [open, setOpen] = useState(false)
  const [hidden, setHidden] = useState(false)
  const foot = useRef<HTMLElement>(null)
  const page = useRef<HTMLDivElement>(null)

  useSmoothScroll()
  useMagnetic()
  useReveal(foot, [pathname])

  useEffect(() => {
    let last = window.scrollY
    const on = () => {
      const y = window.scrollY
      setSolid(y > 40)
      if (Math.abs(y - last) > 6) { setHidden(y > last && y > 240); last = y }
    }
    on()
    window.addEventListener('scroll', on, { passive: true })
    return () => window.removeEventListener('scroll', on)
  }, [])

  // Route change: back to top, close the mobile menu, soft fade-in.
  useEffect(() => {
    window.scrollTo(0, 0)
    setOpen(false)
    if (page.current && !prefersReducedMotion())
      gsap.fromTo(page.current, { opacity: 0, y: 24 }, { opacity: 1, y: 0, duration: 0.9, ease: 'power3.out', clearProps: 'transform,opacity' })
  }, [pathname])

  const staffSide = hasRole('Staff', 'Manager')

  return (
    <div className="site">
      <header className={`site-nav ${solid || open ? 'solid' : ''} ${hidden && !open ? 'hide' : ''}`}>
        <NavLink to="/" className="wordmark" end>
          {tenant?.logoUrl ? <img src={tenant.logoUrl} alt={tenant.name} height={34} /> : tenant?.name}
        </NavLink>
        <button className="burger" aria-label="Menu" aria-expanded={open} onClick={() => setOpen(o => !o)}><span /><span /></button>
        <nav className={open ? 'open' : ''}>
          <NavLink to="/" end>Home</NavLink>
          <NavLink to="/experiences">Experiences</NavLink>
          <NavLink to="/offers">Offers</NavLink>
          <NavLink to="/shop">Boutique</NavLink>
          {user
            ? <>
                <NavLink to={staffSide ? '/portal' : '/account'}>{staffSide ? 'Portal' : 'My account'}</NavLink>
                <button className="link" onClick={async () => { await logout(); navigate('/') }}>Sign out</button>
              </>
            : <NavLink to="/login">Sign in</NavLink>}
          {!staffSide && <Link to="/reserve" className="btn sm nav-cta">Reserve</Link>}
        </nav>
      </header>

      <div ref={page}><Outlet /></div>

      <footer className="site-footer" ref={foot}>
        <p className="footer-mark" aria-hidden="true" data-words><Words text={tenant?.name ?? ''} /></p>
        <div className="footer-grid" data-stagger>
          <div>
            <p className="wordmark">{tenant?.name}</p>
            <p className="muted small">{content.tagline}</p>
          </div>
          <div>
            <p className="eyebrow">Visit</p>
            <p className="small muted">{content.address || 'Address coming soon'}</p>
            {content.phone && <p className="small muted">{content.phone}</p>}
            {content.email && <p className="small muted">{content.email}</p>}
          </div>
          <div>
            <p className="eyebrow">Hours</p>
            {(content.hours.length ? content.hours : ['Daily by reservation']).map(h => <p key={h} className="small muted">{h}</p>)}
          </div>
          <div>
            <p className="eyebrow">Explore</p>
            <p className="small"><Link to="/experiences">Experiences</Link></p>
            <p className="small"><Link to="/offers">Offers</Link></p>
            <p className="small"><Link to="/reserve">Reserve</Link></p>
          </div>
        </div>
        <p className="small muted center" data-rise>© {new Date().getFullYear()} {tenant?.name}. All rights reserved.</p>
      </footer>
    </div>
  )
}
