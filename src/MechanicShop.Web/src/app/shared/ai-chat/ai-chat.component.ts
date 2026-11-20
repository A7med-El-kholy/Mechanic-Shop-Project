import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  ViewChild,
  AfterViewInit,
  OnDestroy,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AIService, ChatMessage, DiagnosticResult, Voice } from '../../core/ai.service';
import { AuthStore } from '../../core/auth-store';

/**
 * AI Master Diagnostics — multimodal assistant.
 * Supports: text chat, voice input (Web Speech API), voice output (TTS), image upload.
 */
@Component({
  selector: 'app-ai-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ai-chat.component.html',
  styleUrl: './ai-chat.component.scss',
})
export class AIChatComponent implements AfterViewInit, OnDestroy {
  private readonly ai = inject(AIService);
  protected readonly auth = inject(AuthStore);

  @ViewChild('streamContainer') private readonly streamContainer?: ElementRef<HTMLElement>;

  // ── UI state ─────────────────────────────────────────────────────────
  protected readonly analyzing = signal(false);
  protected readonly speaking = signal(false);
  protected readonly listening = signal(false);
  protected readonly voiceSupported = signal(false);
  protected readonly micSupported = signal(false);
  /** When on: speak the reply, then automatically reopen the mic for the next turn. */
  protected readonly handsFree = signal(false);
  protected readonly submitError = signal<string | null>(null);
  protected readonly bookingSuccess = signal(false);
  protected readonly bookingBusy = signal(false);

  protected readonly messages = signal<ChatMessage[]>([
    {
      sender: 'ai',
      text: "Hello! I'm your AI Master Technician. Type a symptom, speak into the mic, or upload a photo of a warning light, leak, or part.",
      time: this.now(),
      type: 'text',
    },
  ]);

  protected readonly latestDiagnostic = signal<DiagnosticResult | null>(null);

  // ── Voice (browser Web Speech API) ───────────────────────────────────
  private recognition: SpeechRecognition | null = null;
  private synth = window.speechSynthesis;
  private currentUtterance: SpeechSynthesisUtterance | null = null;
  private voices: Voice[] = [];
  private suppressResume = false;
  private resumeTimer: number | null = null;
  protected selectedVoiceId = signal<string>('');

  protected readonly prompt = signal('');
  protected readonly imageFile = signal<File | null>(null);
  protected readonly imagePreview = signal<string | null>(null);

  protected readonly quickSymptoms = [
    { label: 'Squealing / Grinding Brakes', query: 'High pitched squealing noise when applying brakes at low speed, pedal vibrates.' },
    { label: 'Engine Overheating & Steam', query: 'Temperature gauge in red zone, cooling fan running loudly, sweet smell of coolant under hood.' },
    { label: 'Check Engine Light', query: 'Check engine light blinking, engine shaking at idle, loss of acceleration power.' },
    { label: 'Battery / Electrical Drain', query: 'Car clicks rapidly but will not turn over, headlights dim, battery replaced 6 months ago.' },
    { label: 'Transmission Hard Shifting', query: 'Automatic gearbox delayed shifting into drive, slips between 2nd and 3rd gear.' },
    { label: 'Knocking over Bumps', query: 'Deep metallic clunk from front right wheel area over speed bumps or potholes.' },
  ];

  ngAfterViewInit(): void {
    this.detectVoiceSupport();
    this.loadVoices();
  }

  ngOnDestroy(): void {
    this.clearResumeTimer();
    this.stopListening();
    this.stopSpeaking();
  }

