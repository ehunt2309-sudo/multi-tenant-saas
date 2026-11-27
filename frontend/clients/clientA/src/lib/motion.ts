import { useEffect, useLayoutEffect, type RefObject } from 'react'
import { gsap } from 'gsap'
import { ScrollTrigger } from 'gsap/ScrollTrigger'
import Lenis from 'lenis'

gsap.registerPlugin(ScrollTrigger)

export const prefersReducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

/** Buttery smooth scrolling kept in sync with ScrollTrigger, a gold progress hairline, and re-measuring once fonts have loaded. */
export function useSmoothScroll() {
  useEffect(() => {
    if (prefersReducedMotion()) return
    const lenis = new Lenis({ lerp: 0.085, wheelMultiplier: 0.9 })
    lenis.on('scroll', ScrollTrigger.update)
    const tick = (t: number) => lenis.raf(t * 1000)
    gsap.ticker.add(tick)
    gsap.ticker.lagSmoothing(0)

    const bar = document.createElement('div')
    bar.className = 'scroll-progress'
    bar.setAttribute('aria-hidden', 'true')
    document.body.appendChild(bar)
    const progress = ScrollTrigger.create({
      start: 0, end: 'max',
      onUpdate: s => { bar.style.transform = `scaleX(${s.progress})` },
    })
    document.fonts?.ready.then(() => ScrollTrigger.refresh())

    return () => { progress.kill(); bar.remove(); gsap.ticker.remove(tick); lenis.destroy() }
  }, [])
}

/** Buttons lean gently towards the cursor, then settle back with a soft spring. Pointer devices only. */
export function useMagnetic() {
  useEffect(() => {
    if (prefersReducedMotion() || !window.matchMedia('(hover: hover)').matches) return
    const btnOf = (t: EventTarget | null) => (t as HTMLElement | null)?.closest?.('.btn:not(:disabled)') as HTMLElement | null
    const move = (e: PointerEvent) => {
      const b = btnOf(e.target); if (!b) return
      const r = b.getBoundingClientRect()
      gsap.to(b, { x: (e.clientX - r.left - r.width / 2) * 0.28, y: (e.clientY - r.top - r.height / 2) * 0.4, duration: 0.4, ease: 'power3.out', overwrite: 'auto' })
    }
    const leave = (e: PointerEvent) => {
      const b = btnOf(e.target); if (!b) return
      gsap.to(b, { x: 0, y: 0, duration: 0.8, ease: 'elastic.out(1, 0.45)', overwrite: 'auto' })
    }
    document.addEventListener('pointermove', move)
    document.addEventListener('pointerout', leave)
    return () => { document.removeEventListener('pointermove', move); document.removeEventListener('pointerout', leave) }
  }, [])
}


/** Elements the scroll effects animate. Reset between runs, otherwise GSAP keeps a stale pixel offset from the previous run. */
const ANIMATED = [
  '[data-words] .word-in', '[data-scrub] .word', '[data-slide]', '[data-rise]', '[data-zoom]', '[data-reveal]',
  '[data-stagger] > *', '[data-img]', '[data-img] > *', '[data-line]', '[data-parallax]',
  '.hero-inner', '.hero-canvas', '.scroll-cue', '.hs-track', '.hs-card', '.hs-card .plate > .art',
  '.expand-frame', '.expand-frame > *', '.expand-copy', '.seq-item', '.seq-bar', '.marquee-inner', '.marquee-track',
].join(',')
const resetAnimated = (root: HTMLElement) =>
  gsap.set(root.querySelectorAll(ANIMATED), { clearProps: 'transform,opacity,clipPath,filter,transformOrigin' })

/**
 * Scroll-driven effects, declared in markup. Everything starts hidden and is "played" by the scrollbar:
 * scroll down and it assembles, scroll back up and it un-assembles.
 *
 *   data-hero         hero lifts away, fades and scales down as you leave it; particles drift the other way
 *   data-slide=left|right   glides in sideways from off-screen
 *   data-rise         floats up from below
 *   data-zoom         grows from small
 *   data-stagger      children arrive one by one with a 3D tilt, alternating sides, at different depths
 *   data-words        words rotate up out of a mask as you scroll
 *   data-scrub        words light up one by one
 *   data-img          image plate: curtain wipe + settle + parallax inside the frame
 *   data-marquee      giant rows of text that travel with the scroll and skew with its speed
 *   data-sequence     section pins; its .seq-item lines blur in and out one after another
 *   data-hscroll      section pins and its .hs-track slides sideways (desktop)
 *   data-expand       section pins while a framed .expand-frame grows to fill the screen
 *   data-line         a hairline that draws itself
 *   data-parallax     slow drift against the scroll
 */
