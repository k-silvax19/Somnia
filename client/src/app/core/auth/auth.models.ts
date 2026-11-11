export interface Session {
  usuarioId: string;
  nome: string;
  email: string;
  accessToken: string;
  dataExpiracaoEmUtc: string;
  refreshExpiraEmUtc: string;
  csrfToken: string;
}

export interface Account {
  id: string;
  displayName: string;
  email: string;
  username: string | null;
  bio: string | null;
  avatarUrl: string | null;
}

export interface Credentials {
  email: string;
  password: string;
}

export interface Registration extends Credentials {
  displayName: string;
  username: string;
}
