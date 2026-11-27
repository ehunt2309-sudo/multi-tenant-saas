import { useState, type FormEvent } from 'react'
import { api, dateOnly, dateTime, money, useAuth, type Customer } from '@platform/shared'
import { Empty, Loading, Modal, PageHead, Pager, useSubmit } from '../../components/ui'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useTenant } from '../../tenant'

export default function Customers() {
  const { tenant } = useTenant()
  const [q, setQ] = useState('')
  const [term, setTerm] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<Customer | null>(null)
  const [adding, setAdding] = useState(false)
  const list = useAsync(() => api.customers.list(term || undefined, page), [term, page])

  return (
    <>
      <PageHead title="Customers & rewards" sub="Look up a customer, award points, edit details"
        action={<button className="btn sm" onClick={() => setAdding(true)}>Add customer</button>} />
      <form className="toolbar" onSubmit={e => { e.preventDefault(); setTerm(q); setPage(1) }}>
        <input placeholder="Search name or email" value={q} onChange={e => setQ(e.target.value)} />
        <button className="btn ghost sm">Search</button>
      </form>
      {list.error && <Notice>{list.error}</Notice>}
      {list.loading && <Loading />}
      {list.data?.items.length === 0 && <Empty>No customers found.</Empty>}
      {list.data && list.data.items.length > 0 && (
        <table className="table clickable">
          <thead><tr><th>Name</th><th>Email</th><th>Phone</th><th>Visits</th><th>Spend</th><th>Last visit</th></tr></thead>
          <tbody>{list.data.items.map(c => (
            <tr key={c.id} onClick={() => setOpen(c)} tabIndex={0} onKeyDown={e => e.key === 'Enter' && setOpen(c)}>
              <td>{c.firstName} {c.lastName}</td><td>{c.email ?? '—'}</td><td>{c.phone ?? '—'}</td><td>{c.visitCount}</td>
              <td>{money(c.totalSpend, tenant?.currency)}</td><td>{c.lastVisitAt ? dateOnly(c.lastVisitAt, tenant?.timeZone) : '—'}</td>
            </tr>))}</tbody>
      </table>)}
      {list.data && <Pager page={page} total={list.data.total} pageSize={list.data.pageSize} onPage={setPage} />}
      <CustomerModal customer={open} onClose={() => setOpen(null)} onChanged={() => list.reload()} />
      <AddCustomer open={adding} onClose={() => setAdding(false)} onDone={() => { setAdding(false); list.reload() }} />
    </>
  )
}

