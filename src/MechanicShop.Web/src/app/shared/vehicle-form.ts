import { ChangeDetectionStrategy, Component, input, output, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { VehicleFieldsComponent, VehiclePayload } from './vehicle-fields';

/** Add/edit dialog for one vehicle. The owner is answered OUTSIDE this form:
 * customer-detail creates under its own customer, the Vehicles screen uses a
 * picker above the form — and an owner move does not exist (the API deliberately
 * has none). */
@Component({
  selector: 'app-vehicle-form',
  imports: [FormsModule, VehicleFieldsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overlay" (click)="closed.emit()">
      <div class="modal" (click)="$event.stopPropagation()">
        <h2>{{ initial() ? 'Edit vehicle' : 'Add vehicle' }}</h2>
        <p class="muted sm">
          @if (initial()) {
            Plate and VIN must stay unique across the shop.
          } @else {
            VIN is exactly 17 characters (A–H, J–N, P–R, Z and digits).
          }
        </p>

        <ng-content />

        @if (error()) {
          <p class="al al--err">{{ error() }}</p>
        }

        <form (ngSubmit)="submit()" novalidate>
          <app-vehicle-fields [initial]="initial()" />

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
export class VehicleForm {
  /** Existing vehicle (edit mode) or null (create). */
  readonly initial = input<VehiclePayload | null>(null);
  readonly busy = input(false);
  readonly error = input<string | null>(null);

  readonly saved = output<VehiclePayload>();
  readonly closed = output<void>();

  private readonly fields = viewChild.required(VehicleFieldsComponent);

  protected submit(): void {
    this.saved.emit(this.fields().payload());
  }
}