  private detectVoiceSupport(): void {
    this.micSupported.set(typeof window !== 'undefined' && 'webkitSpeechRecognition' in window);
    const SpeechRecognitionCtor =
      (window as unknown as { webkitSpeechRecognition?: new () => SpeechRecognition }).webkitSpeechRecognition;

    if (SpeechRecognitionCtor) {
      this.recognition = new SpeechRecognitionCtor();
      this.recognition.continuous = false;
      this.recognition.interimResults = false;
      this.recognition.lang = 'en-US';

      this.recognition.onstart = () => {
        this.listening.set(true);
        this.submitError.set(null);
      };

      this.recognition.onresult = (event: SpeechRecognitionEvent) => {
        let transcript = '';
        let isFinal = false;

        for (let i = event.resultIndex; i < event.results.length; i++) {
          transcript += event.results[i][0].transcript;
          isFinal = isFinal || event.results[i].isFinal;
        }

        this.prompt.set(transcript);

        // Hands-free: a finished sentence submits itself, then the answer is
        // spoken and the mic reopens. Manual mode keeps the text for editing.
        if (isFinal && this.handsFree() && transcript.trim()) {
          window.setTimeout(() => {
            if (!this.analyzing()) {
              void this.sendQuery();
            }
          }, 350);
        }
      };

      this.recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
        console.warn('Speech recognition error:', event.error);
        this.listening.set(false);
        if (event.error === 'not-allowed' || event.error === 'service-not-allowed') {
          this.submitError.set('Microphone access denied. Enable it in browser settings.');
        } else if (event.error !== 'no-speech') {
          this.submitError.set(`Voice error: ${event.error}`);
        }
      };

      this.recognition.onend = () => {
        this.listening.set(false);
      };

      this.voiceSupported.set(true);
    }
  }

  private loadVoices(): void {
    const applyBrowserVoices = (): boolean => {
      const browserVoices = this.synth?.getVoices() ?? [];

      if (browserVoices.length === 0) {
        return false;
      }

      this.voices = browserVoices.map((v, index) => ({
        id: `browser-${index}`,
        name: v.name,
        language: v.lang,
      }));

      // Prefer a natural US English voice, but never leave the picker empty.
      const preferred =
        browserVoices.find((v) => v.lang === 'en-US' && /natural|google|zira|aria|jenny/i.test(v.name)) ??
        browserVoices.find((v) => v.lang === 'en-US') ??
        browserVoices.find((v) => v.lang.startsWith('en')) ??
        browserVoices[0];

      this.selectedVoiceId.set(`browser-${browserVoices.indexOf(preferred)}`);

      return true;
    };

    // Chrome populates voices asynchronously. Listen first, then try immediately.
    if (this.synth) {
      this.synth.onvoiceschanged = () => {
        applyBrowserVoices();
      };
    }

    if (applyBrowserVoices()) {
      return;
    }

    // Last resort for browsers that expose no local voices: keep the API catalogue.
    void this.ai.getVoices()
      .then((list) => {
        if (this.voices.length === 0) {
          this.voices = list;
          this.selectedVoiceId.set(list[0]?.id ?? '');
        }
      })
      .catch(() => {
        /* no TTS voice available; the UI still works silently */
      });
  }

  private selectBrowserVoice(): SpeechSynthesisVoice | undefined {
    const browserVoices = this.synth?.getVoices() ?? [];
    const selected = this.selectedVoiceId();

    if (selected.startsWith('browser-')) {
      return browserVoices[Number(selected.slice('browser-'.length))];
    }

    const apiVoice = this.voices.find((v) => v.id === selected);

    return apiVoice
      ? browserVoices.find((v) => v.name === apiVoice.name)
      : undefined;
  }

  // ── Actions ──────────────────────────────────────────────────────────

  protected useSymptom(query: string): void {
    this.prompt.set(query);
    this.sendQuery();
  }

  protected onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type.toLowerCase()) ||
        !/\.(jpe?g|png|webp)$/i.test(file.name)) {
      this.submitError.set('Please upload a JPG, PNG, or WEBP image.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.submitError.set('Image must be under 10 MB.');
      return;
    }

    this.imageFile.set(file);
    const reader = new FileReader();
    reader.onload = () => {
      this.imagePreview.set(reader.result as string);
    };
    reader.readAsDataURL(file);
  }

  protected removeImage(): void {
    this.imageFile.set(null);
    this.imagePreview.set(null);
  }

  protected async sendQuery(): Promise<void> {
    const text = this.prompt().trim();
    if (!text && !this.imageFile()) return;
    if (this.analyzing()) return;

    this.submitError.set(null);
    this.bookingSuccess.set(false);

    const now = this.now();

    const userMessage: ChatMessage = {
      sender: 'user',
      text: text || '(image upload)',
      time: now,
      type: this.imageFile() ? 'image' : 'text',
    };
    this.messages.update((list) => [...list, userMessage]);
    this.prompt.set('');

    this.analyzing.set(true);
    this.scrollToBottom();

    try {
      let result;
      const file = this.imageFile();
      if (file) {
        result = await this.ai.diagnoseImage(file, text || undefined);
        this.imageFile.set(null);
        this.imagePreview.set(null);
      } else {
        result = await this.ai.diagnoseText(text);
      }

      this.latestDiagnostic.set(result.diagnosis);

      const aiReply: ChatMessage = {
        sender: 'ai',
        text: result.responseText,
        time: now,
        type: 'diagnostic',
        diagnostic: result.diagnosis,
      };
      this.messages.update((list) => [...list, aiReply]);

      // The diagnosis is complete. Speaking the verdict is a separate, slower
      // step and must not keep the "Analyzing…" indicator alive.
      this.analyzing.set(false);
      void this.speak(result.responseText, true);
      this.scrollToBottom();
    } catch (err: unknown) {
      if (!this.imageFile()) this.prompt.set(text);
      this.submitError.set(err instanceof Error ? err.message : 'Something went wrong.');
      console.error('AI query failed:', err);
    } finally {
      this.analyzing.set(false);
    }
  }

  protected toggleListening(): void {
    if (this.listening()) {
      this.stopListening();
      return;
    }
    if (!this.recognition) {
      this.submitError.set('Voice input not supported in this browser.');
      return;
    }
    this.submitError.set(null);
    try {
      this.recognition.start();
    } catch {
      this.submitError.set('Could not start voice recognition.');
    }
  }

  private stopListening(): void {
    try {
      this.recognition?.abort();
    } catch {
      /* ignore */
    }
    this.listening.set(false);
  }

  protected async speak(text: string, resumeListening = false): Promise<void> {
    this.stopSpeaking();
    this.suppressResume = false;
    this.speaking.set(true);

    return new Promise<void>((resolve) => {
      let settled = false;

      const finish = () => {
        if (settled) {
          return;
        }

        settled = true;
        this.speaking.set(false);
        this.currentUtterance = null;
        resolve();

        if (resumeListening && this.handsFree() && !this.suppressResume) {
          this.scheduleListening();
        }
      };

      try {
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = 'en-US';
        utterance.rate = 0.97;
        utterance.pitch = 1;

        const browserVoice = this.selectBrowserVoice();
        if (browserVoice) {
          utterance.voice = browserVoice;
          utterance.lang = browserVoice.lang;
        }

        this.currentUtterance = utterance;

        // Some browsers never fire `onend` (no voice installed, tab muted,
        // speech engine error). Never leave the UI stuck in "speaking".
        const timeoutId = window.setTimeout(finish, Math.min(45_000, Math.max(4_000, text.length * 90)));

        utterance.onstart = () => this.speaking.set(true);
        utterance.onend = () => {
          window.clearTimeout(timeoutId);
          finish();
        };
        utterance.onerror = () => {
          window.clearTimeout(timeoutId);
          finish();
        };

        this.synth.speak(utterance);
      } catch (err) {
        console.error('TTS failed:', err);
        finish();
      }
    });
  }

  protected toggleHandsFree(): void {
    const next = !this.handsFree();
    this.handsFree.set(next);

    if (!next) {
      this.suppressResume = true;
      this.clearResumeTimer();
      this.stopListening();
      return;
    }

    this.suppressResume = false;
    this.submitError.set(null);

    // Turning hands-free on should start the conversation immediately.
    if (this.recognition && !this.listening() && !this.analyzing() && !this.speaking()) {
      try {
        this.recognition.start();
      } catch {
        this.submitError.set('Could not start voice recognition.');
      }
    }
  }

  private scheduleListening(): void {
    if (!this.recognition || this.analyzing() || this.listening()) {
      return;
    }

    this.clearResumeTimer();
    this.resumeTimer = window.setTimeout(() => {
      this.resumeTimer = null;

      if (!this.handsFree() || this.analyzing() || this.listening() || this.speaking()) {
        return;
      }

      try {
        this.recognition?.start();
      } catch {
        /* recognition can reject a rapid restart; ignore and let the user tap */
      }
    }, 550);
  }

  private clearResumeTimer(): void {
    if (this.resumeTimer !== null) {
      window.clearTimeout(this.resumeTimer);
      this.resumeTimer = null;
    }
  }

  protected stopSpeaking(): void {
    this.suppressResume = true;
    this.clearResumeTimer();

    try {
      this.synth.cancel();
    } catch {
      /* ignore */
    }

    this.currentUtterance = null;
    this.speaking.set(false);
  }

  protected cancelAnalysis(): void {
    this.analyzing.set(false);
    this.submitError.set(null);
  }

  protected async bookPrioritySlot(): Promise<void> {
    const diag = this.latestDiagnostic();
    if (!diag) return;

    this.bookingBusy.set(true);
    this.submitError.set(null);

    try {
      const notes = [
        `[AI Diagnostic Triage: ${diag.title}]`,
        `Urgency: ${diag.urgency} | Est: ${diag.estimatedCost} (${diag.estimatedLaborTime})`,
        `Likely Causes: ${diag.likelyCauses.map((c) => c.name).join(', ')}`,
        `Client Symptoms: ${this.messages().filter((m) => m.sender === 'user').map((m) => m.text).join(' | ')}`,
        `Safe to Drive: ${diag.safeToDrive ? 'Yes' : 'No - Urgent attention recommended'}`,
      ].join('\n');

      await this.ai.post('/api/appointment-requests', {
        vehicleId: null,
        serviceTaskId: null,
        preferredDate: new Date().toISOString().slice(0, 10),
        notes,
        urgency: diag.urgency === 'Critical' || diag.urgency === 'High' ? 2 : diag.urgency === 'Medium' ? 1 : 0,
      });

      this.bookingSuccess.set(true);
      await this.speak('Your appointment request has been sent to the workshop. A manager will review it shortly.', true);
    } catch (err: unknown) {
      this.submitError.set(err instanceof Error ? err.message : 'Booking failed.');
    } finally {
      this.bookingBusy.set(false);
    }
  }

  protected clearError(): void {
    this.submitError.set(null);
  }

  private now(): string {
    return new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      this.streamContainer?.nativeElement.scrollTo({ top: this.streamContainer.nativeElement.scrollHeight, behavior: 'smooth' });
    }, 100);
  }

  protected renderText(text: string): string {
    return text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  }

  protected parseProbability(prob: string): number {
    const match = prob.match(/\d+/);
    return match ? Number(match[0]) : 0;
  }

  @HostListener('window:beforeunload')
  cleanup(): void {
    this.stopListening();
    this.stopSpeaking();
  }
}
