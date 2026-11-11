import { Component } from '@angular/core';

@Component({
  selector: 'app-brand',
  template: `<span class="brand"
    ><svg viewBox="0 0 32 32" aria-hidden="true">
      <path d="M5 12v8M11 6v20M17 3v26M23 8v16M29 13v6" /></svg
    >Somnia<span class="brand-dot">.</span></span
  >`,
  styles: `
    .brand {
      display: inline-flex;
      align-items: center;
      font-size: 1.8rem;
      font-weight: 750;
      letter-spacing: -0.08rem;
      color: var(--text);
    }
    svg {
      width: 1.5rem;
      height: 1.5rem;
      margin-right: 0.7rem;
      fill: none;
      stroke: var(--accent);
      stroke-width: 2.5;
      stroke-linecap: round;
    }
    .brand-dot {
      color: var(--accent);
    }
  `,
})
export class Brand {}
