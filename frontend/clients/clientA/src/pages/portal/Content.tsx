import { useState, type FormEvent } from 'react'
import { api } from '@platform/shared'
import { PageHead, useSubmit } from '../../components/ui'
import { Notice } from '../../components/Status'
import { useAsync } from '../../components/useAsync'
import { useTenant } from '../../tenant'

export default function Content() {
  const { tenant, content, reload } = useTenant()
  const page = useAsync(() => api.pages.getForEdit('home').catch(() => null))
  const site = useSubmit()
  const brand = useSubmit()
  const [msg, setMsg] = useState<string | null>(null)

  async function saveSite(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const body = {
      title: 'Home', isPublished: true,
      content: {
        tagline: String(f.get('tagline')).trim(), about: String(f.get('about')).trim(),
        address: String(f.get('address')).trim(), phone: String(f.get('phone')).trim(), email: String(f.get('email')).trim(),
        hours: String(f.get('hours')).split('\n').map(l => l.trim()).filter(Boolean),
      },
    }
    if (await site.run(() => api.pages.upsert('home', body))) { setMsg('Website content saved.'); reload() }
  }

  async function saveBrand(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    const color = String(f.get('color'))
    if (await brand.run(() => api.tenant.updateSettings({
      primaryColor: f.get('useColor') === 'on' ? color : null,
      logoUrl: String(f.get('logo')).trim() || null,
      loyaltyPointsPerCurrencyUnit: Number(f.get('rate')),
    }))) { setMsg('Branding and rewards settings saved.'); reload() }
  }

  const c = page.data?.content ?? content
  return (
    <>
      <PageHead title="Website & settings" sub="Edit what visitors see, and how rewards work" />
      {msg && <Notice kind="ok">{msg}</Notice>}
      <div className="grid-2">
        <form onSubmit={saveSite} className="panel form" key={String(page.loading)}>
          <h3>Home page content</h3>
          <label className="fld">Tagline<input name="tagline" defaultValue={c.tagline} maxLength={160} /></label>
          <label className="fld">About (the statement under the hero)<textarea name="about" rows={4} defaultValue={c.about} maxLength={600} /></label>
          <label className="fld">Address<input name="address" defaultValue={c.address} /></label>
          <div className="row"><label className="fld">Phone<input name="phone" defaultValue={c.phone} /></label><label className="fld">Email<input name="email" defaultValue={c.email} /></label></div>
          <label className="fld">Opening hours (one line each)<textarea name="hours" rows={4} defaultValue={(c.hours ?? []).join('\n')} /></label>
          {site.error && <Notice>{site.error}</Notice>}
          <button className="btn" disabled={site.busy}>Save content</button>
        </form>

        <form onSubmit={saveBrand} className="panel form">
          <h3>Branding &amp; rewards</h3>
          <label className="check"><input name="useColor" type="checkbox" defaultChecked={!!tenant?.primaryColor} /> Use a custom accent colour</label>
          <label className="fld">Accent colour<input name="color" type="color" defaultValue={tenant?.primaryColor ?? '#b8975a'} /></label>
          <label className="fld">Logo URL (https, optional)<input name="logo" type="url" defaultValue={tenant?.logoUrl ?? ''} /></label>
          <label className="fld">Loyalty points earned per {tenant?.currency} spent<input name="rate" type="number" min={0} max={100} step="0.01" defaultValue={tenant?.loyaltyPointsPerCurrencyUnit ?? 1} required /></label>
          {brand.error && <Notice>{brand.error}</Notice>}
          <button className="btn" disabled={brand.busy}>Save settings</button>
        </form>
      </div>
    </>
  )
}
