/** Sayfa bağlantıları: ilk, son ve geçerli sayfanın iki yanı. Sıralı ve tekrarsızdır. */
export function pageWindow(current: number, count: number): number[] {
  const pages = new Set([1, count]);
  for (let page = current - 2; page <= current + 2; page += 1) {
    if (page >= 1 && page <= count) pages.add(page);
  }
  return [...pages].sort((a, b) => a - b);
}
