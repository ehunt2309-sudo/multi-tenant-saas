import { useState, type FormEvent } from 'react'
import { api, money, useAuth, type Product } from '@platform/shared'
import { Badge, Loading, Modal, PageHead, useSubmit } from '../../components/ui'
import { Notice, errorMessage } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useTenant } from '../../tenant'

export default function Products() {
  const { tenant } = useTenant()
  const { hasRole } = useAuth()
  const isManager = hasRole('Manager')
  const list = useAsync(() => api.products.all())
  const [edit, setEdit] = useState<Product | 'new' | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const s = useSubmit()

  async function adjust(id: string, delta: number) {
    try { await api.products.adjustStock(id, delta); setErr(null); list.reload() } catch (e) { setErr(errorMessage(e)) }
  }
  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const body = { name: String(f.get('name')).trim(), description: String(f.get('description')).trim() || null, price: Number(f.get('price')),
      stockQuantity: Number(f.get('stock')), imageUrl: String(f.get('image')).trim() || null, isActive: f.get('active') === 'on' }
    if (await s.run(() => edit === 'new' ? api.products.create(body) : api.products.update((edit as Product).id, body))) { setEdit(null); list.reload() }
  }
  const e = edit && edit !== 'new' ? edit : null

  return (
    <>
      <PageHead title="Boutique stock" sub="Products sold in the venue" action={isManager && <button className="btn sm" onClick={() => { s.clear(); setEdit('new') }}>Add product</button>} />
      {(err || list.error) && <Notice>{err ?? list.error}</Notice>}{list.loading && <Loading />}
      <table className="table">
        <thead><tr><th>Product</th><th>Price</th><th>In stock</th><th>Adjust</th><th>Status</th><th /></tr></thead>
        <tbody>{list.data?.items.map(p => (
          <tr key={p.id}>
            <td><strong>{p.name}</strong><div className="small muted">{p.description}</div></td><td>{money(p.price, tenant?.currency)}</td>
            <td>{p.stockQuantity < 6 ? <Badge tone={p.stockQuantity === 0 ? 'bad' : 'warn'}>{p.stockQuantity}</Badge> : p.stockQuantity}</td>
            <td className="actions"><button className="link" onClick={() => adjust(p.id, -1)}>−1</button><button className="link" onClick={() => adjust(p.id, 1)}>+1</button><button className="link" onClick={() => adjust(p.id, 10)}>+10</button></td>
            <td><Badge tone={p.isActive ? 'ok' : 'bad'}>{p.isActive ? 'On sale' : 'Hidden'}</Badge></td>
            <td>{isManager && <button className="link" onClick={() => { s.clear(); setEdit(p) }}>Edit</button>}</td>
          </tr>))}</tbody>
      </table>
      <Modal open={!!edit} title={edit === 'new' ? 'Add product' : 'Edit product'} onClose={() => setEdit(null)}>
        <form onSubmit={onSubmit} className="form" key={e?.id ?? 'new'}>
          <label className="fld">Name<input name="name" defaultValue={e?.name} required maxLength={150} /></label>
          <label className="fld">Description<textarea name="description" rows={2} defaultValue={e?.description ?? ''} /></label>
          <div className="row"><label className="fld">Price<input name="price" type="number" min={0} step="0.01" defaultValue={e?.price ?? 0} required /></label>
            <label className="fld">Stock<input name="stock" type="number" min={0} defaultValue={e?.stockQuantity ?? 0} required /></label></div>
          <label className="fld">Image URL (https, optional)<input name="image" type="url" defaultValue={e?.imageUrl ?? ''} /></label>
          <label className="check"><input name="active" type="checkbox" defaultChecked={e?.isActive ?? true} /> On sale</label>
          {s.error && <Notice>{s.error}</Notice>}
          <button className="btn" disabled={s.busy}>Save</button>
        </form>
      </Modal>
    </>
  )
}
