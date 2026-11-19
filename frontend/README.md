# PropertyManagement Frontend

Read-only React dashboard for the [PropertyManagement](https://github.com/1TahaAhmed/PropertyManagement) .NET API.

## Included screens

- Sign in with the existing API authentication endpoint.
- Portfolio overview with property, resident, lease, invoice, and payment summaries.
- Properties and units.
- Residents and leases.
- Invoices and payments.
- General ledger and financial audit trail (shown according to the signed-in role).

This interface intentionally does not expose create, update, delete, payment, refund, or other mutation actions.

## Requirements

- Node.js 20 or later
- npm
- A running PropertyManagement API and an account with read permissions

## Run locally

```bash
cd frontend
cp .env.example .env
npm install
npm run dev
```

Set `VITE_API_BASE_URL` in `.env` to the API origin. The default is `https://localhost:7261`, matching the local ASP.NET Core HTTPS profile. For Docker Compose, use `http://localhost:5000`.

Open http://localhost:5173.

## Build

```bash
npm run build
```

## API and security notes

- Access tokens and refresh tokens are held in browser `sessionStorage` and cleared on sign out or failed refresh.
- The app calls the existing `/api/v1/Auth/login` and `/api/v1/Auth/refresh-token` endpoints.
- All data pages use GET endpoints from the existing API.
- API authorization remains authoritative; hiding a navigation item is only a user-interface convenience.
- Configure the API CORS policy to allow the frontend origin (for local Vite development: `http://localhost:5173`).
- Do not put provider secrets or backend credentials in frontend environment variables.

The dashboard summarizes the first 100 results returned by each endpoint; data tables load additional pages as needed.
