import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth, type Role } from '@platform/shared'
import type { ReactElement } from 'react'
import PublicLayout from './components/PublicLayout'
import PortalLayout from './components/PortalLayout'
import Home from './pages/public/Home'
import Experiences from './pages/public/Experiences'
import Offers from './pages/public/Offers'
import Shop from './pages/public/Shop'
import Reserve from './pages/public/Reserve'
import Login from './pages/public/Login'
import Account from './pages/public/Account'
import Overview from './pages/portal/Overview'
import Bookings from './pages/portal/Bookings'
import Customers from './pages/portal/Customers'
import Schedule from './pages/portal/Schedule'
import Hours from './pages/portal/Hours'
import Products from './pages/portal/Products'
import StaffAdmin from './pages/portal/StaffAdmin'
import Services from './pages/portal/Services'
import OffersAdmin from './pages/portal/OffersAdmin'
import Content from './pages/portal/Content'
import { useTenant } from './tenant'

function RequireRole({ roles, children }: { roles: Role[]; children: ReactElement }) {
  const { user, loading, hasRole } = useAuth()
  if (loading) return <p className="muted center pad">Loading…</p>
  if (!user) return <Navigate to="/login" replace />
  if (!hasRole(...roles)) return <Navigate to={user.role === 'Customer' ? '/account' : '/portal'} replace />
  return children
}

export default function App() {
  const { tenant, error } = useTenant()

  if (error) {
    return (
      <main className="boot"><h1 className="display sm">We'll be right back</h1><p className="muted">{error}</p></main>
    )
  }
  if (!tenant) return <main className="boot"><p className="muted">Loading…</p></main>

  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route index element={<Home />} />
        <Route path="experiences" element={<Experiences />} />
        <Route path="offers" element={<Offers />} />
        <Route path="shop" element={<Shop />} />
        <Route path="reserve" element={<Reserve />} />
        <Route path="book" element={<Navigate to="/reserve" replace />} />
        <Route path="login" element={<Login />} />
        <Route path="account" element={<RequireRole roles={['Customer']}><Account /></RequireRole>} />
      </Route>

      <Route path="portal" element={<RequireRole roles={['Staff', 'Manager']}><PortalLayout /></RequireRole>}>
        <Route index element={<Overview />} />
        <Route path="bookings" element={<Bookings />} />
        <Route path="customers" element={<Customers />} />
        <Route path="schedule" element={<Schedule />} />
        <Route path="hours" element={<Hours />} />
        <Route path="products" element={<Products />} />
        <Route path="staff" element={<RequireRole roles={['Manager']}><StaffAdmin /></RequireRole>} />
        <Route path="services" element={<RequireRole roles={['Manager']}><Services /></RequireRole>} />
        <Route path="offers" element={<RequireRole roles={['Manager']}><OffersAdmin /></RequireRole>} />
        <Route path="content" element={<RequireRole roles={['Manager']}><Content /></RequireRole>} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
