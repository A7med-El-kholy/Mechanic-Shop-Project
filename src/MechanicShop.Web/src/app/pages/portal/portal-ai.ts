import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Customer } from '../../core/types';
import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';
import { AIService } from '../../core/ai.service';

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
  private readonly ai = inject(AIService);
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

  protected async sendQuery(): Promise<void> {
    const text = this.prompt().trim();
    if (!text || this.analyzing()) return;

    this.prompt.set('');
    this.analyzing.set(true);
    this.bookingSuccess.set(false);
    this.submitError.set(null);

    const now = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    // Add user message
    this.messages.update((list) => [...list, { sender: 'user', text, time: now }]);

    try {
      const result = await this.ai.diagnoseText(text, this.vehicleId() || undefined);
      const diag: DiagnosticResult = {
        ...result.diagnosis,
        urgencyBadgeClass: ['High', 'Critical'].includes(result.diagnosis.urgency) ? 'badge--danger' : 'badge--warn',
        laborTime: result.diagnosis.estimatedLaborTime,
        likelyCauses: result.diagnosis.likelyCauses.map(c => ({ ...c, probability: c.probabilityPercent })),
      };
      this.latestDiagnostic.set(diag);
      this.messages.update(list => [...list, {
        sender: 'ai', text: result.responseText,
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }), diagnostic: diag,
      }]);
    } catch (err) {
      this.prompt.set(text);
      this.submitError.set(err instanceof Error ? err.message : 'Diagnosis failed. Please try again.');
    } finally {
      this.analyzing.set(false);
    }
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
