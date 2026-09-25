import { act } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';

const elk = { layout: vi.fn() };

vi.mock('./layout/layout', () => ({ createElk: () => elk }));
vi.mock('./app/App', () => ({
  App: ({ elk: given }: { elk: unknown }) => <p>{given === elk ? 'App with its ELK' : 'App without it'}</p>,
}));

beforeEach(() => {
  vi.resetModules();
  document.body.innerHTML = '';
});

it('renders the App with a fresh ELK into #root', async () => {
  document.body.innerHTML = '<div id="root"></div>';
  await act(async () => {
    await import('./main');
  });
  expect(document.getElementById('root')?.textContent).toBe('App with its ELK');
});

it('fails loudly when index.html has no #root', async () => {
  await expect(import('./main')).rejects.toThrow('index.html has no #root element');
});
