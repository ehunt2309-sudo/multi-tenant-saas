import { useState, type FormEvent } from 'react'
import { api, money, type ServiceOffering } from '@platform/shared'
import { Badge, Loading, Modal, PageHead, useSubmit } from '../../components/ui'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useTenant } from '../../tenant'

export default function Services() {
  const { tenant } = useTenant()
  const list = useAsync(() => api.services.all())
  const [edit, setEdit] = useState<ServiceOffering | 'new' | null>(null)
  const s = useSubmit()

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const body = { name: String(f.get('name')).trim(), description: String(f.get('description')).trim() || null,
      durationMinutes: Number(f.get('duration')), price: Number(f.get('price')), isActive: f.get('active') === 'on' }
    if (await s.run(() => edit === 'new' ? api.services.create(body) : api.services.update((edit as ServiceOffering).id, body))) { setEdit(null); list.reload() }
  }
  const e = edit && edit !== 'new' ? edit : null

  return (
    <>
      <PageHead title="Experiences" sub="What customers can book" action={<button className="btn sm" onClick={() => { s.clear(); setEdit('new') }}>Add experience</button>} />
      {list.error && <Notice>{list.error}</Notice>}{list.loading && <Loading />}
      <table className="table clickable">
        <thead><tr><th>Name</th><th>Duration</th><th>Price</th><th>Status</th></tr></thead>
        <tbody>{list.data?.map(x => (
          <tr key={x.id} onClick={() => { s.clear(); setEdit(x) }} tabIndex={0} onKeyDown={ev => ev.key === 'Enter' && setEdit(x)}>
            <td><strong>{x.name}</strong><div className="small muted">{x.description}</div></td><td>{x.durationMinutes} min</td><td>{money(x.price, tenant?.currency)}</td>
            <td><Badge tone={x.isActive ? 'ok' : 'bad'}>{x.isActive ? 'Bookable' : 'Hidden'}</Badge></td></tr>))}</tbody>
      </table>
      <Modal open={!!edit} title={edit === 'new' ? 'Add experience' : 'Edit experience'} onClose={() => setEdit(null)}>
        <form onSubmit={onSubmit} className="form" key={e?.id ?? 'new'}>
          <label className="fld">Name<input name="name" defaultValue={e?.name} required maxLength={150} /></label>
          <label className="fld">Description<textarea name="description" rows={3} defaultValue={e?.description ?? ''} maxLength={2000} /></label>
          <div className="row"><label className="fld">Duration (minutes)<input name="duration" type="number" min={5} max={1440} defaultValue={e?.durationMinutes ?? 60} required /></label>
            <label className="fld">Price<input name="price" type="number" min={0} step="0.01" defaultValue={e?.price ?? 0} required /></label></div>
          <label className="check"><input name="active" type="checkbox" defaultChecked={e?.isActive ?? true} /> Visible and bookable</label>
          {s.error && <Notice>{s.error}</Notice>}
          <button className="btn" disabled={s.busy}>Save</button>
        </form>
      </Modal>
    </>
  )
}
