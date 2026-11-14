import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: 'entrar',
    title: 'Entrar · Somnia',
    canActivate: [guestGuard],
    data: { register: false },
    loadComponent: () => import('./features/auth/auth-page').then((m) => m.AuthPage),
  },
  {
    path: 'cadastro',
    title: 'Criar conta · Somnia',
    canActivate: [guestGuard],
    data: { register: true },
    loadComponent: () => import('./features/auth/auth-page').then((m) => m.AuthPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      {
        path: 'inicio',
        title: 'Início · Somnia',
        loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
      },
      {
        path: 'conta',
        title: 'Sua conta · Somnia',
        loadComponent: () => import('./features/account/account-page').then((m) => m.AccountPage),
      },
      { path: '', pathMatch: 'full', redirectTo: 'inicio' },
    ],
  },
  { path: '**', redirectTo: 'inicio' },
];
