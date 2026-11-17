import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  template: `
    @if (router.currentNavigation()) {
      <p class="navigation-status" role="status">Carregando…</p>
    }
    <router-outlet />
  `,
  styles: `
    .navigation-status {
      position: fixed;
      bottom: 1rem;
      right: 1rem;
      z-index: 10;
      background: var(--surface);
      color: var(--text);
      border: 1px solid var(--border);
      padding: 0.75rem 1rem;
      border-radius: 0.5rem;
      font-size: 0.85rem;
    }
  `,
})
export class App {
  protected readonly router = inject(Router);
}
