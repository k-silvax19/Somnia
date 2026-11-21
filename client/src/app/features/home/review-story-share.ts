import { Injectable } from '@angular/core';
import { MusicItem, Review, coverUrl } from './home.models';

export type ReviewStoryShareResult = 'shared' | 'downloaded' | 'cancelled';

const STORY_WIDTH = 1080;
const STORY_HEIGHT = 1920;
const STORY_BACKGROUND = '#141225';
const STORY_SURFACE = '#1e1a31';
const STORY_ACCENT = '#b49bfa';
const STORY_TEXT = '#f7f5ff';
const STORY_MUTED = '#ada4c9';

@Injectable({ providedIn: 'root' })
export class ReviewStoryShare {
  async share(review: Review, item: MusicItem | null): Promise<ReviewStoryShareResult> {
    const file = await this.createImage(review, item);
    const shareData: ShareData = {
      files: [file],
      title: item?.title || 'Avaliação musical no Somnia',
      text: item?.title ? `${item.title} · Avaliação no Somnia` : 'Minha avaliação musical no Somnia',
    };

    if (this.supportsFileShare(shareData)) {
      try {
        await navigator.share(shareData);
        return 'shared';
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') return 'cancelled';
      }
    }

    this.download(file);
    return 'downloaded';
  }

  private supportsFileShare(data: ShareData): boolean {
    try {
      return typeof navigator.share === 'function' &&
        typeof navigator.canShare === 'function' &&
        navigator.canShare({ files: data.files });
    } catch {
      return false;
    }
  }

