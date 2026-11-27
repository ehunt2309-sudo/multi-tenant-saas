import { Fragment } from 'react'

/** Splits a sentence into masked words so scroll effects (data-words / data-scrub) can animate them one by one.
 *  The space lives outside the inline-block word, otherwise the browser collapses it and words run together. */
export function Words({ text }: { text: string }) {
  return (
    <>
      {text.split(/\s+/).filter(Boolean).map((w, i) => (
        <Fragment key={i}><span className="word"><span className="word-in">{w}</span></span>{' '}</Fragment>
      ))}
    </>
  )
}
