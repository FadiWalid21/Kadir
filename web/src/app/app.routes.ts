import { Routes } from '@angular/router';
import { devRoutes } from './dev/dev.routes';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home.page').then((m) => m.HomePage),
    pathMatch: 'full',
  },
  ...devRoutes,
  { path: '**', redirectTo: '' },
];
