import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Account } from '../../core/auth/auth.models';
import { environment } from '../../environments/environment';
import { Connections, Page, Review } from '../home/home.models';

export interface PublicProfile {
  username: string;
  displayName: string;
  bio: string | null;
  avatarUrl: string | null;
}

export interface RelatedProfile extends PublicProfile {
  followedAt: string;
}

export interface AvatarRequest {
  id: string;
  status: 'pending' | 'completed' | 'rejected' | 'cancelled' | 'failed';
  erro: string | null;
}

@Injectable()
export class ProfileApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;
  private readonly write = { headers: { 'X-Somnia-Client': 'web' }, timeout: 15_000 };
  private user(username: string) {
    return `${this.base}/users/${encodeURIComponent(username)}`;
  }

  me() {
    return this.http.get<Account>(`${this.base}/me`, { timeout: 15_000 });
  }
  profile(username: string) {
    return this.http.get<PublicProfile>(this.user(username), { timeout: 15_000 });
  }
  connections(username: string) {
    return this.http.get<Connections>(`${this.user(username)}/connections`, { timeout: 15_000 });
  }
  reviews(username: string, offset: number) {
    return this.http.get<Page<Review>>(`${this.user(username)}/reviews`, {
      params: { limit: 10, offset },
      timeout: 15_000,
    });
  }
  related(username: string, kind: 'followers' | 'following', offset: number) {
    return this.http.get<Page<RelatedProfile>>(`${this.user(username)}/${kind}`, {
      params: { limit: 10, offset },
      timeout: 15_000,
    });
  }
  follow(username: string, value: boolean) {
    const url = `${this.user(username)}/follow`;
    return value
      ? this.http.put<void>(url, null, this.write)
      : this.http.delete<void>(url, this.write);
  }
  edit(displayName: string, bio: string | null) {
    return this.http.patch<Account>(`${this.base}/me/profile`, { displayName, bio }, this.write);
  }
  avatar(username: string) {
    return this.http.get(`${this.user(username)}/avatar`, {
      responseType: 'blob',
      timeout: 15_000,
    });
  }
  upload(file: File) {
    const data = new FormData();
    data.append('file', file);
    return this.http.put<AvatarRequest>(`${this.base}/me/avatar`, data, this.write);
  }
  avatarStatus(id: string) {
    return this.http.get<AvatarRequest>(
      `${this.base}/me/avatar/requests/${encodeURIComponent(id)}`,
      {
        timeout: 15_000,
      },
    );
  }
  removeAvatar() {
    return this.http.delete<void>(`${this.base}/me/avatar`, this.write);
  }
}
