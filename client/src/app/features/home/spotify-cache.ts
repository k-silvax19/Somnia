import { Injectable } from '@angular/core';
import type { MusicDetails } from '../music/music-api';
import { MusicItem, MusicReference, Page, coverUrl, musicKey } from './home.models';

const prefix = 'somnia:spotify-catalog:v1:';
const detailsLifetime = 6 * 60 * 60 * 1000;
const previewLifetime = 6 * 60 * 60 * 1000;
const searchLifetime = 10 * 60 * 1000;
const maxEntries = 50;
const maxEntryLength = 256_000;

type Stored<T> = { expiresAt: number; value: T };

function record(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function validItem(value: unknown, reference?: MusicReference): value is MusicItem {
  if (!record(value)) return false;
  const item = value as Partial<MusicItem>;
  if (item.provider !== 'spotify') return false;
  if (item.type !== 'album' && item.type !== 'track') return false;
  if (typeof item.externalId !== 'string' || !/^[a-zA-Z0-9]{22}$/.test(item.externalId))
    return false;
  if (
    reference &&
    (item.provider !== reference.provider ||
      item.type !== reference.type ||
      item.externalId !== reference.externalId)
  )
    return false;
  if (typeof item.title !== 'string' || !item.title.trim() || item.title.length > 512) return false;
  if (
    !Array.isArray(item.artists) ||
    item.artists.length > 30 ||
    !item.artists.every((artist) => typeof artist === 'string' && artist.length <= 256)
  )
    return false;
  if (item.coverUrl !== null && (typeof item.coverUrl !== 'string' || !coverUrl(item.coverUrl)))
    return false;
  return item.spotifyUrl === `https://open.spotify.com/${item.type}/${item.externalId}`;
}

function validDetails(value: unknown, reference: MusicReference): value is MusicDetails {
  if (!validItem(value, reference)) return false;
  const details = value as MusicDetails;
  if (reference.type === 'album') {
    return (
      Array.isArray(details.tracks) &&
      details.tracks.length <= 1000 &&
      details.tracks.every(
        (track) =>
          validItem(track) &&
          track.type === 'track' &&
          Number.isInteger(track.trackNumber) &&
          track.trackNumber >= 1 &&
          track.trackNumber <= 1000,
      )
    );
  }
  return (
    validItem(details.album) &&
    details.album.type === 'album' &&
    Number.isInteger(details.trackNumber) &&
    (details.trackNumber as number) >= 1 &&
    (details.trackNumber as number) <= 1000
  );
}

@Injectable({ providedIn: 'root' })
export class SpotifyCatalogCache {
  readDetails(reference: MusicReference): MusicDetails | null {
    return this.read(`details:${musicKey(reference)}`, (value) => validDetails(value, reference));
  }

  writeDetails(reference: MusicReference, value: MusicDetails): void {
    if (validDetails(value, reference))
      this.write(`details:${musicKey(reference)}`, value, detailsLifetime);
  }

  readCard(reference: MusicReference): MusicItem | null {
    const details = this.readDetails(reference);
    if (details) return details;
    return this.read(`preview:${musicKey(reference)}`, (value) => validItem(value, reference));
  }

  writePreview(reference: MusicReference, value: MusicItem): void {
    if (validItem(value, reference))
      this.write(`preview:${musicKey(reference)}`, value, previewLifetime);
  }

  readSearch(query: string, type: 'album' | 'track', offset: number): Page<MusicItem> | null {
    return this.read(this.searchKey(query, type, offset), (value): value is Page<MusicItem> => {
      if (!record(value)) return false;
      const page = value as Partial<Page<unknown>>;
      if (!Array.isArray(page.items)) return false;
      return (
        page.limit === 10 &&
        page.offset === offset &&
        Number.isInteger(page.total) &&
        (page.total as number) >= 0 &&
        typeof page.hasMore === 'boolean' &&
        page.items.length <= 10 &&
        page.items.every((item) => validItem(item) && item.type === type)
      );
    });
  }

  writeSearch(
    query: string,
    type: 'album' | 'track',
    offset: number,
    value: Page<MusicItem>,
  ): void {
    if (
      value &&
      Array.isArray(value.items) &&
      value.items.length &&
      value.items.length <= 10 &&
      value.items.every((item) => validItem(item) && item.type === type) &&
      value.limit === 10 &&
      value.offset === offset
    )
      this.write(this.searchKey(query, type, offset), value, searchLifetime);
  }

  private searchKey(query: string, type: 'album' | 'track', offset: number): string {
    return `search:${type}:${offset}:${query.trim()}`;
  }

  private read<T>(key: string, valid: (value: unknown) => value is T): T | null {
    try {
      const storage = globalThis.localStorage;
      const raw = storage.getItem(prefix + key);
      if (!raw) return null;
      if (raw.length > maxEntryLength) {
        storage.removeItem(prefix + key);
        return null;
      }
      const entry: unknown = JSON.parse(raw);
      const stored = record(entry) ? (entry as Partial<Stored<unknown>>) : null;
      const value = stored?.value;
      if (
        typeof stored?.expiresAt !== 'number' ||
        stored.expiresAt <= Date.now() ||
        stored.expiresAt > Date.now() + detailsLifetime ||
        !valid(value)
      ) {
        storage.removeItem(prefix + key);
        return null;
      }
      return value;
    } catch {
      return null;
    }
  }

  private write<T>(key: string, value: T, lifetime: number): void {
    const entry: Stored<T> = { expiresAt: Date.now() + lifetime, value };
    const raw = JSON.stringify(entry);
    if (raw.length > maxEntryLength) return;
    try {
      const storage = globalThis.localStorage;
      const keys = Object.keys(storage).filter((name) => name.startsWith(prefix));
      for (const name of keys) {
        try {
          const cached = JSON.parse(storage.getItem(name) ?? 'null') as Stored<unknown> | null;
          if (!cached || cached.expiresAt <= Date.now()) storage.removeItem(name);
        } catch {
          storage.removeItem(name);
        }
      }
      const current = Object.keys(storage).filter((name) => name.startsWith(prefix));
      if (current.length >= maxEntries && !storage.getItem(prefix + key))
        storage.removeItem(current[0]);
      storage.setItem(prefix + key, raw);
    } catch {
      // Storage can be disabled or full; catalog requests still work without it.
    }
  }
}
