import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Port 5173 matters: the backend recognises this venue by its registered domain "localhost:5173".
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
})
