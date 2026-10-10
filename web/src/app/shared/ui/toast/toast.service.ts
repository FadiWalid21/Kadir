import { Injectable, signal } from '@angular/core';

export type ToastTone = 'info' | 'success' | 'danger';

export interface ToastMessage {
  id: number;
  text: string;
  tone: ToastTone;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly messagesSignal = signal<ToastMessage[]>([]);

  readonly messages = this.messagesSignal.asReadonly();

  show(text: string, tone: ToastTone = 'info', ttlMs = 4000): void {
    const id = this.nextId++;
    this.messagesSignal.update((list) => [...list, { id, text, tone }]);
    window.setTimeout(() => this.dismiss(id), ttlMs);
  }

  dismiss(id: number): void {
    this.messagesSignal.update((list) => list.filter((m) => m.id !== id));
  }
}
