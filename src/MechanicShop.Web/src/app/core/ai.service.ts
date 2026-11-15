import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';

export interface Voice {
  id: string;
  name: string;
  language: string;
}

export interface ProbableCause {
  name: string;
  probabilityPercent: string;
  description: string;
}

export interface DiagnosticResult {
  title: string;
  urgency: 'Low' | 'Medium' | 'High' | 'Critical';
  urgencyColor: string;
  estimatedCost: string;
  estimatedLaborTime: string;
  safeToDrive: boolean;
  drivingAdvice: string;
  likelyCauses: ProbableCause[];
  recommendedActions: string[];
  partsNeeded: string[];
}

export interface DiagnoseResponse {
  diagnosis: DiagnosticResult;
  transcript: string;
  responseText: string;
  requestId: string;
}

export interface TranscribeResponse {
  transcript: string;
  requestId: string;
}

export interface SpeakResponse {
  audioBase64: string;
  contentType: string;
  requestId: string;
}

export interface ChatMessage {
  sender: 'ai' | 'user';
  text: string;
  time: string;
  diagnostic?: DiagnosticResult;
  audioUrl?: string;
  type: 'text' | 'voice' | 'image' | 'diagnostic';
}

@Injectable({ providedIn: 'root' })
export class AIService {
  protected readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl + '/api/ai';

  /** Text-based diagnosis. */
  async diagnoseText(description: string, vehicleId?: string, language = 'en-US'): Promise<DiagnoseResponse> {
    return firstValueFrom(
      this.http.post<DiagnoseResponse>(`${this.baseUrl}/diagnose`, {
        description,
        vehicleId,
        language,
      }),
    );
  }

  /** Image-based diagnosis. */
  async diagnoseImage(file: File, description?: string, vehicleId?: string): Promise<DiagnoseResponse> {
    const form = new FormData();
    form.append('image', file);
    if (description) form.append('description', description);
    if (vehicleId) form.append('vehicleId', vehicleId);

    return firstValueFrom(
      this.http.post<DiagnoseResponse>(`${this.baseUrl}/diagnose-image`, form),
    );
  }

  /** Speech-to-text. */
  async transcribeAudio(file: File): Promise<TranscribeResponse> {
    const form = new FormData();
    form.append('audio', file);

    return firstValueFrom(
      this.http.post<TranscribeResponse>(`${this.baseUrl}/transcribe`, form),
    );
  }

  /** Text-to-speech. Returns base64 audio. */
  async speakText(text: string, voice?: string, language = 'en-US'): Promise<SpeakResponse> {
    return firstValueFrom(
      this.http.post<SpeakResponse>(`${this.baseUrl}/speak`, {
        text,
        voice,
        language,
      }),
    );
  }

  /** Available TTS voices. */
  async getVoices(): Promise<Voice[]> {
    return firstValueFrom(this.http.get<Voice[]>(`${this.baseUrl}/voices`));
  }

  /** Generic POST helper for booking etc. */
  post<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.post<T>(`${environment.apiUrl}${path}`, body));
  }
}
