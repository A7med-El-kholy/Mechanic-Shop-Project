import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-not-yet',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      place-items: center;
      padding: 40px 12px;
      min-height: 70%;
      text-align: center;
    }

    .card {
      max-width: 460px;
    }
  `,
  template: `
    <div class="card">
      <h1>This tab arrives with a later stage</h1>
      <p class="muted">
        The staff app ships screen by screen: customers & vehicles, catalog, work orders and the
        schedule are being built first; invoices, reports and notifications follow once their API
        stages land. The routes are already wired, so nothing needs re-navigation later.
      </p>
      <p class="muted sm">Roadmap: Angular staff UI → customer portal → invoices & payments →
        inventory → AI assistant.</p>
    </div>
  `,
})
export class NotYetPage { }
