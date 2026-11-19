import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login') },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/submissions/submissions-page'),
  },
  {
    path: 'submissions/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/submissions/submission-detail'),
  },
  // Unknown URLs land on the guarded submissions route, so they need a session too.
  { path: '**', redirectTo: '' },
];
