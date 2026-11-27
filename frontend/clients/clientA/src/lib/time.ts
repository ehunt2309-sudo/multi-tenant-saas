/** <input type="datetime-local"> works in the visitor's wall-clock time; the API wants absolute UTC instants. */
const pad = (n: number) => String(n).padStart(2, '0')

export function toLocalInput(iso: string | Date): string {
  const d = typeof iso === 'string' ? new Date(iso) : iso
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}
export const fromLocalInput = (v: string) => new Date(v).toISOString()

export const dayInput = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
export const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate())
export const addDays = (d: Date, n: number) => { const x = new Date(d); x.setDate(x.getDate() + n); return x }
export const startOfWeek = (d: Date) => { const x = startOfDay(d); const dow = (x.getDay() + 6) % 7; return addDays(x, -dow) } // Monday
export const hhmm = (iso: string, timeZone?: string) =>
  new Intl.DateTimeFormat('en-ZA', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone }).format(new Date(iso))
