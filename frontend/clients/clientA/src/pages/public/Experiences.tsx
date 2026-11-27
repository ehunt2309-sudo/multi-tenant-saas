import { useRef } from 'react'
import { Link } from 'react-router-dom'
import { api, money } from '@platform/shared'
import { Art, Loading } from '../../components/ui'
import { Words } from '../../components/Words'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useReveal } from '../../lib/motion'
import { useTenant } from '../../tenant'

export default function Experiences() {
  const { tenant } = useTenant()
  const root = useRef<HTMLElement>(null)
  const services = useAsync(() => api.services.list())
  useReveal(root, [services.loading])

  return (
    <main ref={root} className="page-top">
      <div className="wrap">
        <p className="eyebrow" data-slide="left">Experiences</p>
        <h1 className="display sm" data-words><Words text="Choose your ritual" /></h1>
        {services.loading && <Loading />}
        {services.error && <Notice>{services.error}</Notice>}
        {services.data?.map((s, i) => (
          <article key={s.id} className={`xp-row ${i % 2 ? 'flip' : ''}`}>
            <div className="plate" data-img><Art seed={s.name} label={String(i + 1).padStart(2, '0')} tall /></div>
            <div data-slide={i % 2 ? 'left' : 'right'}>
              <h2>{s.name}</h2>
              <p className="muted">{s.description}</p>
              <p className="gold">{s.durationMinutes} minutes · {money(s.price, tenant?.currency)}</p>
              <Link to={`/reserve?service=${s.id}`} className="btn ghost">Reserve</Link>
            </div>
          </article>
        ))}
      </div>
    </main>
  )
}
