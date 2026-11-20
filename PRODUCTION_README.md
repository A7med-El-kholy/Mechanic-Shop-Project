# Mechanic Shop Workshop System — Production Ready

## 🚀 Quick Start

### Prerequisites
- **Docker Desktop** (Windows/Mac) or **Docker Engine** (Linux)
- **Node.js 18+** and **npm** (for frontend development)
- **.NET 10 SDK** (for backend development)

### Option 1: Docker (Recommended — Full Stack)

```bash
# Clone and enter the project
cd /mnt/c/Users/Professor/Desktop/GraduationProject/MechanicShopWorkshopSystem

# Copy environment variables
cp .env.example .env

# If browser/ or publish/api/ are missing, run the Production Build steps first.
docker compose up -d --build
```

Wait for all containers to be healthy, then open:

| Service | URL |
|---------|-----|
| **Website (Frontend)** | `http://localhost` |
| **API (Backend)** | `http://localhost:5255` |
| **Swagger API Docs** | `http://localhost:5255/swagger` |

**Default credentials:**
- Manager: `manager@mechanicshop.dev` / `Manager@Dev2026`
- Or register your own customer account at `http://localhost/login`

### Sign in with Google

Click **Continue with Google** on the login page. The flow is: SPA → `GET /api/auth/google` → Google → `GET /api/auth/google-callback-complete` (redirects back to the SPA with the JWT in the URL fragment) → `/auth/callback`. The backend exchanges the token for a JWT; the SPA stores it and lands on the portal or the requested destination. Accounts are linked by email, so an existing local account or a Google-only account can't be duplicated.

