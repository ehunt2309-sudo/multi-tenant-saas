import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { AuthProvider, configureApi } from '@platform/shared'
import App from './App'
import { TenantProvider } from './tenant'
import './styles.css'
import './portal.css'

configureApi({ baseUrl: import.meta.env.VITE_API_URL ?? 'https://localhost:7260' })

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <TenantProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </TenantProvider>
    </BrowserRouter>
  </StrictMode>,
)
