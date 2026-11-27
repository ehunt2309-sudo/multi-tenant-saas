import { money } from '@platform/shared'

/** Dependency-free SVG bar chart. */
export default function BarChart({ data, currency }: { data: { label: string; value: number }[]; currency?: string }) {
  if (data.length === 0) return <p className="muted empty">No completed bookings in this period yet.</p>
  const max = Math.max(...data.map(d => d.value), 1)
  const W = 720, H = 190, pad = 24, bw = (W - pad * 2) / data.length
  const step = Math.ceil(data.length / 10)
  return (
    <div className="barchart" role="img" aria-label="Revenue by day">
      <svg viewBox={`0 0 ${W} ${H + 24}`} preserveAspectRatio="none">
        {[0.25, 0.5, 0.75, 1].map(f => <line key={f} x1={pad} x2={W - pad} y1={H - f * (H - 16)} y2={H - f * (H - 16)} className="grid" />)}
        {data.map((d, i) => {
          const h = (d.value / max) * (H - 16)
          return (
            <g key={d.label}>
              <rect x={pad + i * bw + bw * 0.18} y={H - h} width={bw * 0.64} height={h} rx={2}><title>{`${d.label}: ${money(d.value, currency)}`}</title></rect>
              {i % step === 0 && <text x={pad + i * bw + bw / 2} y={H + 16} textAnchor="middle">{d.label}</text>}
            </g>
          )
        })}
      </svg>
    </div>
  )
}