function CustomerModal({ customer, onClose, onChanged }: { customer: Customer | null; onClose: () => void; onChanged: () => void }) {
  const { tenant } = useTenant()
  const { hasRole } = useAuth()
  const id = customer?.id ?? ''
  const account = useAsync(() => (id ? api.loyalty.account(id) : Promise.resolve(null)), [id])
  const history = useAsync(() => (id ? api.loyalty.history(id) : Promise.resolve(null)), [id])
  const s = useSubmit()
  const [msg, setMsg] = useState<string | null>(null)

  async function points(kind: 'award' | 'redeem', e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget); const form = e.currentTarget
    const n = Number(f.get('points')), reason = String(f.get('reason'))
    if (await s.run(() => (kind === 'award' ? api.loyalty.award(id, n, reason) : api.loyalty.redeem(id, n, reason)))) {
      setMsg(kind === 'award' ? 'Points awarded.' : 'Points redeemed.'); form.reset(); account.reload(); history.reload()
    }
  }
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (await s.run(() => api.customers.update(id, {
      firstName: String(f.get('firstName')).trim(), lastName: String(f.get('lastName')).trim(),
      phone: String(f.get('phone')).trim() || null, dateOfBirth: String(f.get('dob')) || null,
      gender: String(f.get('gender')) || null, marketingConsent: f.get('consent') === 'on',
    }))) { setMsg('Details saved.'); onChanged() }
  }
  async function erase() {
    if (!confirm('Erase this customer\'s personal data? This cannot be undone. Spend totals are kept anonymously.')) return
    if (await s.run(() => api.customers.erase(id))) { onChanged(); onClose() }
  }

  return (
    <Modal open={!!customer} title={customer ? `${customer.firstName} ${customer.lastName}` : ''} onClose={() => { setMsg(null); onClose() }} wide>
      {customer && (
        <>
          {msg && <Notice kind="ok">{msg}</Notice>}{s.error && <Notice>{s.error}</Notice>}
          <div className="grid-2">
            <div>
              <p className="eyebrow">{account.data?.tier ?? '…'} · {account.data?.pointsBalance.toLocaleString() ?? '–'} points</p>
              <form className="inline" onSubmit={e => points('award', e)}>
                <input name="points" type="number" min={1} max={10000} placeholder="Points" required /><input name="reason" placeholder="Reason" required maxLength={200} />
                <button className="btn sm">Award</button></form>
              <form className="inline" onSubmit={e => points('redeem', e)}>
                <input name="points" type="number" min={1} placeholder="Points" required /><input name="reason" placeholder="Reason" required maxLength={200} />
                <button className="btn ghost sm">Redeem</button></form>
              <h4>Recent activity</h4>
              {history.data?.items.length === 0 && <Empty>No points activity yet.</Empty>}
              <ul className="list compact">{history.data?.items.map(t => <li key={t.id}><span>{t.reason}<span className="small muted"> · {dateTime(t.createdAt, tenant?.timeZone)}</span></span><span className="gold">{t.points > 0 ? '+' : ''}{t.points}</span></li>)}</ul>
            </div>
            <form onSubmit={save} className="form" key={customer.id}>
              <div className="row"><label className="fld">First name<input name="firstName" defaultValue={customer.firstName} required /></label>
                <label className="fld">Last name<input name="lastName" defaultValue={customer.lastName} required /></label></div>
              <label className="fld">Phone<input name="phone" defaultValue={customer.phone ?? ''} /></label>
              <div className="row"><label className="fld">Date of birth<input name="dob" type="date" defaultValue={customer.dateOfBirth ?? ''} /></label>
                <label className="fld">Gender<select name="gender" defaultValue={customer.gender ?? ''}><option value="">Not stated</option><option>Female</option><option>Male</option><option>Non-binary</option></select></label></div>
              <label className="check"><input name="consent" type="checkbox" defaultChecked={customer.marketingConsent} /> Marketing consent</label>
              <div className="actions"><button className="btn sm" disabled={s.busy}>Save</button>
                {hasRole('Manager') && <button type="button" className="link danger" onClick={erase}>Erase personal data</button>}</div>
            </form>
          </div>
        </>
      )}
    </Modal>
  )
}

function AddCustomer({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const s = useSubmit()
  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    if (await s.run(() => api.customers.create({
      firstName: String(f.get('firstName')).trim(), lastName: String(f.get('lastName')).trim(),
      email: String(f.get('email')).trim() || null, phone: String(f.get('phone')).trim() || null,
      dateOfBirth: String(f.get('dob')) || null, gender: String(f.get('gender')) || null, marketingConsent: f.get('consent') === 'on',
    }))) onDone()
  }
  return (
    <Modal open={open} title="Add customer" onClose={onClose}>
      <form onSubmit={onSubmit} className="form">
        <div className="row"><label className="fld">First name<input name="firstName" required /></label><label className="fld">Last name<input name="lastName" required /></label></div>
        <div className="row"><label className="fld">Email<input name="email" type="email" /></label><label className="fld">Phone<input name="phone" /></label></div>
        <div className="row"><label className="fld">Date of birth<input name="dob" type="date" /></label>
          <label className="fld">Gender<select name="gender" defaultValue=""><option value="">Not stated</option><option>Female</option><option>Male</option><option>Non-binary</option></select></label></div>
        <label className="check"><input name="consent" type="checkbox" /> Has given marketing consent</label>
        {s.error && <Notice>{s.error}</Notice>}
        <button className="btn" disabled={s.busy}>Add customer</button>
      </form>
    </Modal>
  )
}
