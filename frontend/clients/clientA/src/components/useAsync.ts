import { useCallback, useEffect, useState } from 'react'
import { errorMessage } from './Status'

/** Loads data on mount; `reload()` refetches. Ignores results that arrive after unmount. */
export function useAsync<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [tick, setTick] = useState(0)

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fn, [tick, ...deps])

  useEffect(() => {
    let live = true
    setLoading(true)
    run()
      .then(d => { if (live) { setData(d); setError(null) } })
      .catch(e => { if (live) setError(errorMessage(e)) })
      .finally(() => { if (live) setLoading(false) })
    return () => { live = false }
  }, [run])

  return { data, error, loading, reload: () => setTick(t => t + 1) }
}
