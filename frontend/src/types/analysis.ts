export type DirectionTone = 'positive' | 'negative' | 'neutral' | 'pending';

export function getDirectionTone(direction?: number | null): DirectionTone {
  if (direction === null || direction === undefined) return 'pending';
  if (direction > 0) return 'positive';
  if (direction < 0) return 'negative';
  return 'neutral';
}

export function getDirectionLabel(direction?: number | null): string {
  if (direction === null || direction === undefined) return 'Pending direction';
  if (direction > 0) return 'Bullish / positive';
  if (direction < 0) return 'Bearish / negative';
  return 'Neutral / unclear';
}

export function getAnalyzerProvider(analyzerVersion?: string | null): string {
  if (!analyzerVersion) return 'Unknown';
  if (analyzerVersion.toLowerCase().startsWith('openai-')) return 'OpenAI';
  if (analyzerVersion.toLowerCase().startsWith('mock-')) return 'Mock';
  return analyzerVersion;
}

export function hasNoTextContent(content?: string | null): boolean {
  return content === '[No text content]';
}
