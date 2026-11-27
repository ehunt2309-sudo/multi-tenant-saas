import { useEffect, useRef } from 'react'
import * as THREE from 'three'

/** Slowly drifting gold dust with a whisper of mouse parallax. Pauses off-screen and respects the venue accent colour. */
export default function HeroScene() {
  const ref = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = ref.current
    if (!canvas) return
    let renderer: THREE.WebGLRenderer
    try { renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true }) } catch { return } // no WebGL: the CSS background still looks good

    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(55, 1, 0.1, 100)
    camera.position.z = 6

    const accent = getComputedStyle(document.documentElement).getPropertyValue('--accent').trim() || '#b8975a'
    const count = 650
    const pos = new Float32Array(count * 3)
    const drift = new Float32Array(count)
    for (let i = 0; i < count; i++) {
      pos[i * 3] = (Math.random() - 0.5) * 18
      pos[i * 3 + 1] = (Math.random() - 0.5) * 12
      pos[i * 3 + 2] = (Math.random() - 0.5) * 8
      drift[i] = 0.05 + Math.random() * 0.18
    }
    const geo = new THREE.BufferGeometry()
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3))

    const c = document.createElement('canvas'); c.width = c.height = 64
    const g = c.getContext('2d')!
    const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32)
    grad.addColorStop(0, 'rgba(255,255,255,1)'); grad.addColorStop(1, 'rgba(255,255,255,0)')
    g.fillStyle = grad; g.fillRect(0, 0, 64, 64)
    const sprite = new THREE.CanvasTexture(c)

    const mat = new THREE.PointsMaterial({
      size: 0.1, map: sprite, color: new THREE.Color(accent), transparent: true, opacity: 0.75,
      depthWrite: false, blending: THREE.AdditiveBlending,
    })
    const points = new THREE.Points(geo, mat)
    scene.add(points)

    const resize = () => {
      const p = canvas.parentElement!
      renderer.setSize(p.clientWidth, p.clientHeight, false)
      camera.aspect = p.clientWidth / p.clientHeight
      camera.updateProjectionMatrix()
    }
    resize()
    window.addEventListener('resize', resize)

    const target = { x: 0, y: 0 }
    const onMove = (e: MouseEvent) => { target.x = (e.clientX / window.innerWidth - 0.5) * 0.8; target.y = (e.clientY / window.innerHeight - 0.5) * 0.5 }
    window.addEventListener('mousemove', onMove)

    let visible = true
    const io = new IntersectionObserver(([e]) => { visible = e.isIntersecting })
    io.observe(canvas)

    const clock = new THREE.Clock()
    let raf = 0
    const tick = () => {
      raf = requestAnimationFrame(tick)
      if (!visible || document.hidden) return
      const dt = clock.getDelta()
      const p = geo.attributes.position as THREE.BufferAttribute
      for (let i = 0; i < count; i++) {
        let y = p.getY(i) + drift[i] * dt
        if (y > 6) y = -6
        p.setY(i, y)
      }
      p.needsUpdate = true
      points.rotation.y += dt * 0.02
      camera.position.x += (target.x - camera.position.x) * 0.03
      camera.position.y += (-target.y - camera.position.y) * 0.03
      camera.lookAt(0, 0, 0)
      renderer.render(scene, camera)
    }
    tick()

    return () => {
      cancelAnimationFrame(raf)
      window.removeEventListener('resize', resize)
      window.removeEventListener('mousemove', onMove)
      io.disconnect()
      geo.dispose(); mat.dispose(); sprite.dispose(); renderer.dispose()
    }
  }, [])

  return <canvas ref={ref} className="hero-canvas" aria-hidden="true" />
}
