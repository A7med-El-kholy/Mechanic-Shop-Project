import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';
import { ToastService } from '../../core/toast.service';
import { Customer } from '../../core/types';

@Component({
  selector: 'app-portal-profile',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-profile.html',
  styleUrl: './portal-profile.scss',
})
export class PortalProfilePage {
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthStore);

  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly passwordSaving = signal(false);
  protected readonly profile = signal<Customer | null>(null);

  protected readonly name = signal('');
  protected readonly phoneNumber = signal('');
  protected readonly email = signal('');

  protected readonly currentPassword = signal('');
  protected readonly newPassword = signal('');
  protected readonly confirmPassword = signal('');

  protected readonly initials = computed(() => {
    const name = this.name().trim() || this.auth.fullName().trim();

    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  ngOnInit(): void {
    this.http.get<Customer>('/api/customers/me').subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.name.set(profile.name);
        this.phoneNumber.set(profile.phoneNumber);
        this.email.set(profile.email);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.pageError.set(problemMessage(error));
        this.loading.set(false);
      },
    });
  }

  protected saveProfile(): void {
    this.pageError.set(null);

    const name = this.name().trim();
    const phoneNumber = this.phoneNumber().replace(/[\s()-]/g, '');

    if (name.length < 2) {
      this.pageError.set('Enter your full name.');
      return;
    }

    if (!/^\+?\d{7,15}$/.test(phoneNumber)) {
      this.pageError.set('Enter a valid phone number (7–15 digits, optional +country code).');
      return;
    }

    this.saving.set(true);

    this.http.put<void>('/api/customers/me', { name, phoneNumber }).subscribe({
      next: () => {
        this.saving.set(false);
        this.phoneNumber.set(phoneNumber);
        this.profile.update((current) => current ? { ...current, name, phoneNumber } : current);
        this.toast.success('Profile updated.');
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.pageError.set(problemMessage(error));
      },
    });
  }

  protected changePassword(): void {
    this.pageError.set(null);

    if (!this.currentPassword()) {
      this.pageError.set('Enter your current password.');
      return;
    }

    if (!/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(this.newPassword())) {
      this.pageError.set('New password must be at least 8 characters and include upper case, lower case, a number and a symbol.');
      return;
    }

    if (this.newPassword() !== this.confirmPassword()) {
      this.pageError.set('The new passwords do not match.');
      return;
    }

    this.passwordSaving.set(true);

    this.http.post<void>('/api/auth/change-password', {
      currentPassword: this.currentPassword(),
      newPassword: this.newPassword(),
    }).subscribe({
      next: () => {
        this.passwordSaving.set(false);
        this.currentPassword.set('');
        this.newPassword.set('');
        this.confirmPassword.set('');
        this.toast.success('Password changed.');
      },
      error: (error: HttpErrorResponse) => {
        this.passwordSaving.set(false);
        this.pageError.set(problemMessage(error));
      },
    });
  }
}
