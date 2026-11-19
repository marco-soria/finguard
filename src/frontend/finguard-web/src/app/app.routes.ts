import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login') },
  { path: '', canActivate: [authGuard], loadComponent: () => import('./features/home/home') },
  // Unknown URLs land on the guarded home route, so they need a session too.
  { path: '**', redirectTo: '' },
];
