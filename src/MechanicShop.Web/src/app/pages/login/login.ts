import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthStore } from '../../core/auth-store';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginPage {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly auth = inject(AuthStore);

  protected readonly email = signal('manager@mechanicshop.dev');
  protected readonly password = signal('Manager@Dev2026');
  protected readonly showPassword = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly returnUrl: string | null =
    this.route.snapshot.queryParamMap.get('returnUrl');

  protected fillCredentials(role: 'manager' | 'tech'): void {
    this.error.set(null);
    if (role === 'manager') {
      this.email.set('manager@mechanicshop.dev');
      this.password.set('Manager@Dev2026');
    } else {
      this.email.set('tech@mechanicshop.dev');
      this.password.set('Tech@Dev2026');
    }
  }

  protected togglePassword(): void {
    this.showPassword.update(s => !s);
  }

  protected async submit(): Promise<void> {
    this.error.set(null);

    const email = this.email().trim();
    const password = this.password().trim();

    if (!email || !password) {
      this.error.set('Please enter both email and password.');
      return;
    }

    try {
      await this.auth.login(email, password);

      // Each role gets its own home; a returnUrl from a guard bounce still wins.
      const home = this.auth.isManager()
        ? '/dashboard'
        : this.auth.hasStaffRole() ? '/tech-home' : '/portal';

      await this.router.navigateByUrl(this.returnUrl ?? home);
    } catch (ex) {
      this.error.set(
        ex instanceof HttpErrorResponse && typeof ex.error?.detail === 'string'
          ? ex.error.detail
          : 'Invalid credentials. If using Docker, check Manager@Dev2026.');
    }
  }
}
