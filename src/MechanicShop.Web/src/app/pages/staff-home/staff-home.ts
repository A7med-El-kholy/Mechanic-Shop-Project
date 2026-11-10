import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';

import { AuthStore } from '../../core/auth-store';

/** The bare '' path sends each role to its own home: managers open the shop
 * dashboard, technicians their workspace. One component for two homes because a
 * static redirectTo cannot branch on who signed in. */
@Component({
  selector: 'app-staff-home',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffHomePage {
  constructor() {
    const auth = inject(AuthStore);
    const router = inject(Router);

    void router.navigate([auth.isManager() ? 'dashboard' : 'tech-home']);
  }
}
