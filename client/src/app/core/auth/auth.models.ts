export interface Session {
  usuarioId: string;
  nome: string;
  email: string;
  accessToken: string;
  dataExpiracaoEmUtc: string;
  refreshExpiraEmUtc: string;
  csrfToken: string;
  lembrarLogin?: boolean;
}

export interface Account {
  id: string;
  displayName: string;
  email: string;
  username: string | null;
  bio: string | null;
  avatarUrl: string | null;
  bannerTheme: 'midnight' | 'aurora' | 'mist';
}

export interface Credentials {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface Registration extends Credentials {
  displayName: string;
  username: string;
}
