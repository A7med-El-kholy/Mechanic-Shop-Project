import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AppUser, AuthResponse } from './types';

const TOKEN_KEY = 'shop.token';
const USER_KEY = 'shop.user';
const EXPIRES_KEY = 'shop.expiresAt';

/** True while a token's expiry timestamp (epoch ms) is in the future. */
function notExpired(expiresAt: string | null): boolean {
  if (expiresAt === null) {
    return false;
  }

  const epoch = Number(expiresAt);

  return Number.isFinite(epoch) && epoch > Date.now();
}

function readUser(): AppUser | null {
  const raw = localStorage.getItem(USER_KEY);

  if (raw === null) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as AppUser;

    return parsed && typeof parsed.email === 'string' && Array.isArray(parsed.roles) ? parsed : null;
  } catch {
    return null;
  }
}

/**
 * The single owner of "who is signed in": token, expiry and user live here, in
 * localStorage (60-minute sessions by design; no refresh tokens yet), exposed
 * as signals so zoneless change detection just works.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly http = inject(HttpClient);

  private readonly busySignal = signal(false);
  readonly busy = this.busySignal.asReadonly();

  private readonly tokenSignal = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  readonly token = this.tokenSignal.asReadonly();

  private readonly userSignal = signal<AppUser | null>(readUser());
  readonly user = this.userSignal.asReadonly();

  private readonly expiresSignal = signal<string | null>(localStorage.getItem(EXPIRES_KEY));

  readonly isAuthenticated = computed(() =>
    this.tokenSignal() !== null &&
    this.userSignal() !== null &&
    notExpired(this.expiresSignal()));

  readonly primaryRole = computed(() => this.userSignal()?.roles[0] ?? null);

  readonly fullName = computed(() => this.userSignal()?.fullName ?? '');

  /** Staff = the workshop side; customers get the portal shell instead. */
  readonly hasStaffRole = computed(() => {
    const roles = this.userSignal()?.roles ?? [];

    return roles.includes('Manager') || roles.includes('Technician');
  });

  async login(email: string, password: string): Promise<void> {
    this.busySignal.set(true);

    try {
      const response = await firstValueFrom(this.http.post<AuthResponse>(
        '/api/auth/login',
        { email: email.trim(), password }));

      this.tokenSignal.set(response.accessToken);
      this.userSignal.set(response.user);
      this.expiresSignal.set(String(Date.now() + response.expiresInSeconds * 1000));

      localStorage.setItem(TOKEN_KEY, response.accessToken);
      localStorage.setItem(USER_KEY, JSON.stringify(response.user));
      localStorage.setItem(EXPIRES_KEY, String(Date.now() + response.expiresInSeconds * 1000));
    } finally {
      this.busySignal.set(false);
    }
  }

  logout(): void {
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.expiresSignal.set(null);

    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(EXPIRES_KEY);
  }
}
