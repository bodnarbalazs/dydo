import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// jsdom lacks the layout APIs React Flow measures with.
class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

class DOMMatrixReadOnlyStub {
  readonly m22: number;
  constructor(transform?: string) {
    const scale = /scale\(([\d.]+)\)/.exec(transform ?? '')?.[1];
    this.m22 = scale === undefined ? 1 : Number(scale);
  }
}

// jsdom has no matchMedia either: the OS scheme reads light unless a test stubs its own.
const lightOs = () => ({ matches: false, addEventListener: () => undefined, removeEventListener: () => undefined });

Object.assign(globalThis, { ResizeObserver: ResizeObserverStub, DOMMatrixReadOnly: DOMMatrixReadOnlyStub, matchMedia: lightOs });

afterEach(() => {
  cleanup();
});
