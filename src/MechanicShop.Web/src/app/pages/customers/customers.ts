import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { Customer, PaginatedList } from '../../core/types';
import { CustomerForm, CustomerPayload } from '../../shared/customer-form';
import { problemMessage } from '../../core/api';

const PAGE_SIZE = 8;

/** The customers book. Search, page, and the two mutations; a customer's cars
 * live one click deeper (their details screen), which mirrors the API — a
 * customer without a booking is clean to delete, with one the server says 409. */
@Component({
  selector: 'app-customers',
  imports: [RouterLink, FormsModule, CustomerForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customers.html',
  styleUrl: './customers.scss',
})
export class CustomersPage {
  private readonly http = inject(HttpClient);

  protected readonly search = signal('');
  protected readonly page = signal(1);
  protected readonly data = signal<PaginatedList<Customer> | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);

  protected readonly formOpen = signal(false);
  protected readonly editing = signal<Customer | null>(null);
  protected readonly formBusy = signal(false);
  protected readonly formError = signal<string | null>(null);

  /** The editing customer as the form dialog wants it (null = create mode). */
  protected readonly editingPayload = computed<CustomerPayload | null>(() => {
    const editing = this.editing();

    return editing
      ? { name: editing.name, phoneNumber: editing.phoneNumber, email: editing.email }
      : null;
  });

  ngOnInit(): void {
    this.load();
  }

  // --- list ---------------------------------------------------------------

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    const params = new URLSearchParams({
      page: String(this.page()),
      pageSize: String(PAGE_SIZE),
    });

    const search = this.search().trim();

    if (search.length > 0) {
      params.set('search', search);
    }

    this.http
      .get<PaginatedList<Customer>>(`/api/customers?${params}`)
      .subscribe({
        next: data => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected startSearch(): void {
    this.page.set(1);
    this.load();
  }

  protected goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  // --- create / edit --------------------------------------------------------

  protected openCreate(): void {
    this.editing.set(null);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  protected openEdit(customer: Customer): void {
    this.editing.set(customer);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  protected closeForm(): void {
    this.formOpen.set(false);
  }

  protected save(payload: CustomerPayload): void {
    const editing = this.editing();
    this.formBusy.set(true);
    this.formError.set(null);

    // Create carries the required first vehicle; edit only rephrases the person.
    const request$ = editing
      ? this.http.put(`/api/customers/${editing.customerId}`, {
          name: payload.name,
          phoneNumber: payload.phoneNumber,
          email: payload.email,
        })
      : this.http.post('/api/customers', {
          name: payload.name,
          phoneNumber: payload.phoneNumber,
          email: payload.email,
          vehicles: payload.vehicle ? [payload.vehicle] : [],
        });

    request$.subscribe({
      next: () => {
        this.formBusy.set(false);
        this.formOpen.set(false);
        this.alert.set(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.formBusy.set(false);
        this.formError.set(problemMessage(error));
      },
    });
  }

  // --- delete ---------------------------------------------------------------

  protected remove(customer: Customer): void {
    if (!window.confirm(`Delete "${customer.name}"?`)) {
      return;
    }

    this.http.delete(`/api/customers/${customer.customerId}`).subscribe({
      next: () => {
        this.alert.set(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.alert.set(problemMessage(error));
      },
    });
  }
}
