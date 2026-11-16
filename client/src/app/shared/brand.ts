import { Component } from '@angular/core';

@Component({
  selector: 'app-brand',
  template: `<span class="brand"
    ><svg viewBox="0 0 48 48" aria-hidden="true">
      <path
        class="moon"
        d="M31 2c-8 1.5-13 7.8-13 16 0 8.4 4.9 14.6 13 17-11 .5-19-6.6-19-17C12 8.4 20 1.8 31 2Z"
      />
      <path d="M8 38v5M14 35v8M20 37v6M26 39v4M32 37v6" /></svg
    >somnia<span class="brand-dot">.</span></span
  >`,
  styles: `
    .brand {
      display: inline-flex;
      align-items: center;
      font-size: 1.65rem;
      font-weight: 800;
      letter-spacing: -0.08em;
      color: var(--text);
    }
    svg {
      width: 2.1rem;
      height: 2.1rem;
      margin-right: 0.55rem;
      fill: none;
      stroke: var(--accent);
      stroke-width: 3;
      stroke-linecap: round;
    }
    .moon {
      fill: var(--accent);
      stroke: none;
    }
    .brand-dot {
      color: var(--accent);
    }
  `,
})
export class Brand {}
