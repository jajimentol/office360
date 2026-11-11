import { formatDateTimeLabel } from './date';

describe('formatDateTimeLabel', () => {
  it('kaydı Türkiye saatiyle gösterir', () => {
    expect(formatDateTimeLabel(new Date('2026-11-08T09:15:00Z'))).toBe('8 Kasım 2026 12:15');
  });

  it('gece yarısını geçen saatte Türkiye gününü gösterir', () => {
    expect(formatDateTimeLabel(new Date('2026-11-08T21:30:00Z'))).toBe('9 Kasım 2026 00:30');
  });
});
