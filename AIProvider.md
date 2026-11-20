# AI Provider Configuration (Sign-in / AI Assistant)

The application has two interchangeable AI backends, both implementing the
same `IAIService` interface:

| Provider | Status | Needs API key |
|----------|--------|---------------|
| `Mock` (default) | Offline, rule-based | No — works out of the box |
| `OpenAI` | Real OpenAI-compatible API | Yes |

The factory in `Program.cs` picks the active provider at runtime. Selecting an
unknown provider or selecting OpenAI without a key fails startup instead of
silently giving users mock answers.

```
AISettings:Provider == "OpenAI"  AND  AISettings:OpenAI:ApiKey is set
    → OpenAIService
    → otherwise → MockAIService
```

Both are registered; only the active one receives AI calls. OpenAI failures
return a 503 (or 504 on timeout); the API does not disguise a failed live call
as a successful mock answer.

## Configuring the OpenAI provider

Set the following (never hardcode the key in source — it ships via config/secrets):

### via `dotnet user-secrets` (local dev, recommended)
```bash
cd src/MechanicShop.Api

dotnet user-secrets set AISettings:Provider "OpenAI"
dotnet user-secrets set AISettings:OpenAI:ApiKey "sk-your-key-here"
dotnet user-secrets set AISettings:OpenAI:BaseUrl "https://api.openai.com/v1"
dotnet user-secrets set AISettings:OpenAI:Model "gpt-4o-mini"
dotnet user-secrets set AISettings:OpenAI:TranscriptionModel "whisper-1"
dotnet user-secrets set AISettings:OpenAI:SpeechModel "tts-1"
dotnet user-secrets set AISettings:OpenAI:TimeoutSeconds "30"
```

### via environment variables / docker-compose
```
AISettings__Provider=OpenAI
AISettings__OpenAI__ApiKey=sk-your-key-here
AISettings__OpenAI__BaseUrl=https://api.openai.com/v1
AISettings__OpenAI__Model=gpt-4o-mini
AISettings__OpenAI__TranscriptionModel=whisper-1
AISettings__OpenAI__SpeechModel=tts-1
AISettings__OpenAI__TimeoutSeconds=30
```

### via `appsettings.json`
```json
"AISettings": {
  "Provider": "OpenAI",
  "OpenAI": {
    "ApiKey": "",
    "BaseUrl": "https://api.openai.com/v1",
    "Model": "gpt-4o-mini",
    "TranscriptionModel": "whisper-1",
    "SpeechModel": "tts-1",
    "TimeoutSeconds": 30
  }
}
```

## Key values

| Setting | Default | Meaning |
|---------|---------|---------|
| `AISettings:OpenAI:ApiKey` | `""` | Bearer key. Required for OpenAI. |
| `AISettings:OpenAI:BaseUrl` | `https://api.openai.com/v1` | API base URL. Change to Azure OpenAI (`https://<resource>.openai.azure.com/openai/v1`) or any OpenAI-compatible server (vLLM, Ollama, LM Studio). |
| `AISettings:OpenAI:Model` | `gpt-4o-mini` | Chat model for diagnose (vision-capable). |
| `AISettings:OpenAI:TranscriptionModel` | `whisper-1` | Audio transcription model. |
| `AISettings:OpenAI:SpeechModel` | `tts-1` | Text-to-speech model. |
| `AISettings:OpenAI:TimeoutSeconds` | `30` | HTTP timeout for AI calls. |

The model settings can be overridden for compatible providers. Provider
endpoints use the configured base URL and bearer key. Image diagnosis preserves
the uploaded JPEG/PNG/WEBP media type in the vision request.

> **Important:** the API key is only ever read server-side
> (`OpenAIService`) and sent as an `Authorization: Bearer` header by the
> HttpClient. It is **never** serialized into any response, so the Angular SPA
> cannot see it.

## Provider capabilities

| Feature | OpenAI | Mock |
|---------|--------|------|
| Text diagnosis (structured JSON) | ✅ real | ⚠️ rule-based demo |
| Image diagnosis (vision) | ✅ real (base64 `image_url`) | ⚠️ image content is not analyzed |
| Speech-to-text (`/transcribe`) | ✅ real | ⚠️ sample transcript only |
| Text-to-speech (`/speak`) | ✅ real (mp3 base64) | ⚠️ silence WAV |
| Available voices (`/voices`) | ✅ static catalogue (alloy/echo/fable/…) | ✅ static catalogue |

## Timeouts & failure modes

- `AISettings:OpenAI:TimeoutSeconds` — HTTP timeout per call (default 30s).
- Cancellation propagates from the endpoint through to the provider call.
- Non-2xx responses, network errors, and unparseable JSON are logged; API calls
  return a service-unavailable response (or timeout response). There is no
  mock fallback on a configured live provider.

## Testing

- With `AISettings:Provider=Mock` (default): `POST /api/ai/diagnose` returns
  structured Mock diagnoses — no network or keys needed.
- With `Provider=OpenAI` + key: real calls. Point `BaseUrl` at your provider
  and call `POST /api/ai/diagnose` with a symptom description (swagger or the
  SPA's AI Assistant).

## Privacy / cost note

Using the real provider sends vehicle symptoms, relevant matching service/part
catalog entries, and (for image diagnosis) photos to the configured provider.
The key controls access only. For air-gapped or privacy-sensitive use, keep
the Mock provider. The browser voice UI currently uses browser speech APIs;
the server audio endpoints are separately available and are not used by that UI.
