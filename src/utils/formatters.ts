/**
 * Formatter utilities for Jellyfin Cast & Crew
 */

const COUNTRY_MAP: Record<string, string> = {
  japan: 'JP',
  jp: 'JP',
  '日本': 'JP',
  'united states': 'US',
  usa: 'US',
  us: 'US',
  america: 'US',
  '美国': 'US',
  'united kingdom': 'GB',
  uk: 'GB',
  gb: 'GB',
  england: 'GB',
  'great britain': 'GB',
  '英国': 'GB',
  france: 'FR',
  fr: 'FR',
  '法国': 'FR',
  germany: 'DE',
  de: 'DE',
  '德国': 'DE',
  italy: 'IT',
  it: 'IT',
  '意大利': 'IT',
  spain: 'ES',
  es: 'ES',
  '西班牙': 'ES',
  canada: 'CA',
  ca: 'CA',
  '加拿大': 'CA',
  australia: 'AU',
  au: 'AU',
  '澳大利亚': 'AU',
  chile: 'CL',
  cl: 'CL',
  '智利': 'CL',
  ireland: 'IE',
  ie: 'IE',
  '爱尔兰': 'IE',
  china: 'CN',
  cn: 'CN',
  '中国': 'CN',
  'hong kong': 'HK',
  hk: 'HK',
  '香港': 'HK',
  taiwan: 'TW',
  tw: 'TW',
  '台湾': 'TW',
  'south korea': 'KR',
  korea: 'KR',
  kr: 'KR',
  '韩国': 'KR',
  singapore: 'SG',
  sg: 'SG',
  '新加坡': 'SG',
  thailand: 'TH',
  th: 'TH',
  '泰国': 'TH',
  international: 'WW',
  global: 'WW',
};

/**
 * Converts a country name or location string into a 2-letter uppercase country code (ISO 3166-1 alpha-2).
 * Examples:
 *   "Japan" -> "JP"
 *   "United States" -> "US"
 *   "Tokyo, Japan" -> "JP"
 *   "Los Angeles, California, USA" -> "US"
 *   "JP" -> "JP"
 */
export function toCountryCode(location?: string): string {
  if (!location) return '';
  const trimmed = location.trim();
  if (!trimmed) return '';

  // Already 2 uppercase letters
  if (/^[A-Z]{2}$/.test(trimmed)) {
    return trimmed;
  }

  const lower = trimmed.toLowerCase();

  // Direct match
  if (COUNTRY_MAP[lower]) {
    return COUNTRY_MAP[lower];
  }

  // If location has commas, e.g. "Tokyo, Japan" or "Los Angeles, California, USA"
  if (trimmed.includes(',')) {
    const parts = trimmed.split(',').map((p) => p.trim());
    const lastPart = parts[parts.length - 1].toLowerCase();
    if (COUNTRY_MAP[lastPart]) {
      return COUNTRY_MAP[lastPart];
    }
    // Also check second to last
    for (let i = parts.length - 1; i >= 0; i--) {
      const pLower = parts[i].toLowerCase();
      if (COUNTRY_MAP[pLower]) {
        return COUNTRY_MAP[pLower];
      }
    }
  }

  // Substring match
  for (const [key, code] of Object.entries(COUNTRY_MAP)) {
    if (lower.includes(key)) {
      return code;
    }
  }

  // Fallback to first two letters uppercase
  const letters = trimmed.replace(/[^a-zA-Z]/g, '').toUpperCase();
  return letters.slice(0, 2) || trimmed.slice(0, 2).toUpperCase();
}

/**
 * Formats a birth date string.
 * In Chinese ('zh'): formats strictly as "YYYY年M月D日" (年 月 日).
 * In English ('en'): formats as "Month Day, Year".
 */
export function formatBirthDate(
  dateStr?: string,
  locale: 'en' | 'zh' = 'zh',
  fallbackYear?: number
): string {
  if (!dateStr && fallbackYear) {
    return locale === 'zh' ? `${fallbackYear}年` : `${fallbackYear}`;
  }
  if (!dateStr) return locale === 'zh' ? '未收录' : 'Unknown';

  const trimmed = dateStr.trim();

  // Already in "YYYY年M月D日" format
  if (/^\d{4}年\d{1,2}月\d{1,2}日$/.test(trimmed)) {
    if (locale === 'zh') return trimmed;
  }

  // Try ISO YYYY-MM-DD
  const isoMatch = trimmed.match(/^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})/);
  if (isoMatch) {
    const y = parseInt(isoMatch[1], 10);
    const m = parseInt(isoMatch[2], 10);
    const d = parseInt(isoMatch[3], 10);
    if (locale === 'zh') {
      return `${y}年${m}月${d}日`;
    }
    const monthNames = [
      'January', 'February', 'March', 'April', 'May', 'June',
      'July', 'August', 'September', 'October', 'November', 'December'
    ];
    return `${monthNames[m - 1] || m} ${d}, ${y}`;
  }

  // Try natural English format e.g. "May 25, 1987" or "November 11, 1974"
  const englishMonthMatch = trimmed.match(
    /(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2}),?\s*(\d{4})/i
  );
  if (englishMonthMatch) {
    const monthsMap: Record<string, number> = {
      january: 1, february: 2, march: 3, april: 4, may: 5, june: 6,
      july: 7, august: 8, september: 9, october: 10, november: 11, december: 12,
    };
    const m = monthsMap[englishMonthMatch[1].toLowerCase()] || 1;
    const d = parseInt(englishMonthMatch[2], 10);
    const y = parseInt(englishMonthMatch[3], 10);
    if (locale === 'zh') {
      return `${y}年${m}月${d}日`;
    }
    return `${englishMonthMatch[1]} ${d}, ${y}`;
  }

  // Date object parse fallback
  const parsed = new Date(trimmed);
  if (!isNaN(parsed.getTime())) {
    const y = parsed.getFullYear();
    const m = parsed.getMonth() + 1;
    const d = parsed.getDate();
    if (locale === 'zh') {
      return `${y}年${m}月${d}日`;
    }
    const monthNames = [
      'January', 'February', 'March', 'April', 'May', 'June',
      'July', 'August', 'September', 'October', 'November', 'December'
    ];
    return `${monthNames[parsed.getMonth()]} ${d}, ${y}`;
  }

  return trimmed;
}
