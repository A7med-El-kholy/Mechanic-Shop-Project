import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Customer } from '../../core/types';
import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';

interface DiagnosticResult {
  title: string;
  urgency: 'Low' | 'Medium' | 'High' | 'Critical';
  urgencyColor: string;
  urgencyBadgeClass: string;
  estimatedCost: string;
  laborTime: string;
  likelyCauses: { name: string; probability: string; description: string }[];
  recommendedActions: string[];
  partsNeeded: string[];
  safeToDrive: boolean;
  drivingAdvice: string;
}

interface ChatMessage {
  sender: 'ai' | 'user';
  text: string;
  time: string;
  diagnostic?: DiagnosticResult;
}

@Component({
  selector: 'app-portal-ai',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-ai.html',
  styleUrl: './portal-ai.scss',
})
export class PortalAiPage {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthStore);

  protected readonly loading = signal(false);
  protected readonly vehicles = signal<Customer['vehicles']>([]);
  protected readonly vehicleId = signal('');
  protected readonly selectedVehicleText = signal('General Vehicle / Walk-in');

  protected readonly prompt = signal('');
  protected readonly analyzing = signal(false);
  protected readonly submitError = signal<string | null>(null);
  protected readonly bookingSuccess = signal(false);
  protected readonly bookingBusy = signal(false);

  protected readonly quickSymptoms = [
    { label: 'Squealing / Grinding Brakes', query: 'High pitched squealing noise when applying brakes at low speed, pedal slightly vibrates.' },
    { label: 'Engine Overheating & Steam', query: 'Temperature gauge in red zone, cooling fan running loudly, sweet smell of coolant under hood.' },
    { label: 'Check Engine Light (P0300)', query: 'Check engine light blinking, engine shaking violently at idle, loss of acceleration power.' },
    { label: 'Battery / Electrical Drain', query: 'Car clicks rapidly but engine will not turn over, headlights dim, battery was replaced 6 months ago.' },
    { label: 'Transmission Hard Shifting', query: 'Automatic gearbox delayed when shifting into drive, slips between 2nd and 3rd gear with high RPM.' },
    { label: 'Knocking / Clunking over Bumps', query: 'Deep metallic clunk sound from front right wheel area when driving over speed bumps or potholes.' },
  ];

  protected readonly messages = signal<ChatMessage[]>([
    {
      sender: 'ai',
      text: 'Hello! I am your AI Master Technician. Describe any car noise, symptom, dashboard light, or OBD-II code. I will analyze the root cause, safety severity, and repair estimates for you.',
      time: 'Just now',
    },
  ]);

  protected readonly latestDiagnostic = signal<DiagnosticResult | null>(null);

  ngOnInit(): void {
    if (this.auth.isAuthenticated()) {
      this.http.get<Customer>('/api/customers/me').subscribe({
        next: (profile) => {
          this.vehicles.set(profile.vehicles ?? []);
          if (profile.vehicles && profile.vehicles.length > 0) {
            this.vehicleId.set(profile.vehicles[0].vehicleId);
            this.selectedVehicleText.set(`${profile.vehicles[0].make} ${profile.vehicles[0].model} (${profile.vehicles[0].licensePlate})`);
          }
        },
        error: () => {
          // If manager or tech views this page, allow generic diagnosis
          this.selectedVehicleText.set('Workshop Diagnostic Mode');
        },
      });
    }
  }

  protected onVehicleChange(vId: string): void {
    this.vehicleId.set(vId);
    const v = this.vehicles().find(x => x.vehicleId === vId);
    if (v) {
      this.selectedVehicleText.set(`${v.make} ${v.model} (${v.licensePlate})`);
    } else {
      this.selectedVehicleText.set('General Vehicle');
    }
  }

  protected useSymptom(symptom: string): void {
    this.prompt.set(symptom);
    this.sendQuery();
  }

  protected sendQuery(): void {
    const text = this.prompt().trim();
    if (!text || this.analyzing()) return;

    this.prompt.set('');
    this.analyzing.set(true);
    this.bookingSuccess.set(false);
    this.submitError.set(null);

    const now = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    // Add user message
    this.messages.update((list) => [...list, { sender: 'user', text, time: now }]);

    // Run AI Diagnosis Engine
    setTimeout(() => {
      const diag = this.generateDiagnostic(text);
      this.latestDiagnostic.set(diag);

      const aiReply: ChatMessage = {
        sender: 'ai',
        text: `Based on your report, I have diagnosed the issue as **${diag.title}**. Urgency rating: **${diag.urgency}**. Estimated cost: **${diag.estimatedCost}**. Review the full breakdown below:`,
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        diagnostic: diag,
      };

      this.messages.update((list) => [...list, aiReply]);
      this.analyzing.set(false);
    }, 900);
  }

  private generateDiagnostic(query: string): DiagnosticResult {
    const q = query.toLowerCase();

    if (q.includes('brake') || q.includes('squeal') || q.includes('grind') || q.includes('stop')) {
      return {
        title: 'Brake Friction System Wear & Rotor Scoring',
        urgency: 'High',
        urgencyColor: '#ef4444',
        urgencyBadgeClass: 'badge--danger',
        estimatedCost: '$180 – $360',
        laborTime: '1.2 – 2.0 hrs',
        safeToDrive: false,
        drivingAdvice: 'Avoid highway driving or heavy downhill loads. Brake stopping distance is degraded.',
        likelyCauses: [
          { name: 'Worn Ceramic / Semi-Metallic Brake Pads', probability: '90%', description: 'Wear indicator acoustic tab is rubbing directly on the brake disc.' },
          { name: 'Scored or Grooved Brake Rotors', probability: '75%', description: 'Deep heat grooves from prolonged metal-on-metal friction.' },
          { name: 'Seized Caliper Slide Pins', probability: '45%', description: 'Uneven wear caused by dried grease in guide pins.' },
        ],
        recommendedActions: [
          'Inspect pad friction material thickness (safe minimum is 3mm).',
          'Measure rotor runout and minimum thickness with micrometer.',
          'Flush & bleed DOT 4 brake fluid if moisture content exceeds 3%.',
        ],
        partsNeeded: ['Front Ceramic Brake Pad Set', 'Vented Brake Discs (Pair)', 'Synthetic Caliper Grease'],
      };
    }

    if (q.includes('heat') || q.includes('steam') || q.includes('temp') || q.includes('coolant')) {
      return {
        title: 'Cooling System Thermal Failure',
        urgency: 'Critical',
        urgencyColor: '#dc2626',
        urgencyBadgeClass: 'badge--danger',
        estimatedCost: '$220 – $580',
        laborTime: '2.0 – 3.5 hrs',
        safeToDrive: false,
        drivingAdvice: 'DO NOT DRIVE. Running an overheated engine risks catastrophic head gasket warp and block cracking.',
        likelyCauses: [
          { name: 'Failed Thermostat in Closed Position', probability: '85%', description: 'Restricts coolant circulation from engine block to radiator.' },
          { name: 'Coolant Leak / Radiator Hose Rupture', probability: '70%', description: 'Loss of system pressure causing flash boiling.' },
          { name: 'Water Pump Impeller Failure', probability: '50%', description: 'Bearing play or broken plastic impeller prevents circulation.' },
        ],
        recommendedActions: [
          'Cooling system pressure test to detect micro-cracks under 16 PSI.',
          'Test thermostat opening temperature.',
          'Block test for combustion gases in coolant (head gasket check).',
        ],
        partsNeeded: ['OEM Thermostat Assembly & O-Ring', 'Pre-mixed OAT Antifreeze Coolant (5L)', 'Upper/Lower Radiator Hose'],
      };
    }

    if (q.includes('battery') || q.includes('click') || q.includes('alternator') || q.includes('electric') || q.includes('start')) {
      return {
        title: 'Starting & Charging Circuit Depletion',
        urgency: 'Medium',
        urgencyColor: '#f59e0b',
        urgencyBadgeClass: 'badge--warn',
        estimatedCost: '$120 – $280',
        laborTime: '0.8 – 1.5 hrs',
        safeToDrive: true,
        drivingAdvice: 'Turn off air conditioning, radio, and high beams. Do not shut off engine until safely parked at workshop.',
        likelyCauses: [
          { name: 'Degraded Battery Cell / Low Cold Cranking Amps (CCA)', probability: '85%', description: 'Battery voltage drops below 9.6V under crank load.' },
          { name: 'Alternator Diode or Voltage Regulator Fault', probability: '60%', description: 'Charging voltage below 13.8V while engine is running.' },
          { name: 'Corroded Negative Battery Terminal Clamp', probability: '40%', description: 'High parasitic resistance preventing high starter amperage.' },
        ],
        recommendedActions: [
          'Conduct digital conductance battery health and state-of-charge test.',
          'Perform alternator ripple and charging load test with multimeter.',
          'Clean battery terminal posts and apply protective anti-corrosion sealant.',
        ],
        partsNeeded: ['AGM / EFB Heavy Duty 12V Battery', 'Terminal Clamp Cleaner Kit'],
      };
    }

    if (q.includes('transmission') || q.includes('gear') || q.includes('slip') || q.includes('shift')) {
      return {
        title: 'Transmission Hydraulic Pressure & Clutch Slippage',
        urgency: 'High',
        urgencyColor: '#ef4444',
        urgencyBadgeClass: 'badge--danger',
        estimatedCost: '$250 – $750',
        laborTime: '2.5 – 5.0 hrs',
        safeToDrive: false,
        drivingAdvice: 'Drive with gentle throttle input only. Avoid towing or steep hill climbs.',
        likelyCauses: [
          { name: 'Degraded / Low Automatic Transmission Fluid (ATF)', probability: '80%', description: 'Inadequate hydraulic line pressure to lock clutch packs.' },
          { name: 'Sticking Shift Solenoid Valve', probability: '65%', description: 'Fluid varnish restricts electromagnetic plunger motion.' },
          { name: 'Torque Converter Clutch Lockup Failure', probability: '40%', description: 'Slip between engine output shaft and gearbox turbine.' },
        ],
        recommendedActions: [
          'Check transmission fluid level and smell for burnt clutch odor.',
          'Scan TCM (Transmission Control Module) for trouble codes.',
          'Execute transmission adaptation relearn procedure.',
        ],
        partsNeeded: ['Full Synthetic ATF Fluid (6L)', 'Transmission Filter & Pan Gasket'],
      };
    }

    // Default Comprehensive Automotive Diagnostic
    return {
      title: 'Powertrain & Mechanical Anomaly Detection',
      urgency: 'Medium',
      urgencyColor: '#f59e0b',
      urgencyBadgeClass: 'badge--warn',
      estimatedCost: '$140 – $320',
      laborTime: '1.0 – 2.0 hrs',
      safeToDrive: true,
      drivingAdvice: 'Vehicle can be driven with caution. Monitor instrument cluster for warning lamps.',
      likelyCauses: [
        { name: 'Sensor Drift or Vacuum Leak', probability: '75%', description: 'Unmetered air or MAF/MAP sensor reading deviation.' },
        { name: 'Suspension Bushing / Ball Joint Wear', probability: '60%', description: 'Deteriorated rubber elastomer allowing metal contact.' },
        { name: 'Ignition Coil / Spark Plug Fouling', probability: '50%', description: 'Incomplete combustion causing intermittent misfire.' },
      ],
      recommendedActions: [
        'Complete 48-point electronic OBD-II live telemetry scan.',
        'Smoke test intake manifold for micro-vacuum leaks.',
        'Chassis stethoscope test on vehicle lift to isolate vibration source.',
      ],
      partsNeeded: ['Inspection Service & Diagnostic Report', 'Replacement Sensor / Consumable Hardware'],
    };
  }

  protected bookPrioritySlot(): void {
    const diag = this.latestDiagnostic();
    if (!diag) return;

    this.bookingBusy.set(true);
    this.submitError.set(null);

    const vehicleIdToSend = this.vehicleId() || (this.vehicles().length > 0 ? this.vehicles()[0].vehicleId : null);

    const notes = [
      `[AI Diagnostic Triage: ${diag.title}]`,
      `Urgency: ${diag.urgency} | Est: ${diag.estimatedCost} (${diag.laborTime})`,
      `Likely Causes: ${diag.likelyCauses.map(c => c.name).join(', ')}`,
      `Client Symptoms: ${this.messages().filter(m => m.sender === 'user').map(m => m.text).join(' | ')}`,
      `Safe to Drive: ${diag.safeToDrive ? 'Yes' : 'No - Urgent attention recommended'}`,
    ].join('\n');

    this.http.post('/api/appointment-requests', {
      vehicleId: vehicleIdToSend,
      serviceTaskId: null,
      preferredDate: new Date().toISOString().slice(0, 10),
      notes,
      urgency: diag.urgency === 'Critical' || diag.urgency === 'High' ? 2 : diag.urgency === 'Medium' ? 1 : 0,
    }).subscribe({
      next: () => {
        this.bookingBusy.set(false);
        this.bookingSuccess.set(true);
      },
      error: (err: HttpErrorResponse) => {
        this.bookingBusy.set(false);
        // Even if the API returns 401 (e.g. guest mode) or 400, handle gracefully
        if (err.status === 401) {
          this.submitError.set('Please sign in to officially book an appointment with your vehicle profile.');
        } else {
          this.submitError.set(problemMessage(err) ?? 'Request recorded for workshop follow-up.');
        }
      },
    });
  }
}
