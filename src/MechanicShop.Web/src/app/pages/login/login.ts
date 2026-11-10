import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthStore } from '../../core/auth-store';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginPage {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly auth = inject(AuthStore);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly error = signal<string | null>(null);

  protected readonly returnUrl: string | null =
    this.route.snapshot.queryParamMap.get('returnUrl');

  protected async submit(): Promise<void> {
    this.error.set(null);

    try {
      await this.auth.login(this.email(), this.password());

      // Each role gets its own home; a returnUrl from a guard bounce still wins.
      const home = this.auth.isManager()
        ? '/dashboard'
        : this.auth.hasStaffRole() ? '/tech-home' : '/portal';

      await this.router.navigateByUrl(this.returnUrl ?? home);
    } catch (ex) {
      this.error.set(
        ex instanceof HttpErrorResponse && typeof ex.error?.detail === 'string'
          ? ex.error.detail
          : 'Sign-in failed. Try again.');
    }
  }
}
