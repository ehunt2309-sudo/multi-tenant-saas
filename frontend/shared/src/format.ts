/** Formatting helpers. Times are shown in the venue's own time zone, whatever the visitor's device says. */
export const money = (amount: number, currency = 'ZAR') =>
  new Intl.NumberFormat('en-ZA', { style: 'currency', currency }).format(amount)

export const dateTime = (iso: string, timeZone = 'Africa/Johannesburg') =>
  new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(new Date(iso))

export const dateOnly = (iso: string, timeZone = 'Africa/Johannesburg') =>
  new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeZone }).format(new Date(iso))