> **Setup:** create a Google OAuth client in the [Google Cloud Console](https://console.cloud.google.com/), add authorized redirect URIs (`http://localhost/auth/callback` for docker, `http://localhost:4200/auth/callback` for the dev SPA), and set the Client ID/Secret via user-secrets (`dotnet user-secrets set GoogleAuth:ClientId/Secret`) or `.env` (`GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`). See [GoogleOAuth.md](GoogleOAuth.md) for the full checklist and [AIProvider.md](AIProvider.md) for AI setup.

### Option 2: Frontend Only (Development)

```bash
cd src/MechanicShop.Web
npm install
npm start        # http://localhost:4200 with proxy to API
```

### Option 3: Backend Only (Development)

```bash
cd src/MechanicShop.Api
dotnet run       # http://localhost:5255
```

---

## 🤖 AI Assistant — Multimodal

The AI Master Technician supports four modes of interaction:

| Mode | How to use |
|------|-----------|
| **Text** | Type symptoms in the chat input |
| **Voice (Speak to AI)** | Click the 🎤 microphone button, speak, the AI transcribes and diagnoses |
| **Voice (AI speaks back)** | The agent reads every diagnosis aloud via browser TTS |
| **Image** | Click 📷 to upload a photo of a warning light, leak, or part |

**To access:** Click the floating 🤖 AI Voice Assistant button (bottom-right), or navigate to **AI Assistant**.

**No API key required:** The built-in Mock AI engine provides realistic diagnoses for brakes, overheating, battery, transmission, and engine issues. To connect a real AI provider, set `AISettings:Provider=OpenAI` and configure `AISettings:OpenAI:ApiKey` (see [AIProvider.md](AIProvider.md)) — the same `IAIService` interface allows swapping to Gemini or any OpenAI-compatible server by changing `AISettings:OpenAI:BaseUrl`.

### Payments and demo data

The portal currently supports cash-at-shop/pickup reservations. Card requests are rejected until a payment gateway is configured; selecting a payment method never marks an invoice paid. A manager records cash received in the workshop. Docker demo seeding is controlled by `DEMO_SEED_DATA` (`true` in `.env.example`); set it to `false` for real production data to avoid inserting sample customers, vehicles, appointments, work orders, parts, and services.

---

## 🏗️ Architecture

```
MechanicShop.slnx
├── MechanicShop.Api          # ASP.NET Core 10 Web API (endpoints, auth, Swagger)
├── MechanicShop.Application  # CQRS use cases, MediatR, validators, IAIService interface
├── MechanicShop.Domain       # Entities, value objects, domain contracts
├── MechanicShop.Infrastructure # EF Core, Identity, MockAIService implementation
├── MechanicShop.Web          # Angular 21 frontend (TypeScript, SCSS)
└── MechanicShop.WorkshopSystem # (placeholder)
```

### API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/auth/register` | Customer registration |
| `POST` | `/api/auth/login` | Login (JWT) |
| `GET` | `/api/auth/me` | Current user profile |
| `POST` | `/api/ai/diagnose` | Text symptom → diagnosis |
| `POST` | `/api/ai/diagnose-image` | Photo upload → diagnosis |
| `POST` | `/api/ai/transcribe` | Audio upload → transcript |
| `POST` | `/api/ai/speak` | Text → audio (base64) |
| `GET` | `/api/ai/voices` | Available TTS voices |

---

## 🐳 Docker Compose Services

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| `sql` | `mcr.microsoft.com/mssql/server:2022-latest` | 1433 | SQL Server 2022 Express |
| `api` | `mechanic-shop-api` | 5255 | ASP.NET Core API |
| `spa` | `mechanic-shop-spa` | 80 | Angular + nginx |

### Environment Variables (`.env`)

```
SQL_PASSWORD=Mech@Shop_Dev2026
MANAGER_PASSWORD=Manager@Dev2026
JWT_SECRET=replace-with-a-random-base64-256-bit-key

# AI settings
AISettings__Provider=Mock
AISettings__DefaultVoice=en-US-Neural
AISettings__DefaultLanguage=en-US
```

---

## 🎨 Frontend Features

- **Angular 21** with standalone components
- **TypeScript** strict mode
- **SCSS** with CSS custom properties (design tokens)
- **Responsive** mobile-first design
- **Glassmorphism** UI with backdrop blur
- **Animations**: fade-in, float, pulse, shimmer, typewriter, radar
- **Loading bar** on HTTP requests
- **Toast notifications** (success, error, warn, info)
- **Voice input** (Web Speech API SpeechRecognition)
- **Voice output** (Web Speech API SpeechSynthesis)
- **Image upload** (drag photo for AI analysis)
- **JWT auth** with automatic token attachment
- **Route guards** (auth, manager, portal)

---

## 📦 Production Build

The Docker images serve pre-built artifacts (`browser/` and `publish/api/`).
Run these from the repository root whenever source code changes:

```bash
# Frontend — Angular outputs to dist/, then copy into the Docker context
(cd src/MechanicShop.Web && npm run build)
rm -rf browser
cp -r src/MechanicShop.Web/dist/mechanic-shop-web/browser browser

# Backend
dotnet publish src/MechanicShop.Api/MechanicShop.Api.csproj -c Release -o publish/api

# Rebuild and restart
docker compose up -d --build
```

Nginx config includes:
- `client_max_body_size 32m` (image/audio uploads)
- Security headers (X-Frame-Options, X-Content-Type-Options, etc.)
- Gzip compression
- Asset caching (1 year for static files)
- 300s proxy timeout for AI processing

---

## 🔧 Troubleshooting

| Problem | Fix |
|---------|-----|
| Port 80 in use | Change `spa` port in `docker-compose.yml` to `8080:80` |
| API not healthy | Wait for SQL Server to start (20–30s), check `docker compose logs api` |
| Voice not working | Use Chrome/Edge/Safari (Web Speech API) and allow microphone |
| 401 errors | Log out and log in again; JWT expires after 60 minutes |
| Build fails | Run `docker compose build --no-cache` |

### View Logs
```bash
docker compose logs -f api      # Backend logs
docker compose logs -f spa      # Frontend/nginx logs
docker compose logs -f sql      # Database logs
```

### Stop Everything
```bash
docker compose down
```

---

## 📄 License

Internal project — Mechanic Shop Workshop System.
