import { ChangeDetectionStrategy, Component, OnInit, input, output, viewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { VehicleFieldsComponent, VehiclePayload } from './vehicle-fields';

export interface CustomerPayload {
  name: string;
  phoneNumber: string;
  email: string;
  /** Only in create mode: the API requires each new customer to arrive with a
   * first vehicle, so the create dialog embeds the car fields. */
  vehicle?: VehiclePayload;
}

/** Create/edit dialog for one customer. Vehicles besides the first live on the
 * customer detail screen, so edit mode shows people fields only. */
@Component({
  selector: 'app-customer-form',
  imports: [FormsModule, VehicleFieldsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overlay" (click)="closed.emit()">
      <div class="modal" (click)="$event.stopPropagation()">
        <h2>{{ mode() === 'create' ? 'New customer' : 'Edit customer' }}</h2>

        @if (mode() === 'create') {
          <p class="muted sm">
            A customer joins the book with their first car. More cars are added on the detail screen later.
          </p>
        } @else {
          <p class="muted sm">Email stays reachable and unique-checked on the server.</p>
        }

        <ng-content />

        @if (error()) {
          <p class="al al--err">{{ error() }}</p>
        }

        <form (ngSubmit)="submit()" novalidate>
          <label class="field">
            <span>Name</span>
            <input class="input" [ngModel]="name()" (ngModelChange)="name.set($event)" name="name" maxlength="120" required placeholder="Ahmed Khaled" />
          </label>

          <label class="field">
            <span>Phone</span>
            <input class="input" [ngModel]="phone()" (ngModelChange)="phone.set($event)" name="phone" maxlength="20" required placeholder="+20 123 456 7890" />
          </label>

          <label class="field">
            <span>Email</span>
            <input class="input" type="email" [ngModel]="email()" (ngModelChange)="email.set($event)" name="email" maxlength="150" required placeholder="ahmed@example.com" />
          </label>

          @if (mode() === 'create') {
            <h2 class="sectionHead">First vehicle</h2>
            <app-vehicle-fields />
          }

          <div class="modalActions">
            <button class="btn" type="button" (click)="closed.emit()">Cancel</button>
            <button class="btn btn--brand" type="submit" [disabled]="busy()">
              {{ busy() ? 'Saving…' : 'Save' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class CustomerForm implements OnInit {
  readonly mode = input<'create' | 'edit'>('edit');
  readonly initial = input<CustomerPayload | null>(null);
  readonly busy = input(false);
  readonly error = input<string | null>(null);

  readonly saved = output<CustomerPayload>();
  readonly closed = output<void>();

  // The vehicle fields are only rendered in create mode; reading the query in
  // create mode is safe (template guarantees presence).
  private readonly vehicleFields = viewChild(VehicleFieldsComponent);

  protected readonly name = signal('');
  protected readonly phone = signal('');
  protected readonly email = signal('');

  ngOnInit(): void {
    const existing = this.initial();

    if (existing) {
      this.name.set(existing.name);
      this.phone.set(existing.phoneNumber);
      this.email.set(existing.email);
    }
  }

  protected submit(): void {
    const payload: CustomerPayload = {
      name: this.name().trim(),
      phoneNumber: this.phone().trim(),
      email: this.email().trim(),
    };

    const vehicle = this.vehicleFields();

    if (this.mode() === 'create' && vehicle) {
      payload.vehicle = vehicle.payload();
    }

    this.saved.emit(payload);
  }
}
