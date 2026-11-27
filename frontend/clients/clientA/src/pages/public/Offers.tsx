import { useRef } from 'react'
import { Link } from 'react-router-dom'
import { api, dateOnly, money } from '@platform/shared'
import { Empty, Loading } from '../../components/ui'
import { Words } from '../../components/Words'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useReveal } from '../../lib/motion'
import { useTenant } from '../../tenant'

export default function Offers() {
  const { tenant } = useTenant()
  const root = useRef<HTMLElement>(null)
  const promos = useAsync(() => api.promotions.active())
  useReveal(root, [promos.loading])

  return (
    <main ref={root} className="page-top">
      <div className="wrap">
        <p className="eyebrow" data-slide="left">Offers</p>
        <h1 className="display sm" data-words><Words text="Current offers" /></h1>
        {promos.loading && <Loading />}
        {promos.error && <Notice>{promos.error}</Notice>}
        {promos.data?.length === 0 && <Empty>No offers at the moment. Please check back soon.</Empty>}
        <div className="grid-3" data-stagger>
          {promos.data?.map(p => (
            <article key={p.id} className="offer-card">
              <p className="offer-figure">
                {p.discountType === 'Percentage' ? <>{p.discountValue}<small>%</small></>
                  : p.discountType === 'FixedAmount' ? money(p.discountValue, tenant?.currency)
                  : <span className="offer-gift">Gift</span>}
              </p>
              <h3>{p.title}</h3>
              <p className="muted">{p.description}</p>
              <p className="small muted">{dateOnly(p.startsAt, tenant?.timeZone)} – {dateOnly(p.endsAt, tenant?.timeZone)}</p>
            </article>
          ))}
        </div>
        <p className="center" data-zoom><Link to="/reserve" className="btn">Reserve with an offer</Link></p>
      </div>
    </main>
  )
}
