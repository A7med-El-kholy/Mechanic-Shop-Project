import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AuthStore } from '../../core/auth-store';

interface ParsedCallback {
  accessToken: string | null;
  expiresInSeconds: number;
  returnUrl: string;
}

/** Handles the browser redirect from the backend after a Google sign-in.
 * The backend put the JWT in the URL fragment (`#access_token=...&expires_in=...&returnUrl=...`).
 * We exchange it for a stored session, fetch the user profile, and navigate on. */
@Component({
  selector: 'app-auth-callback',
  templateUrl: './auth-callback.html',
  styleUrl: './auth-callback.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCallbackPage implements OnInit {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthStore);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.start();
  }

  private async start(): Promise<void> {
    const parsed = this.parseFragment();

    if (!parsed.accessToken) {
      this.error.set('Google sign-in did not return a token. Try signing in again.');
      await this.router.navigateByUrl('/login');
      return;
    }

    try {
      await this.auth.startExternalSession(
        parsed.accessToken,
        parsed.expiresInSeconds,
        parsed.returnUrl,
      );
    } catch {
      this.error.set('Could not complete the Google sign-in. Try again.');
      await this.router.navigateByUrl('/login');
    }
  }

  private parseFragment(): ParsedCallback {
    const hash = window.location.hash;
    const params = new URLSearchParams(hash.startsWith('#') ? hash.slice(1) : hash);
    const expires = Number(params.get('expires_in') ?? 0);
    const rawReturn = params.get('returnUrl') ?? '/portal';

    return {
      accessToken: params.get('access_token'),
      // Backend always sends a positive expiry, but fall back defensively.
      expiresInSeconds: Number.isFinite(expires) && expires > 0 ? expires : 60,
      // Guard against open redirects: only allow in-app relative paths.
      returnUrl: rawReturn.startsWith('/') && !rawReturn.startsWith('//') ? rawReturn : '/portal',
    };
  }
}
