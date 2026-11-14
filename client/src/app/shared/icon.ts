import { Component, input } from '@angular/core';

const paths = {
  home: 'm3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-8H9v8H4a1 1 0 0 1-1-1Z',
  search: 'M21 21l-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
  user: 'M20 21v-2a7 7 0 0 0-14 0v2M17 6a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
  music: 'M9 18V5l12-3v13M9 18c0 4-7 5-7 1 0-3 7-4 7-1m12-3c0 4-7 5-7 1 0-3 7-4 7-1',
  info: 'M12 11v6m0-10v1M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
  menu: 'M3 6h18M3 12h18M3 18h18',
  exit: 'M9 3H4v18h5m6-14 5 5-5 5m-6-5h11',
  arrow: 'M4 12h16m-6-6 6 6-6 6',
  close: 'm6 6 12 12M6 18 18 6',
};

@Component({
  selector: 'app-icon',
  template: '<svg viewBox="0 0 24 24" aria-hidden="true"><path [attr.d]="paths[name()]" /></svg>',
  styles:
    ':host { display: inline-flex; width: 1.3rem; height: 1.3rem; flex-shrink: 0; } svg { width: 100%; height: 100%; fill: none; stroke: currentColor; stroke-width: 1.6; stroke-linecap: round; stroke-linejoin: round; }',
})
export class Icon {
  readonly name = input.required<keyof typeof paths>();
  protected readonly paths = paths;
}
