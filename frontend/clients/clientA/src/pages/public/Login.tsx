import { useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '@platform/shared'
import { Art } from '../../components/ui'
import { Notice, errorMessage } from '../../components/Status'
import { useTenant } from '../../tenant'

export default function Login() {
  const { tenant } = useTenant()
  const { login, register } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [mode, setMode] = useState<'login' | 'register'>(params.get('mode') === 'register' ? 'register' : 'login')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const email = String(f.get('email')).trim()
    const password = String(f.get('password'))
    setBusy(true); setError(null)
    try {
      const user = mode === 'login'
        ? await login(email, password)
        : await register({
            email, password,
            firstName: String(f.get('firstName')).trim(),
            lastName: String(f.get('lastName')).trim(),
            phone: String(f.get('phone') ?? '').trim() || null,
            dateOfBirth: String(f.get('dob') ?? '') || null,
            gender: String(f.get('gender') ?? '') || null,
            marketingConsent: f.get('consent') === 'on',
          })
      const next = params.get('next')
      navigate(next && next.startsWith('/') ? next : user.role === 'Customer' ? '/account' : '/portal')
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login">
      <aside className="login-art"><Art seed={tenant?.name ?? 'spa'} label="" tall /><blockquote><p>Stillness is a luxury<br />worth returning to.</p></blockquote></aside>
      <section className="login-form">
        <p className="eyebrow">{tenant?.name}</p>
        <h1 className="display sm">{mode === 'login' ? 'Welcome back' : 'Join the Inner Circle'}</h1>
        <form onSubmit={onSubmit} className="form">
          {mode === 'register' && (
            <div className="row">
              <label>First name<input name="firstName" required maxLength={100} autoComplete="given-name" /></label>
              <label>Last name<input name="lastName" required maxLength={100} autoComplete="family-name" /></label>
            </div>
          )}
          <label>Email<input name="email" type="email" required autoComplete="email" /></label>
          <label>Password
            <input name="password" type="password" required minLength={mode === 'register' ? 12 : 1}
                   autoComplete={mode === 'login' ? 'current-password' : 'new-password'} />
          </label>
          {mode === 'register' && (
            <>
              <p className="small muted">At least 12 characters, and not containing your email name.</p>
              <div className="row">
                <label>Phone<input name="phone" type="tel" autoComplete="tel" /></label>
                <label>Date of birth<input name="dob" type="date" autoComplete="bday" /></label>
              </div>
              <label>Gender (optional)
                <select name="gender" defaultValue=""><option value="">Prefer not to say</option><option>Female</option><option>Male</option><option>Non-binary</option></select>
              </label>
              <label className="check"><input name="consent" type="checkbox" /> Send me news and offers (change this any time)</label>
            </>
          )}
          {error && <Notice>{error}</Notice>}
          <button className="btn" disabled={busy}>{busy ? 'One moment…' : mode === 'login' ? 'Sign in' : 'Create account'}</button>
        </form>
        <p className="small muted">
          <button className="link" onClick={() => { setMode(mode === 'login' ? 'register' : 'login'); setError(null) }}>
            {mode === 'login' ? 'New here? Create an account' : 'Already a member? Sign in'}
          </button>
        </p>
      </section>
    </main>
  )
}
