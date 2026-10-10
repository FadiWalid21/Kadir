import { Routes } from '@angular/router';

export const devRoutes: Routes = [
  {
    path: 'dev/ui-kit',
    loadComponent: () =>
      import('./ui-kit/ui-kit.page').then((m) => m.UiKitPage),
  },
];
