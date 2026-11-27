import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '@platform/shared'
import { useTenant } from '../tenant'

const NAV: { to: string; label: string; manager?: boolean; end?: boolean }[] = [
  { to: '/portal', label: 'Overview', end: true },
  { to: '/portal/bookings', label: 'Bookings' },
  { to: '/portal/customers', label: 'Customers & rewards' },
  { to: '/portal/schedule', label: 'Schedule' },
  { to: '/portal/hours', label: 'Hours & pay' },
  { to: '/portal/products', label: 'Boutique stock' },
  { to: '/portal/staff', label: 'Staff', manager: true },
  { to: '/portal/services', label: 'Experiences', manager: true },
  { to: '/portal/offers', label: 'Offers', manager: true },
  { to: '/portal/content', label: 'Website & settings', manager: true },
]

export default function PortalLayout() {
  const { tenant } = useTenant()
  const { user, logout, hasRole } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)

  return (
    <div className="portal">
      <aside className={`side ${open ? 'open' : ''}`}>
        <div className="side-top">
          <p className="wordmark">{tenant?.name}</p>
          <button className="burger portal-burger" aria-label="Menu" onClick={() => setOpen(o => !o)}><span /><span /></button>
        </div>
        <nav onClick={() => setOpen(false)}>
          {NAV.filter(n => !n.manager || hasRole('Manager')).map(n => (
            <NavLink key={n.to} to={n.to} end={n.end}>{n.label}</NavLink>
          ))}
          <NavLink to="/" end className="out">← View website</NavLink>
        </nav>
        <div className="side-user">
          <p className="small"><strong>{user?.firstName} {user?.lastName}</strong></p>
          <p className="small muted">{user?.role}</p>
          <button className="link" onClick={async () => { await logout(); navigate('/') }}>Sign out</button>
        </div>
      </aside>
      <section className="portal-main"><Outlet /></section>
    </div>
  )
}