export function useReveal(ref: RefObject<HTMLElement | null>, deps: unknown[]) {
  // Layout effect: start states are applied before the first paint, so nothing flashes on screen before it hides.
  useLayoutEffect(() => {
    const root = ref.current
    if (!root || prefersReducedMotion()) return
    resetAnimated(root)
    const mm = gsap.matchMedia()

    const ctx = gsap.context(() => {
      const all = <T extends HTMLElement>(sel: string) => gsap.utils.toArray<T>(sel)
      /** scrubbed range: begins as the element's top peeks in, finishes about 60% of the way up the screen */
      const range = (el: Element, start = 'top 96%', end = 'top 58%', scrub: number | boolean = 0.7) =>
        ({ trigger: el, start, end, scrub })

      // ── Pinned sections first: they add scroll distance, so everything below must be measured after them ──
      all('[data-expand]').forEach(sec => {
        const frame = sec.querySelector<HTMLElement>('.expand-frame')
        const art = frame?.firstElementChild
        const copy = sec.querySelector<HTMLElement>('.expand-copy')
        if (!frame || !art || !copy) return
        gsap.timeline({ scrollTrigger: { trigger: sec, start: 'top top', end: '+=160%', pin: true, scrub: 0.6 } })
          .fromTo(frame, { clipPath: 'inset(26% 36% 26% 36%)' }, { clipPath: 'inset(0% 0% 0% 0%)', ease: 'none', duration: 1 }, 0)
          .fromTo(art, { scale: 1.6, rotate: -2 }, { scale: 1, rotate: 0, ease: 'none', duration: 1 }, 0)
          .fromTo(copy, { opacity: 0, y: 80, scale: 0.94 }, { opacity: 1, y: 0, scale: 1, ease: 'power2.out', duration: 0.5 }, 0.6)
      })

      all('[data-sequence]').forEach(sec => {
        const items = sec.querySelectorAll<HTMLElement>('.seq-item')
        const bar = sec.querySelector<HTMLElement>('.seq-bar')
        if (!items.length) return
        const tl = gsap.timeline({ scrollTrigger: { trigger: sec, start: 'top top', end: `+=${items.length * 100}%`, pin: true, scrub: 0.6 } })
        items.forEach((it, i) => {
          tl.fromTo(it, { opacity: 0, yPercent: 35, scale: 0.88, filter: 'blur(16px)' },
            { opacity: 1, yPercent: 0, scale: 1, filter: 'blur(0px)', duration: 1, ease: 'power2.out' })
          if (i < items.length - 1)
            tl.to(it, { opacity: 0, yPercent: -35, scale: 1.1, filter: 'blur(16px)', duration: 1, ease: 'power2.in' }, '+=0.5')
        })
        if (bar) tl.fromTo(bar, { scaleX: 0 }, { scaleX: 1, ease: 'none', duration: tl.duration() }, 0)
      })

      mm.add('(min-width: 900px)', () => {
        all('[data-hscroll]').forEach(sec => {
          const track = sec.querySelector<HTMLElement>('.hs-track')
          if (!track) return
          const dist = () => Math.max(0, track.scrollWidth - window.innerWidth)
          const slide = gsap.to(track, {
            x: () => -dist(), ease: 'none',
            scrollTrigger: { trigger: sec, pin: true, scrub: 0.8, start: 'top top', end: () => '+=' + dist(), invalidateOnRefresh: true },
          })
          sec.querySelectorAll<HTMLElement>('.hs-card').forEach(card => {
            // each card rises, straightens and un-blurs as it travels in from the right edge
            gsap.fromTo(card, { opacity: 0.1, y: 110, rotate: 4, scale: 0.9 },
              { opacity: 1, y: 0, rotate: 0, scale: 1, ease: 'power2.out',
                scrollTrigger: { trigger: card, containerAnimation: slide, start: 'left 105%', end: 'left 62%', scrub: true } })
            const a = card.querySelector<HTMLElement>('.plate > .art')
            if (a) gsap.fromTo(a, { xPercent: -8, scale: 1.1 }, { xPercent: 8, scale: 1, ease: 'none',
              scrollTrigger: { trigger: card, containerAnimation: slide, start: 'left right', end: 'right left', scrub: true } })
          })
        })
      })

      // ── Hero: leaves the stage as you scroll away ──
      all('[data-hero]').forEach(hero => {
        const inner = hero.querySelector('.hero-inner')
        const canvas = hero.querySelector('.hero-canvas')
        const cue = hero.querySelector('.scroll-cue')
        const tl = gsap.timeline({ scrollTrigger: { trigger: hero, start: 'top top', end: 'bottom top', scrub: true } })
        if (inner) tl.to(inner, { yPercent: -22, opacity: 0, scale: 0.93, ease: 'none' }, 0)
        if (canvas) tl.to(canvas, { yPercent: 22, scale: 1.12, ease: 'none' }, 0)
        if (cue) tl.to(cue, { opacity: 0, ease: 'none', duration: 0.2 }, 0)
      })

      // ── Image plates ──
      all('[data-img]').forEach(el => {
        const inner = el.firstElementChild as HTMLElement | null
        gsap.fromTo(el, { clipPath: 'inset(100% 0% 0% 0%)' },
          { clipPath: 'inset(0% 0% 0% 0%)', ease: 'power3.out', scrollTrigger: range(el, 'top 98%', 'top 40%', 0.8) })
        if (!inner) return
        gsap.fromTo(inner, { scale: 1.4 }, { scale: 1, ease: 'power3.out', scrollTrigger: range(el, 'top 98%', 'top 35%', 0.8) })
        gsap.fromTo(inner, { yPercent: -7 }, { yPercent: 7, ease: 'none', scrollTrigger: { trigger: el, scrub: true, start: 'top bottom', end: 'bottom top' } })
      })

      // ── Text ──
      all('[data-words]').forEach(el =>
        gsap.fromTo(el.querySelectorAll('.word-in'), { yPercent: 125, rotate: 8, opacity: 0, transformOrigin: '0% 100%' },
          { yPercent: 0, rotate: 0, opacity: 1, stagger: 0.09, ease: 'power3.out', scrollTrigger: range(el, 'top 96%', 'top 40%', 0.9) }))
      all('[data-scrub]').forEach(el =>
        gsap.fromTo(el.querySelectorAll('.word'), { opacity: 0.12 }, { opacity: 1, stagger: 0.12, ease: 'none',
          scrollTrigger: { trigger: el, start: 'top 80%', end: 'bottom 45%', scrub: true } }))

      // ── Directional entrances ──
      all('[data-slide]').forEach(el => {
        const dir = el.dataset.slide === 'right' ? 1 : -1
        gsap.fromTo(el, { x: dir * 220, opacity: 0 }, { x: 0, opacity: 1, ease: 'power3.out', scrollTrigger: range(el, 'top 100%', 'top 55%') })
      })
      all('[data-rise]').forEach(el =>
        gsap.fromTo(el, { y: 140, opacity: 0 }, { y: 0, opacity: 1, ease: 'power3.out', scrollTrigger: range(el, 'top 100%', 'top 60%') }))
      all('[data-zoom]').forEach(el =>
        gsap.fromTo(el, { scale: 0.78, opacity: 0 }, { scale: 1, opacity: 1, ease: 'power3.out', scrollTrigger: range(el, 'top 100%', 'top 50%') }))
      // legacy alias used across pages
      all('[data-reveal]').forEach(el =>
        gsap.fromTo(el, { y: 90, opacity: 0 }, { y: 0, opacity: 1, ease: 'power3.out', scrollTrigger: range(el, 'top 100%', 'top 66%') }))

      all('[data-stagger]').forEach(group => {
        Array.from(group.children).forEach((child, i) => {
          gsap.fromTo(child,
            { y: 130 + (i % 3) * 55, x: (i % 2 ? 1 : -1) * 70, opacity: 0, rotateX: 26, rotateZ: (i % 2 ? 1 : -1) * 2.5, scale: 0.9, transformPerspective: 1100, transformOrigin: '50% 100%' },
            { y: 0, x: 0, opacity: 1, rotateX: 0, rotateZ: 0, scale: 1, ease: 'power3.out', scrollTrigger: range(child, 'top 105%', 'top 62%', 0.9) })
        })
      })

      all('[data-line]').forEach(el =>
        gsap.fromTo(el, { scaleX: 0, transformOrigin: 'left center' }, { scaleX: 1, ease: 'power2.inOut', scrollTrigger: range(el, 'top 98%', 'top 70%', 0.8) }))
      all('[data-parallax]').forEach(el =>
        gsap.to(el, { yPercent: -12, ease: 'none', scrollTrigger: { trigger: el.parentElement, scrub: true, start: 'top bottom', end: 'bottom top' } }))

      // ── Scroll-driven marquee: rows travel opposite ways, and skew with the scroll speed ──
      all('[data-marquee]').forEach(m => {
        const inner = m.querySelector<HTMLElement>('.marquee-inner')
        m.querySelectorAll<HTMLElement>('.marquee-track').forEach((row, i) => {
          const from = i % 2 ? -32 : 0, to = i % 2 ? 0 : -32
          gsap.fromTo(row, { xPercent: from }, { xPercent: to, ease: 'none', scrollTrigger: { trigger: m, start: 'top bottom', end: 'bottom top', scrub: true } })
        })
        if (!inner) return
        const skew = gsap.quickTo(inner, 'skewX', { duration: 0.5, ease: 'power3.out' })
        let idle: number | undefined
        ScrollTrigger.create({
          trigger: m, start: 'top bottom', end: 'bottom top',
          onUpdate: s => {
            skew(gsap.utils.clamp(-9, 9, s.getVelocity() / -260))
            window.clearTimeout(idle); idle = window.setTimeout(() => skew(0), 120)
          },
          onLeave: () => skew(0), onLeaveBack: () => skew(0),
        })
      })
    }, root)

    ScrollTrigger.sort()
    ScrollTrigger.refresh()
    return () => { mm.revert(); ctx.revert(); resetAnimated(root) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)
}
