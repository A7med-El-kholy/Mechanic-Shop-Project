namespace MechanicShop.Infrastructure.AI;

using MechanicShop.Application.Features.AI;
using MechanicShop.Application.Features.AI.DTOs;

/// <summary>
/// Offline, rule-based AI diagnostic engine. No external API key required —
/// this lets the AI assistant work end-to-end in development, demos, and
/// air-gapped environments. When the shop configures a real AI provider,
/// replace this registration with an OpenAI/Azure/Gemini implementation
/// behind the same IAIService interface.
/// </summary>
public sealed class MockAIService : IAIService
{
    private static readonly Random _rng = new();

    public Task<DiagnoseResponse> DiagnoseTextAsync(DiagnoseTextRequest request, CancellationToken ct = default)
    {
        var query = request.Description ?? string.Empty;
        var diag = BuildDiagnosis(query);
        var responseText = BuildResponseText(diag, query);
        return Task.FromResult(new DiagnoseResponse(diag, query, responseText, NewId()));
    }

    public Task<DiagnoseResponse> DiagnoseImageAsync(DiagnoseImageRequest request, CancellationToken ct = default)
    {
        var hint = !string.IsNullOrWhiteSpace(request.Description)
            ? request.Description
            : $"uploaded image {request.FileName}";
        var diag = BuildDiagnosis($"[Image analysis] {hint}");
        var responseText = BuildResponseText(diag, $"Image analysis: {hint}");
        return Task.FromResult(new DiagnoseResponse(diag, hint, responseText, NewId()));
    }

    public Task<TranscribeResponse> TranscribeAudioAsync(TranscribeAudioRequest request, CancellationToken ct = default)
    {
        // Mock: return a plausible transcript so the voice flow is demo-able.
        // In production this calls a speech-to-text provider.
        var transcript = MockTranscriptFor(request.FileName);
        return Task.FromResult(new TranscribeResponse(transcript, NewId()));
    }

    public Task<SpeakResponse> SpeakTextAsync(SpeakTextRequest request, CancellationToken ct = default)
    {
        // Mock: return silence WAV header + placeholder. In production, synthesize
        // via Azure Speech / OpenAI TTS / Polly and return the audio bytes.
        var wav = SilenceWav(request.Text.Length);
        return Task.FromResult(new SpeakResponse(
            Convert.ToBase64String(wav),
            "audio/wav",
            NewId()));
    }

