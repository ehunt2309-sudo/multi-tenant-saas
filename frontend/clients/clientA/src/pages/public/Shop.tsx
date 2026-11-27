import { useRef } from 'react'
import { api, money } from '@platform/shared'
import { Art, Empty, Loading } from '../../components/ui'
import { Words } from '../../components/Words'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useReveal } from '../../lib/motion'
import { useTenant } from '../../tenant'

export default function Shop() {
  const { tenant } = useTenant()
  const root = useRef<HTMLElement>(null)
  const products = useAsync(() => api.products.list())
  useReveal(root, [products.loading])

  return (
    <main ref={root} className="page-top">
      <div className="wrap">
        <p className="eyebrow" data-slide="left">Boutique</p>
        <h1 className="display sm" data-words><Words text="Take the experience home" /></h1>
        <p className="muted" data-rise>Available in our boutique. Ask your practitioner, or collect on your next visit.</p>
        {products.loading && <Loading />}
        {products.error && <Notice>{products.error}</Notice>}
        {products.data?.items.length === 0 && <Empty>The boutique is being restocked.</Empty>}
        <div className="grid-4" data-stagger>
          {products.data?.items.map(p => (
            <article key={p.id} className="product-card">
              {p.imageUrl ? <img src={p.imageUrl} alt={p.name} loading="lazy" /> : <Art seed={p.name} />}
              <h3>{p.name}</h3>
              <p className="small muted">{p.description}</p>
              <p className="gold">{money(p.price, tenant?.currency)}</p>
              <p className="small muted">{p.stockQuantity > 0 ? (p.stockQuantity < 6 ? `Only ${p.stockQuantity} left` : 'In stock') : 'Out of stock'}</p>
            </article>
          ))}
        </div>
      </div>
    </main>
  )
}
