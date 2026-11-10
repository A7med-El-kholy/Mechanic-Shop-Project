import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Customer } from '../../core/types';
import { problemMessage } from '../../core/api';

interface AssistantQuestion {
  text: string;
  options: readonly string[];
}

const QUESTIONS: readonly AssistantQuestion[] = [
  { text: 'When does it happen?', options: ['When braking', 'While driving at speed', 'All the time'] },
  { text: 'How long has it been happening?', options: ['Started today', 'A few days', 'Weeks or more'] },
  { text: 'Any dashboard warning lights?', options: ['Yes', 'No', 'Not sure'] },
];

const URGENCY_LABELS = ['Low', 'Medium', 'High'] as const;
const URGENCY_COLORS = ['var(--ok)', 'var(--warn)', 'var(--danger)'] as const;

type AssistantStep = 0 | 1 | 2 | 3;

@Component({
  selector: 'app-portal-ai',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-ai.html',
  styleUrl: './portal-ai.scss',
})
export class PortalAiPage {
  private readonly http = inject(HttpClient);

  protected readonly step = signal<AssistantStep>(0);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly submitError = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly vehicles = signal<Customer['vehicles']>([]);
  protected readonly vehicleId = signal('');
  protected readonly report = signal('');
  protected readonly answers = signal<string[]>([]);

  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly currentQuestion = (): AssistantQuestion =>
    QUESTIONS[this.answers().length] ?? QUESTIONS[0];

  protected readonly answeredPairs = (): { question: string; answer: string }[] =>
    this.answers().map((answer, index) => ({ question: QUESTIONS[index].text, answer }));

  protected readonly triage = (): {
    braking: boolean;
    urgencyIndex: number;
    label: string;
    color: string;
    advice: string;
    areas: string;
    transcript: string;
  } => {
    const report = this.report().trim();
    const answers = this.answers();

    const braking = /brak/i.test(report) || answers[0] === 'When braking';
    const urgencyIndex =
      answers[0] === 'When braking' ? 2 : answers[2] === 'Yes' ? 1 : 0;

    const label = URGENCY_LABELS[urgencyIndex];
    const color = URGENCY_COLORS[urgencyIndex];

    const adviceMap: Record<string, string> = {
      High: 'Avoid long drives and book an inspection soon.',
      Medium: 'A warning light with these symptoms deserves a prompt check.',
      Low: 'Mention it at your next visit if it persists.',
    };

    const areasMap: Record<string, string> = {
      High: 'Brake pads or rotors, brake caliper hardware, suspension components.',
      Medium: 'Warning light system, affected sensors, related fluid levels.',
      Low: 'General inspection of the affected system at next service.',
    };

    const transcript = QUESTIONS.map(
      (q, i) => `${q.text} ${answers[i] ?? ''}`
    ).join(' · ');

    return {
      braking,
      urgencyIndex,
      label,
      color,
      advice: adviceMap[label as keyof typeof adviceMap],
      areas: braking ? (areasMap as Record<string, string>)['High'] : areasMap[label as keyof typeof areasMap],
      transcript,
    };
  };

  protected readonly chosenVehicle = (): Customer['vehicles'][number] | undefined =>
    this.vehicles().find((v) => v.vehicleId === this.vehicleId());

  ngOnInit(): void {
    this.http
      .get<Customer>('/api/customers/me')
      .subscribe({
        next: (profile) => {
          this.vehicles.set(profile.vehicles);
          if (profile.vehicles.length === 1) {
            this.vehicleId.set(profile.vehicles[0].vehicleId);
          }
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected continueToQuestions(): void {
    const report = this.report().trim();
    if (!report) {
      this.pageError.set('Describe the problem so the workshop can understand it.');
      return;
    }
    this.pageError.set(null);
    this.report.set(report);
    this.answers.set([]);
    this.step.set(1);
  }

  protected answer(option: string): void {
    this.answers.update((current) => [...current, option]);
    if (this.answers().length === QUESTIONS.length) {
      this.step.set(2);
    }
  }

  protected editDescription(): void {
    this.step.set(0);
  }

  protected submitForReview(): void {
    this.busy.set(true);
    this.submitError.set(null);

    const t = this.triage();
    const notes = [
      this.report().trim(),
      ...QUESTIONS.map((q, i) => `${q.text} ${this.answers()[i] ?? ''}`),
      `Areas a mechanic may check: ${t.areas}`,
    ].join('\n');

    this.http
      .post('/api/appointment-requests', {
        vehicleId: this.vehicleId(),
        serviceTaskId: null,
        preferredDate: this.today,
        notes,
        urgency: t.urgencyIndex,
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.step.set(3);
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.submitError.set(
            problemMessage(error) ?? 'The request could not be sent.'
          );
        },
      });
  }

  protected restart(): void {
    this.report.set('');
    this.answers.set([]);
    this.pageError.set(null);
    this.submitError.set(null);
    this.vehicleId.set(this.vehicles().length === 1 ? this.vehicleId() : '');
    this.step.set(0);
  }
}
