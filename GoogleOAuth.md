# Sign in with Google — Setup Guide

The backend integrates Google OAuth2 via ASP.NET Core's built-in handler
(`Microsoft.AspNetCore.Authentication.Google`). The Client ID and Client Secret
are read from configuration (`GoogleAuth:ClientId` / `GoogleAuth:ClientSecret`) and
are **never hardcoded**. In the local docker-compose flow they are supplied through
the `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` variables in `.env` (see
`.env.example`).

## 1. Google Cloud Console setup

1. Open the [Google Cloud Console](https://console.cloud.google.com/) and select
   (or create) a project.
2. Navigate to **APIs & Services → Credentials** and click **Create credentials →
   OAuth client ID**.
3. **Application type:** choose **Web application**.
4. **Authorized JavaScript origins** (development):
   - `http://localhost:4200` (dev SPA)
   - `http://localhost` (docker SPA)
   - Add your production origin, e.g. `https://yourdomain.com`
5. **Authorized redirect URIs** (required — the backend appends nothing):
   ```
   http://localhost:4200/auth/callback
   http://localhost/auth/callback
   https://yourdomain.com/auth/callback
   ```
   These MUST match the `Frontend:BaseUrl` your backend uses (default
   `http://localhost:4200`, overridable via `Frontend__BaseUrl` or
   `Cors__AllowedOrigins`).
6. Click **Create**. Google shows the **Client ID** and **Client Secret**.

> **CORS note:** The SPA and API run on different origins. Google returns the
> browser to the frontend at `/auth/callback#access_token=...`. The fragment is
> not sent to the backend. No extra CORS config is needed for the Google redirect
> itself because the browser lands directly on the SPA origin.

## 2. Configuring the API

Choose ONE of the following (highest precedence wins):

### a) User secrets (local development, recommended)
```
dotnet user-secrets set GoogleAuth:ClientId "<YOUR_CLIENT_ID>"
dotnet user-secrets set GoogleAuth:ClientSecret "<YOUR_CLIENT_SECRET>"
```
Run from the `MechanicShop.Api` directory (a `UserSecretsId` is declared in
`MechanicShop.Api.csproj`).

### b) Environment variables (deployment)
```
GoogleAuth__ClientId=<YOUR_CLIENT_ID>
GoogleAuth__ClientSecret=<YOUR_CLIENT_SECRET>
```
Docker Compose already forwards `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` from
`.env` as `GoogleAuth__ClientId` / `GoogleAuth__ClientSecret`.

### c) `appsettings.json` (not recommended for secrets — prefer (a)/(b))
Add the values under `"GoogleAuth"`.

### d) Frontend `Frontend:BaseUrl`
Ensure the redirect URI used by the browser matches. Default is
`http://localhost:4200`; override with `GoogleAuth__Frontend__BaseUrl` is NOT
used — the frontend base comes from `Frontend__BaseUrl` (or appsettings
`Frontend:BaseUrl`). To change the frontend origin, set
`FRONTEND_BASE_URL` in `.env` and use that URL in your Google "Authorized
redirect URIs" and as the SPA's actual URL.

## 3. How the flow works

1. User clicks **Sign in with Google** on the login/portal screen.
2. SPA calls `GET /api/auth/google` → the API responds with a 302 to Google.
3. User consents on Google. Google redirects back to
   `GET /api/auth/signin-google?code=...`. The ASP.NET Core Google handler
   validates the code and signs in via a short-lived, HttpOnly cookie
   (`MechanicShop.External`, lifetime 10 minutes) — this cookie is only for the
   external login and is not the session for regular users.
4. The API redirects the browser back to
   `<FrontendBaseUrl>/auth/callback#access_token=<jwt>&expires_in=<sec>&returnUrl=<path>`.
5. The SPA's `/auth/callback` page reads the fragment, stores the JWT, fetches
   `GET /api/auth/me` to learn the user profile, and navigates to the target page.

### Account linking
- If an account with the same email already exists (password or any provider),
  Google is linked to it and the existing account is returned — no duplicates.
- If none exists, a new Customer account + customer profile are provisioned
  (Google-only: no password).
- If the email was already used by an account that has a **different Google
  provider key**, the link fails and you're asked to try the other provider.

### Failure paths
- `GET /api/auth/google` returns a validation error ("Sign-in with Google is not
  configured…") when `ClientId`/`ClientSecret` are missing.
- `GET /api/auth/google-failure?error=...` surfaces Google-side failures
  (denial, invalid code, etc.).
- `GET /api/auth/google-callback-complete` returns `Auth.GoogleFailed` /
  `Auth.GoogleIncomplete` when the external-login cookie is missing/invalid.

### Scopes
The handler requests the default OAuth2 scope set (`openid profile email`) — enough
to get `email`, `name`, and the `sub` (Google subject) needed for account lookup.

## 4. Testing locally

- `docker compose up --build` (with `.env` containing the Google credentials).
- Open the SPA, click **Sign in with Google**, complete Google consent, and verify
  you land back on the portal with a signed-in session.
- The SPA's login page also shows a "Google" button that calls
  `GET /api/auth/google`.
