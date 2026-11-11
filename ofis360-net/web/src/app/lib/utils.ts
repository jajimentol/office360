import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** Tailwind sinif birlestirme yardimcisi. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** Ad soyaddan bas harfleri uretir (avatar rozetleri icin). */
export function initialsOf(fullName: string): string {
  const parts = fullName.trim().split(/\s+/);
  if (parts.length === 0) return '';
  if (parts.length === 1) return parts[0].slice(0, 2).toLocaleUpperCase('tr-TR');
  return `${parts[0][0] ?? ''}${parts[parts.length - 1][0] ?? ''}`.toLocaleUpperCase('tr-TR');
}

/** Yuzde degerini "%73" bicimine cevirir. */
export function formatPercent(value: number, fractionDigits = 0): string {
  if (!Number.isFinite(value)) return '%0';
  return `%${value.toFixed(fractionDigits)}`;
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat('tr-TR').format(value);
}

/** 0 bolen guvenli oran hesabi (yuzde olarak). */
export function ratio(numerator: number, denominator: number): number {
  if (!denominator) return 0;
  return (numerator / denominator) * 100;
}

/** Dizi elemanlarini bir anahtara gore gruplar. */
export function groupBy<T, K extends string | number>(
  items: T[],
  keyOf: (item: T) => K,
): Map<K, T[]> {
  const map = new Map<K, T[]>();
  for (const item of items) {
    const key = keyOf(item);
    const bucket = map.get(key);
    if (bucket) bucket.push(item);
    else map.set(key, [item]);
  }
  return map;
}

/** Basit CSV uretimi - degerler kacislanir ve UTF-8 BOM eklenir. */
export function toCsv(headers: string[], rows: Array<Array<string | number | null>>): string {
  const escape = (value: string | number | null): string => {
    if (value === null || value === undefined) return '';
    const text = String(value);
    if (/[";\n]/.test(text)) return `"${text.replace(/"/g, '""')}"`;
    return text;
  };
  const lines = [headers.map(escape).join(';')];
  for (const row of rows) {
    lines.push(row.map(escape).join(';'));
  }
  // BOM: Excel'in Turkce karakterleri dogru okumasi icin gereklidir.
  return `﻿${lines.join('\r\n')}`;
}

/** Deterministik sozde-rastgele sayi uretici (seed verilerinin tekrarlanabilir olmasi icin). */
export function createSeededRandom(seed: number) {
  let state = seed >>> 0;
  return () => {
    state = (state * 1_664_525 + 1_013_904_223) >>> 0;
    return state / 0xffff_ffff;
  };
}
