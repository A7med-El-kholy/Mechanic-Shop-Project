import { ChangeDetectionStrategy, Component, OnInit, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface VehiclePayload {
  make: string;
  model: string;
  year: number;
  licensePlate: string;
  vin: string;
}

/** The domain's year rule and VIN regex, mirrored so the form rejects junk
 * before the API does. */
const YEAR_MIN = 1886;
const YEAR_MAX = new Date().getFullYear() + 1;
const VIN_PATTERN = '[A-HJ-NPR-Z0-9a-z]{17}';

/** The five vehicle input fields, embedded — the Vehicles screen's modal uses
 * them bare, the customer-create dialog drops them under the people fields
 * (the API requires one car at customer creation). */
@Component({
  selector: 'app-vehicle-fields',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="field">
      <span>Make</span>
      <input class="input" [ngModel]="make()" (ngModelChange)="make.set($event)" name="make" maxlength="100" required placeholder="Toyota" />
    </label>

    <label class="field">
      <span>Model</span>
      <input class="input" [ngModel]="model()" (ngModelChange)="model.set($event)" name="model" maxlength="100" required placeholder="Corolla" />
    </label>

    <label class="field">
      <span>Year</span>
      <input class="input" type="number" [ngModel]="year()" (ngModelChange)="year.set($event)" name="year" [min]="yearMin" [max]="yearMax" required placeholder="2024" />
    </label>

    <label class="field">
      <span>License plate</span>
      <input class="input" [ngModel]="plate()" (ngModelChange)="plate.set($event.toUpperCase())" name="plate" maxlength="20" required placeholder="ABC-1234" />
    </label>

    <label class="field">
      <span>VIN</span>
      <input class="input" [ngModel]="vin()" (ngModelChange)="vin.set($event.toUpperCase())" name="vin" maxlength="17" minlength="17" [pattern]="vinPattern" required placeholder="1HGCR2F59JA123456" />
    </label>
  `,
})
export class VehicleFieldsComponent implements OnInit {
  readonly initial = input<VehiclePayload | null>(null);

  protected readonly make = signal('');
  protected readonly model = signal('');
  protected readonly year = signal<number>(YEAR_MAX);
  protected readonly plate = signal('');
  protected readonly vin = signal('');

  protected readonly yearMin = YEAR_MIN;
  protected readonly yearMax = YEAR_MAX;
  protected readonly vinPattern = VIN_PATTERN;

  ngOnInit(): void {
    const existing = this.initial();

    if (existing) {
      this.make.set(existing.make);
      this.model.set(existing.model);
      this.year.set(existing.year);
      this.plate.set(existing.licensePlate);
      this.vin.set(existing.vin);
    }
  }

  /** Current field values, normalized the way the domain does. */
  payload(): VehiclePayload {
    return {
      make: this.make().trim(),
      model: this.model().trim(),
      year: Number(this.year()),
      licensePlate: this.plate().trim().toUpperCase(),
      vin: this.vin().trim().toUpperCase(),
    };
  }
}
