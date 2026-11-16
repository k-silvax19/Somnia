import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Somnia · Histórias através da música',
    loadComponent: () => import('./features/landing/landing-page').then((m) => m.LandingPage),
  },
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
        path: 'musica/:provider/:type/:externalId',
        title: 'Detalhes e avaliações · Somnia',
        loadComponent: () => import('./features/music/music-page').then((m) => m.MusicPage),
      },
      {
        path: 'inicio',
        title: 'Início · Somnia',
        loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
      },
      {
        path: 'conta',
        title: 'Sua conta · Somnia',
        data: { own: true },
        loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
      },
      {
        path: 'perfil/:username',
        title: 'Perfil · Somnia',
        loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
