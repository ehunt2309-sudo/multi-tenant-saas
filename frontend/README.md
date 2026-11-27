# Frontends

npm workspace:

    frontend/
      shared/            @platform/shared - typed API client, auth (login/refresh/logout), formatters. NO design.
      clients/clientA/   first bespoke client site (React + Vite + TypeScript + GSAP)

Each client is its own bespoke design. They all reuse `@platform/shared`, so auth, token refresh and API calls
are written (and secured) once.

## Run locally
1. Start the API (https://localhost:7260) and create a venue whose primary domain is **`localhost:5173`**
   (see docs/BACKEND-SETUP.md, step 4: `POST /api/platform/tenants`). That domain is how the API knows which venue this site is.
2. In `frontend/`:

        npm install
        cp clients/clientA/.env.example clients/clientA/.env.local   # adjust the API port if yours differs
        npm run dev -w client-a

3. Open http://localhost:5173. Sign in as the manager you created, or register as a customer.
   The first time, your browser may warn about the https://localhost:7260 dev certificate: open that URL once and accept it
   (or run `dotnet dev-certs https --trust`).

## Add another client
Copy `clients/clientA` to `clients/clientB`, change `name` in its package.json, give it a different dev port in
`vite.config.ts`, register that host (e.g. `localhost:5174`) as a domain for another test venue, and redesign it.
For production, add the client's real domain with `POST /api/platform/tenants/{id}/domains`.

## Security notes
- The access token lives in memory only. The refresh token is in sessionStorage (cleared when the tab closes) and rotates on every use.
- Never put secrets in frontend code or `.env` files: everything in a browser bundle is public.
- Render CMS/page content as text or sanitised data. Never use `dangerouslySetInnerHTML` on content from the API.
