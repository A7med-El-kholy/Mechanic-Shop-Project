import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.html',
  styleUrl: './register.scss',
})
export class RegisterPage {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthStore);

  protected readonly fullName = signal('');
  protected readonly email = signal('');
  protected readonly phoneNumber = signal('');
  protected readonly password = signal('');
  protected readonly confirmPassword = signal('');
  protected readonly showPassword = signal(false);
  protected readonly error = signal<string | null>(null);

  protected togglePassword(): void {
    this.showPassword.update((shown) => !shown);
  }

  protected async submit(): Promise<void> {
    this.error.set(null);

    const fullName = this.fullName().trim();
    const email = this.email().trim().toLowerCase();
    const phoneNumber = this.phoneNumber().replace(/[\s()-]/g, '');
    const password = this.password();
    const confirmPassword = this.confirmPassword();

    if (fullName.length < 2) {
      this.error.set('Enter your full name.');
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.error.set('Enter a valid email address.');
      return;
    }

    if (!/^\+?\d{7,15}$/.test(phoneNumber)) {
      this.error.set('Enter a valid phone number (7–15 digits, optional +country code).');
      return;
    }

    if (!/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(password)) {
      this.error.set('Password must be at least 8 characters and include upper case, lower case, a number and a symbol.');
      return;
    }

    if (password !== confirmPassword) {
      this.error.set('The two passwords do not match.');
      return;
    }

    try {
      await this.auth.register(fullName, email, phoneNumber, password);
      await this.router.navigateByUrl('/portal');
    } catch (ex) {
      this.error.set(
        ex instanceof HttpErrorResponse
          ? problemMessage(ex)
          : 'Could not create the account. Try again.');
    }
  }
}
