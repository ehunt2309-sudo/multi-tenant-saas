import { useState, type FormEvent } from 'react'
import { api, money, type StaffMember } from '@platform/shared'
import { Badge, Loading, Modal, PageHead, useSubmit } from '../../components/ui'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useTenant } from '../../tenant'

export default function StaffAdmin() {
  const { tenant } = useTenant()
  const staff = useAsync(() => api.staff.list())
  const [edit, setEdit] = useState<StaffMember | 'new' | null>(null)
  const s = useSubmit()

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const base = { firstName: String(f.get('firstName')).trim(), lastName: String(f.get('lastName')).trim(), jobTitle: String(f.get('jobTitle')).trim(), hourlyRate: Number(f.get('rate')) }
    const ok = await s.run(() => edit === 'new'
      ? api.staff.create({ ...base, email: String(f.get('email')).trim(), password: String(f.get('password')), role: String(f.get('role')) as 'Staff' | 'Manager' })
      : api.staff.update((edit as StaffMember).id, { ...base, isActive: f.get('active') === 'on' }))
    if (ok) { setEdit(null); staff.reload() }
  }

  const e = edit && edit !== 'new' ? edit : null
  return (
    <>
      <PageHead title="Staff" sub="Team members, pay rates and access" action={<button className="btn sm" onClick={() => { s.clear(); setEdit('new') }}>Add staff</button>} />
      {staff.error && <Notice>{staff.error}</Notice>}
      {staff.loading && <Loading />}
      <table className="table clickable">
        <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Title</th><th>Rate / hour</th><th>Status</th></tr></thead>
        <tbody>{staff.data?.map(m => (
          <tr key={m.id} onClick={() => { s.clear(); setEdit(m) }} tabIndex={0} onKeyDown={ev => ev.key === 'Enter' && setEdit(m)}>
            <td>{m.firstName} {m.lastName}</td><td>{m.email}</td><td>{m.role}</td><td>{m.jobTitle}</td>
            <td>{m.hourlyRate != null ? money(m.hourlyRate, tenant?.currency) : '—'}</td><td><Badge tone={m.isActive ? 'ok' : 'bad'}>{m.isActive ? 'Active' : 'Inactive'}</Badge></td>
          </tr>))}</tbody>
      </table>
      <Modal open={!!edit} title={edit === 'new' ? 'Add staff member' : 'Edit staff member'} onClose={() => setEdit(null)}>
        <form onSubmit={onSubmit} className="form" key={e?.id ?? 'new'}>
          <div className="row"><label className="fld">First name<input name="firstName" defaultValue={e?.firstName} required /></label>
            <label className="fld">Last name<input name="lastName" defaultValue={e?.lastName} required /></label></div>
          {edit === 'new' && <>
            <label className="fld">Email (their login)<input name="email" type="email" required /></label>
            <label className="fld">Temporary password (12+ characters)<input name="password" type="password" required minLength={12} autoComplete="new-password" /></label>
            <label className="fld">Role<select name="role" defaultValue="Staff"><option>Staff</option><option>Manager</option></select></label></>}
          <div className="row"><label className="fld">Job title<input name="jobTitle" defaultValue={e?.jobTitle} required /></label>
            <label className="fld">Hourly rate<input name="rate" type="number" min={0} step="0.01" defaultValue={e?.hourlyRate ?? 0} required /></label></div>
          {e && <label className="check"><input name="active" type="checkbox" defaultChecked={e.isActive} /> Active (untick to remove access)</label>}
          {s.error && <Notice>{s.error}</Notice>}
          <button className="btn" disabled={s.busy}>{edit === 'new' ? 'Add staff member' : 'Save'}</button>
        </form>
      </Modal>
    </>
  )
}
