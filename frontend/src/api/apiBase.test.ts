import { describe, expect, it } from 'vitest';
import { API_BASE, apiUrl } from './apiBase';

describe('apiBase', () => {
  it('drops the trailing slash so fetchBaseQuery joins cleanly', () => {
    // fetchBaseQuery only inserts a separator when the base does NOT end in '/'. With
    // '/snipit/' it would concatenate straight onto '/api/...' and produce '/snipitapi/...',
    // which fails as a 404 that looks nothing like a base-path problem.
    expect(API_BASE.endsWith('/')).toBe(false);
    expect(API_BASE).toBe('/snipit');
  });

  it('prefixes app-absolute paths', () => {
    expect(apiUrl('/api/transcriptions/abc/source')).toBe('/snipit/api/transcriptions/abc/source');
  });
});
