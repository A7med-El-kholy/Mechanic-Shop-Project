import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
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
  private readonly router = inject(Router);

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

  /** The counter: full shop view (manager nav, booking, cancel/delete). */
  readonly isManager = computed(() =>
    (this.userSignal()?.roles ?? []).includes('Manager'));

  async login(email: string, password: string): Promise<void> {
    this.busySignal.set(true);

    try {
      const response = await firstValueFrom(this.http.post<AuthResponse>(
        '/api/auth/login',
        { email: email.trim(), password }));

      this.applySession(response);
    } finally {
      this.busySignal.set(false);
    }
  }

  /**
   * Public customer sign-up. The backend creates the login AND the customer
   * profile atomically, then we sign in with the same credentials so the user
   * lands straight in the portal instead of being bounced back to /login.
   */
  async register(
    fullName: string,
    email: string,
    phoneNumber: string,
    password: string): Promise<void> {
    this.busySignal.set(true);

    try {
      await firstValueFrom(this.http.post('/api/auth/register', {
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim(),
        password,
      }));

      const response = await firstValueFrom(this.http.post<AuthResponse>(
        '/api/auth/login',
        { email: email.trim(), password }));

      this.applySession(response);
    } finally {
      this.busySignal.set(false);
    }
  }

  /**
   * Completes a sign-in initiated by a third-party provider (Google callback).
   * The backend already issued the JWT and redirected us here with the token in
   * the URL fragment, so all that is left is to store the session and fetch the
   * user profile, then navigate to the original destination.
   */
  async startExternalSession(
    accessToken: string,
    expiresInSeconds: number,
    returnUrl: string,
  ): Promise<void> {
    this.busySignal.set(true);

    try {
      const expiresAt = String(Date.now() + expiresInSeconds * 1000);

      this.tokenSignal.set(accessToken);
      this.expiresSignal.set(expiresAt);
      localStorage.setItem(TOKEN_KEY, accessToken);
      localStorage.setItem(EXPIRES_KEY, expiresAt);

      const user = await firstValueFrom(
        this.http.get<AppUser>(`${environment.apiUrl}/api/auth/me`),
      );

      this.userSignal.set(user);
      localStorage.setItem(USER_KEY, JSON.stringify(user));
    } finally {
      this.busySignal.set(false);
    }

    await this.router.navigateByUrl(returnUrl);
  }

  private applySession(response: AuthResponse): void {
    this.tokenSignal.set(response.accessToken);
    this.userSignal.set(response.user);
    this.expiresSignal.set(String(Date.now() + response.expiresInSeconds * 1000));

    localStorage.setItem(TOKEN_KEY, response.accessToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    localStorage.setItem(EXPIRES_KEY, String(Date.now() + response.expiresInSeconds * 1000));
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
