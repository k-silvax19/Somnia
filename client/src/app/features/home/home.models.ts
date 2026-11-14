export interface Page<T> {
  items: T[];
  limit: number;
  offset: number;
  total: number;
  hasMore: boolean;
}

export interface MusicReference {
  provider: string;
  type: 'album' | 'track';
  externalId: string;
}

export interface Review {
  id: string;
  author: { username: string; displayName: string };
  item: MusicReference;
  score: number | null;
  review: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface MusicItem extends MusicReference {
  title: string;
  artists: string[];
  coverUrl: string | null;
  spotifyUrl: string | null;
}

export interface Connections {
  isFollowing: boolean;
  followersCount: number;
  followingCount: number;
}

export const musicKey = (item: MusicReference) =>
  `${item.provider}:${item.type}:${item.externalId}`;

export function coverUrl(value: string | null): string | null {
  try {
    const url = new URL(value ?? '');
    return url.protocol === 'https:' && !url.username && !url.password ? url.href : null;
  } catch {
    return null;
  }
}

export function spotifyUrl(item: MusicItem): string | null {
  try {
    const url = new URL(item.spotifyUrl ?? '');
    return url.origin === 'https://open.spotify.com' &&
      !url.username &&
      !url.password &&
      url.pathname === `/${item.type}/${item.externalId}`
      ? url.href
      : null;
  } catch {
    return null;
  }
}
