import { pageWindow } from './pagination';

describe('pageWindow', () => {
  it('az sayfada hepsini gösterir', () => {
    expect(pageWindow(2, 4)).toEqual([1, 2, 3, 4]);
  });

  it('çok sayfada ilk, son ve geçerli sayfanın çevresini gösterir', () => {
    expect(pageWindow(15, 40)).toEqual([1, 13, 14, 15, 16, 17, 40]);
    expect(pageWindow(40, 40)).toEqual([1, 38, 39, 40]);
  });
});