  private async createImage(review: Review, item: MusicItem | null): Promise<File> {
    await document.fonts?.ready;
    const canvas = document.createElement('canvas');
    canvas.width = STORY_WIDTH;
    canvas.height = STORY_HEIGHT;
    const context = canvas.getContext('2d');
    if (!context) throw new Error('Canvas indisponível neste navegador.');

    this.drawBackground(context);
    const artwork = await this.loadArtwork(item?.coverUrl ?? null);
    this.drawArtwork(context, artwork);
    artwork?.close();
    this.drawReview(context, review, item);
    this.drawBrand(context);

    const blob = await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob((result) => result ? resolve(result) : reject(new Error('Não foi possível gerar o PNG.')), 'image/png');
    });
    return new File([blob], this.fileName(item?.title), { type: 'image/png' });
  }

  private drawBackground(context: CanvasRenderingContext2D): void {
    const gradient = context.createLinearGradient(0, 0, STORY_WIDTH, STORY_HEIGHT);
    gradient.addColorStop(0, '#151327');
    gradient.addColorStop(0.58, '#211b36');
    gradient.addColorStop(1, '#302647');
    context.fillStyle = gradient;
    context.fillRect(0, 0, STORY_WIDTH, STORY_HEIGHT);

    const glow = context.createRadialGradient(830, 210, 10, 830, 210, 760);
    glow.addColorStop(0, 'rgba(180,155,250,0.22)');
    glow.addColorStop(1, 'rgba(180,155,250,0)');
    context.fillStyle = glow;
    context.fillRect(0, 0, STORY_WIDTH, STORY_HEIGHT);

    this.roundedRect(context, 58, 64, 964, 1792, 46, STORY_SURFACE, 'rgba(180,155,250,0.34)');
  }

  private async loadArtwork(value: string | null): Promise<ImageBitmap | null> {
    const url = coverUrl(value);
    if (!url) return null;
    try {
      const response = await fetch(url, {
        mode: 'cors',
        credentials: 'omit',
        referrerPolicy: 'no-referrer',
      });
      if (!response.ok || !response.headers.get('content-type')?.startsWith('image/')) return null;
      const blob = await response.blob();
      if (blob.size > 8 * 1024 * 1024) return null;
      return await createImageBitmap(blob);
    } catch {
      // An unavailable catalog image should not block sharing the review itself.
      return null;
    }
  }

  private drawArtwork(context: CanvasRenderingContext2D, artwork: ImageBitmap | null): void {
    const x = 202;
    const y = 232;
    const size = 676;
    context.save();
    context.shadowColor = 'rgba(0,0,0,0.38)';
    context.shadowBlur = 42;
    context.shadowOffsetY = 18;
    this.roundedRect(context, x, y, size, size, 34, '#28223f');
    context.restore();

    context.save();
    this.clipRoundedRect(context, x, y, size, size, 34);
    if (artwork) {
      const scale = Math.max(size / artwork.width, size / artwork.height);
      const width = artwork.width * scale;
      const height = artwork.height * scale;
      context.drawImage(artwork, x + (size - width) / 2, y + (size - height) / 2, width, height);
    } else {
      const fallback = context.createLinearGradient(x, y, x + size, y + size);
      fallback.addColorStop(0, '#393052');
      fallback.addColorStop(1, '#211c35');
      context.fillStyle = fallback;
      context.fillRect(x, y, size, size);
      context.strokeStyle = 'rgba(180,155,250,0.72)';
      context.lineWidth = 6;
      context.beginPath();
      context.arc(STORY_WIDTH / 2, y + size / 2, 142, 0, Math.PI * 2);
      context.stroke();
      context.beginPath();
      context.arc(STORY_WIDTH / 2, y + size / 2, 18, 0, Math.PI * 2);
      context.fillStyle = STORY_ACCENT;
      context.fill();
      context.font = '600 96px Manrope, Segoe UI, sans-serif';
      context.fillStyle = STORY_TEXT;
      context.textAlign = 'center';
      context.fillText('♫', STORY_WIDTH / 2, y + size / 2 - 190);
    }
    context.restore();
  }

  private drawReview(context: CanvasRenderingContext2D, review: Review, item: MusicItem | null): void {
    context.textAlign = 'center';
    context.fillStyle = STORY_ACCENT;
    context.font = '750 25px Manrope, Segoe UI, sans-serif';
    context.letterSpacing = '5px';
    context.fillText('AVALIAÇÃO MUSICAL', STORY_WIDTH / 2, 166);
    context.letterSpacing = '0px';

    context.fillStyle = STORY_MUTED;
    context.font = '700 22px Manrope, Segoe UI, sans-serif';
    context.fillText(review.item.type === 'album' ? 'ÁLBUM' : 'FAIXA', STORY_WIDTH / 2, 970);

    context.fillStyle = STORY_ACCENT;
    if (review.score === null) {
      context.font = '750 58px Manrope, Segoe UI, sans-serif';
      context.fillText('SEM NOTA', STORY_WIDTH / 2, 1068);
    } else {
      context.font = '800 104px Manrope, Segoe UI, sans-serif';
      context.fillText(String(review.score), STORY_WIDTH / 2 - 36, 1080);
      context.textAlign = 'left';
      context.fillStyle = STORY_MUTED;
      context.font = '600 30px Manrope, Segoe UI, sans-serif';
      context.fillText('/ 100', STORY_WIDTH / 2 + 48, 1078);
      context.textAlign = 'center';
    }

    const title = item?.title || (review.item.type === 'album' ? 'Álbum' : 'Música');
    context.fillStyle = STORY_TEXT;
    context.font = '750 54px Manrope, Segoe UI, sans-serif';
    this.drawWrappedText(context, title, STORY_WIDTH / 2, 1180, 820, 64, 2);

    const artists = item?.artists.join(', ');
    if (artists) {
      context.fillStyle = STORY_MUTED;
      context.font = '500 29px Manrope, Segoe UI, sans-serif';
      this.drawWrappedText(context, artists, STORY_WIDTH / 2, 1320, 820, 38, 1);
    }

    const reviewText = review.review?.trim();
    if (reviewText) {
      context.fillStyle = 'rgba(247,245,255,0.9)';
      context.font = '400 31px Manrope, Segoe UI, sans-serif';
      this.drawWrappedText(context, `“${reviewText}”`, STORY_WIDTH / 2, 1415, 790, 45, 4);
    }
  }

  private drawBrand(context: CanvasRenderingContext2D): void {
    context.save();
    context.translate(280, 1705);
    context.scale(1.38, 1.38);
    context.fillStyle = STORY_ACCENT;
    context.fill(new Path2D('M31 2c-8 1.5-13 7.8-13 16 0 8.4 4.9 14.6 13 17-11 .5-19-6.6-19-17C12 8.4 20 1.8 31 2Z'));
    for (const [x, height] of [[8, 14], [14, 20], [20, 16], [26, 12], [32, 16]] as const) {
      context.beginPath();
      context.roundRect(x - 2, 40 - height / 2, 4, height, 2);
      context.fill();
    }
    context.restore();

    context.textAlign = 'left';
    context.textBaseline = 'middle';
    context.fillStyle = STORY_TEXT;
    context.font = '800 54px Manrope, Segoe UI, sans-serif';
    context.fillText('somnia', 354, 1736);
    context.fillStyle = STORY_ACCENT;
    context.fillText('.', 540, 1736);
    context.fillStyle = STORY_MUTED;
    context.font = '600 21px Manrope, Segoe UI, sans-serif';
    context.letterSpacing = '4px';
    context.fillText('SUA MÚSICA, SUA HISTÓRIA', 357, 1790);
    context.letterSpacing = '0px';
    context.textBaseline = 'alphabetic';
  }

  private drawWrappedText(
    context: CanvasRenderingContext2D,
    value: string,
    centerX: number,
    firstY: number,
    maxWidth: number,
    lineHeight: number,
    maxLines: number,
  ): void {
    const words = value.replace(/\s+/g, ' ').split(' ');
    const lines: string[] = [];
    let current = '';
    for (const word of words) {
      const candidate = current ? `${current} ${word}` : word;
      if (context.measureText(candidate).width <= maxWidth || !current) {
        current = candidate;
      } else {
        lines.push(current);
        current = word;
        if (lines.length === maxLines) break;
      }
    }
    if (current && lines.length < maxLines) lines.push(current);
    const consumed = lines.join(' ').length;
    if (consumed < value.length && lines.length) {
      let finalLine = lines[lines.length - 1];
      while (finalLine && context.measureText(`${finalLine}…`).width > maxWidth) {
        finalLine = finalLine.slice(0, -1);
      }
      lines[lines.length - 1] = `${finalLine.trimEnd()}…`;
    }
    lines.forEach((line, index) => context.fillText(line, centerX, firstY + index * lineHeight));
  }

  private roundedRect(
    context: CanvasRenderingContext2D,
    x: number,
    y: number,
    width: number,
    height: number,
    radius: number,
    fill: string,
    stroke?: string,
  ): void {
    context.beginPath();
    context.roundRect(x, y, width, height, radius);
    context.fillStyle = fill;
    context.fill();
    if (stroke) {
      context.strokeStyle = stroke;
      context.lineWidth = 2;
      context.stroke();
    }
  }

  private clipRoundedRect(
    context: CanvasRenderingContext2D,
    x: number,
    y: number,
    width: number,
    height: number,
    radius: number,
  ): void {
    context.beginPath();
    context.roundRect(x, y, width, height, radius);
    context.clip();
  }

  private fileName(title?: string): string {
    const slug = (title || 'avaliacao-musical')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '')
      .slice(0, 48);
    return `somnia-${slug || 'avaliacao'}.png`;
  }

  private download(file: File): void {
    const url = URL.createObjectURL(file);
    const link = document.createElement('a');
    link.href = url;
    link.download = file.name;
    document.body.append(link);
    link.click();
    link.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
  }
}