    public Task<IReadOnlyList<VoiceDto>> GetVoicesAsync(CancellationToken ct = default)
    {
        var voices = new List<VoiceDto>
        {
            new("en-US-Neural", "Azure Neural (US English)", "en-US"),
            new("en-GB-Neural", "Azure Neural (UK English)", "en-GB"),
            new("es-ES-Neural", "Azure Neural (Spanish)", "es-ES"),
            new("fr-FR-Neural", "Azure Neural (French)", "fr-FR"),
            new("ar-SA-Neural", "Azure Neural (Arabic)", "ar-SA"),
        };
        return Task.FromResult((IReadOnlyList<VoiceDto>)voices);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static DiagnosticResultDto BuildDiagnosis(string query)
    {
        var q = query.ToLowerInvariant();

        if (ContainsAny(q, "brake", "squeal", "grind", "stop", "rotor", "pad"))
        {
            return new(
                "Brake Friction System Wear & Rotor Scoring",
                "High", "#dc2626", "$180 – $360", "1.2 – 2.0 hrs", false,
                "Avoid highway driving or heavy downhill loads. Brake stopping distance is degraded.",
                new List<ProbableCauseDto>
                {
                    new("Worn Ceramic / Semi-Metallic Brake Pads", "90%", "Wear indicator acoustic tab rubbing on the brake disc."),
                    new("Scored or Grooved Brake Rotors", "75%", "Deep heat grooves from prolonged metal-on-metal friction."),
                    new("Seized Caliper Slide Pins", "45%", "Uneven wear from dried grease in guide pins."),
                },
                new[]
                {
                    "Inspect pad friction material thickness (safe minimum is 3mm).",
                    "Measure rotor runout and minimum thickness with micrometer.",
                    "Flush & bleed DOT 4 brake fluid if moisture exceeds 3%.",
                },
                new[] { "Front Ceramic Brake Pad Set", "Vented Brake Discs (Pair)", "Synthetic Caliper Grease" });
        }

        if (ContainsAny(q, "heat", "steam", "temp", "coolant", "overheat", "radiator"))
        {
            return new(
                "Cooling System Thermal Failure",
                "Critical", "#dc2626", "$220 – $580", "2.0 – 3.5 hrs", false,
                "DO NOT DRIVE. Running an overheated engine risks catastrophic head gasket warp.",
                new List<ProbableCauseDto>
                {
                    new("Failed Thermostat in Closed Position", "85%", "Restricts coolant circulation from engine to radiator."),
                    new("Coolant Leak / Radiator Hose Rupture", "70%", "Loss of system pressure causing flash boiling."),
                    new("Water Pump Impeller Failure", "50%", "Bearing play or broken impeller prevents circulation."),
                },
                new[]
                {
                    "Cooling system pressure test to detect micro-cracks under 16 PSI.",
                    "Test thermostat opening temperature.",
                    "Block test for combustion gases in coolant (head gasket check).",
                },
                new[] { "OEM Thermostat Assembly & O-Ring", "Pre-mixed OAT Antifreeze Coolant (5L)", "Upper/Lower Radiator Hose" });
        }

        if (ContainsAny(q, "battery", "click", "alternator", "electric", "start", "crank", "dead"))
        {
            return new(
                "Starting & Charging Circuit Depletion",
                "Medium", "#f59e0b", "$120 – $280", "0.8 – 1.5 hrs", true,
                "Turn off A/C, radio, and high beams. Do not shut off engine until safely parked at the workshop.",
                new List<ProbableCauseDto>
                {
                    new("Degraded Battery Cell / Low Cold Cranking Amps", "85%", "Battery voltage drops below 9.6V under crank load."),
                    new("Alternator Diode or Voltage Regulator Fault", "60%", "Charging voltage below 13.8V while engine runs."),
                    new("Corroded Negative Battery Terminal Clamp", "40%", "High parasitic resistance preventing starter amperage."),
                },
                new[]
                {
                    "Digital conductance battery health and state-of-charge test.",
                    "Alternator ripple and charging load test with multimeter.",
                    "Clean battery terminal posts; apply anti-corrosion sealant.",
                },
                new[] { "AGM / EFB Heavy Duty 12V Battery", "Terminal Clamp Cleaner Kit" });
        }

        if (ContainsAny(q, "transmission", "gear", "slip", "shift", "atf"))
        {
            return new(
                "Transmission Hydraulic Pressure & Clutch Slippage",
                "High", "#dc2626", "$250 – $750", "2.5 – 5.0 hrs", false,
                "Drive with gentle throttle only. Avoid towing or steep hill climbs.",
                new List<ProbableCauseDto>
                {
                    new("Degraded / Low Automatic Transmission Fluid", "80%", "Inadequate hydraulic line pressure to lock clutch packs."),
                    new("Sticking Shift Solenoid Valve", "65%", "Fluid varnish restricts electromagnetic plunger motion."),
                    new("Torque Converter Clutch Lockup Failure", "40%", "Slip between engine output shaft and gearbox turbine."),
                },
                new[]
                {
                    "Check transmission fluid level and smell for burnt odor.",
                    "Scan TCM for trouble codes.",
                    "Execute transmission adaptation relearn procedure.",
                },
                new[] { "Full Synthetic ATF Fluid (6L)", "Transmission Filter & Pan Gasket" });
        }

        if (ContainsAny(q, "engine", "knock", "ping", "misfire", "oil", "pressure", "oil pressure"))
        {
            return new(
                "Engine Mechanical Anomaly Detection",
                "High", "#dc2626", "$200 – $600", "1.5 – 3.0 hrs", false,
                "Reduce speed and avoid high RPM. Low oil pressure can cause engine failure.",
                new List<ProbableCauseDto>
                {
                    new("Oil Pressure Sensor Drift", "75%", "Unmetered pressure reading or faulty sender unit."),
                    new("Worn Engine Bearings", "55%", "Excessive clearance causing metal-on-metal contact."),
                    new("Ignition Coil / Spark Plug Fouling", "50%", "Incomplete combustion causing intermittent misfire."),
                },
                new[]
                {
                    "Mechanical oil pressure test with gauge.",
                    "OBD-II live telemetry scan for misfire counters.",
                    "Chassis stethoscope test on vehicle lift.",
                },
                new[] { "Oil Pressure Sensor", "Engine Oil & Filter", "Ignition Coil Set" });
        }

        // Default comprehensive diagnosis
        return new(
            "Powertrain & Mechanical Anomaly Detection",
            "Medium", "#f59e0b", "$140 – $320", "1.0 – 2.0 hrs", true,
            "Vehicle can be driven with caution. Monitor instrument cluster for warning lamps.",
            new List<ProbableCauseDto>
            {
                new("Sensor Drift or Vacuum Leak", "75%", "Unmetered air or MAF/MAP sensor reading deviation."),
                new("Suspension Bushing / Ball Joint Wear", "60%", "Deteriorated rubber elastomer allowing metal contact."),
                new("Ignition Coil / Spark Plug Fouling", "50%", "Incomplete combustion causing intermittent misfire."),
            },
            new[]
            {
                "Complete 48-point OBD-II live telemetry scan.",
                "Smoke test intake manifold for micro-vacuum leaks.",
                "Chassis stethoscope test on vehicle lift.",
            },
            new[] { "Inspection Service & Diagnostic Report", "Replacement Sensor / Consumable Hardware" });
    }

    private static string BuildResponseText(DiagnosticResultDto d, string userQuery)
    {
        var safe = d.SafeToDrive ? "It's generally safe to drive with caution." : "Please do not drive the vehicle until inspected.";
        return
            $"Based on your description, I've identified the issue as {d.Title}. " +
            $"Urgency is {d.Urgency}. Estimated repair cost is {d.EstimatedCost} with {d.EstimatedLaborTime} of labor. " +
            $"{safe} " +
            $"The most likely cause is {d.LikelyCauses.FirstOrDefault()?.Name ?? "a mechanical issue"}. " +
            $"I recommend: {string.Join("; ", d.RecommendedActions.Take(2))}.";
    }

    private static string MockTranscriptFor(string fileName)
    {
        // Return a varied but deterministic transcript based on the filename hash.
        var seed = fileName.GetHashCode();
        var options = new[]
        {
            "I hear a grinding noise when I apply the brakes at low speed.",
            "My check engine light is on and the car is shaking at idle.",
            "The temperature gauge is in the red and there's steam under the hood.",
            "The car won't start — it just clicks when I turn the key.",
            "The transmission slips between second and third gear.",
            "There's a knocking sound from the front right when I go over bumps.",
        };
        return options[Math.Abs(seed) % options.Length];
    }

    private static byte[] SilenceWav(int textLength)
    {
        // Minimal valid WAV header + short silence so clients can play something.
        // Duration scales slightly with text length (mock speaking rate).
        var seconds = Math.Clamp(textLength / 15.0, 1.0, 30.0);
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var dataSize = (int)(seconds * byteRate);
        var fileSize = 36 + dataSize;

        using var stream = new MemoryStream(44 + dataSize);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(fileSize);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);                      // PCM chunk size
            writer.Write((short)1);                // PCM format
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write("data"u8);
            writer.Write(dataSize);
            writer.Write(new byte[dataSize]);      // silence
        }

        return stream.ToArray();
    }

    private static bool ContainsAny(string q, params string[] terms) => terms.Any(q.Contains);

    private static string NewId() => Guid.NewGuid().ToString("N")[..12];
}
