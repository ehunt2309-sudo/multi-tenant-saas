import { useState, type FormEvent } from 'react'
import { api, dateOnly, type DiscountType, type Promotion } from '@platform/shared'
import { Badge, Loading, Modal, PageHead, useSubmit } from '../../components/ui'
import { Notice, errorMessage } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { fromLocalInput, toLocalInput } from '../../lib/time'
import { useTenant } from '../../tenant'

export default function OffersAdmin() {
  const { tenant } = useTenant()
  const list = useAsync(() => api.promotions.list())
  const [edit, setEdit] = useState<Promotion | 'new' | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const s = useSubmit()

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const body = { title: String(f.get('title')).trim(), description: String(f.get('description')).trim() || null,
      startsAt: fromLocalInput(String(f.get('start'))), endsAt: fromLocalInput(String(f.get('end'))),
      discountType: String(f.get('type')) as DiscountType, discountValue: Number(f.get('value')) || 0, isActive: f.get('active') === 'on' }
    if (await s.run(() => edit === 'new' ? api.promotions.create(body) : api.promotions.update((edit as Promotion).id, body))) { setEdit(null); list.reload() }
  }
  async function remove(id: string) {
    if (!confirm('Delete this offer?')) return
    try { await api.promotions.remove(id); list.reload() } catch (x) { setErr(errorMessage(x)) }
  }
  const e = edit && edit !== 'new' ? edit : null
  const now = Date.now()
  const live = (p: Promotion) => p.isActive && new Date(p.startsAt).getTime() <= now && new Date(p.endsAt).getTime() > now

  return (
    <>
      <PageHead title="Offers" sub="Promotions shown on the website" action={<button className="btn sm" onClick={() => { s.clear(); setEdit('new') }}>Add offer</button>} />
      {(err || list.error) && <Notice>{err ?? list.error}</Notice>}{list.loading && <Loading />}
      <table className="table">
        <thead><tr><th>Offer</th><th>Discount</th><th>Runs</th><th>Status</th><th /></tr></thead>
        <tbody>{list.data?.items.map(p => (
          <tr key={p.id}>
            <td><strong>{p.title}</strong><div className="small muted">{p.description}</div></td>
            <td>{p.discountType === 'Percentage' ? `${p.discountValue}%` : p.discountType === 'FixedAmount' ? `${tenant?.currency} ${p.discountValue}` : 'Gift / perk'}</td>
            <td className="small">{dateOnly(p.startsAt, tenant?.timeZone)} – {dateOnly(p.endsAt, tenant?.timeZone)}</td>
            <td><Badge tone={live(p) ? 'ok' : 'neutral'}>{live(p) ? 'Live' : p.isActive ? 'Scheduled / ended' : 'Off'}</Badge></td>
            <td className="actions"><button className="link" onClick={() => { s.clear(); setEdit(p) }}>Edit</button><button className="link danger" onClick={() => remove(p.id)}>Delete</button></td>
          </tr>))}</tbody>
      </table>
      <Modal open={!!edit} title={edit === 'new' ? 'Add offer' : 'Edit offer'} onClose={() => setEdit(null)}>
        <form onSubmit={onSubmit} className="form" key={e?.id ?? 'new'}>
          <label className="fld">Title<input name="title" defaultValue={e?.title} required maxLength={150} /></label>
          <label className="fld">Description<textarea name="description" rows={2} defaultValue={e?.description ?? ''} /></label>
          <div className="row"><label className="fld">Starts<input name="start" type="datetime-local" required defaultValue={toLocalInput(e?.startsAt ?? new Date())} /></label>
            <label className="fld">Ends<input name="end" type="datetime-local" required defaultValue={toLocalInput(e?.endsAt ?? new Date(Date.now() + 30 * 864e5))} /></label></div>
          <div className="row"><label className="fld">Type<select name="type" defaultValue={e?.discountType ?? 'Percentage'}><option value="Percentage">Percentage off</option><option value="FixedAmount">Amount off</option><option value="None">Gift / perk (no discount)</option></select></label>
            <label className="fld">Value<input name="value" type="number" min={0} step="0.01" defaultValue={e?.discountValue ?? 0} /></label></div>
          <label className="check"><input name="active" type="checkbox" defaultChecked={e?.isActive ?? true} /> Active</label>
          {s.error && <Notice>{s.error}</Notice>}
          <button className="btn" disabled={s.busy}>Save</button>
        </form>
      </Modal>
    </>
  )
}
