import { ApiError } from '@platform/shared'

export const errorMessage = (e: unknown) =>
  e instanceof ApiError ? e.message : 'Something went wrong. Please try again.'

export function Notice({ kind = 'error', children }: { kind?: 'error' | 'ok'; children: React.ReactNode }) {
  return <p className={`notice ${kind}`} role={kind === 'error' ? 'alert' : 'status'}>{children}</p>
}
