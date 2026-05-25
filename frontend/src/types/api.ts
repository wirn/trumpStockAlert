export type Direction = 'Positive' | 'Negative' | 'Neutral' | 'Uncertain' | string;

export interface PostAnalysis {
  id: number;
  postId: number;
  marketImpactScore: number;
  direction: Direction;
  reasoning: string;
  affectedAssetsJson?: string | null;
  confidence?: number | null;
  confidenceScore?: number | null;
  analyzerVersion: string;
  rawAiResponse?: string | null;
  analyzedAt: string;
  createdAt: string;
}

export interface TruthPost {
  id: number;
  source: string;
  author: string;
  externalId: string;
  url: string;
  content: string;
  createdAt: string;
  collectedAt: string;
  savedAtUtc: string;
  hasImage: boolean;
  imageUrls: string[];
  raw?: unknown;
  analysis?: PostAnalysis | null;
}

export interface Alert {
  id: number;
  postId: number;
  postAnalysisId: number;
  alertType: string;
  recipient: string;
  subject: string;
  body: string;
  threshold: number;
  sentAt?: string | null;
  sendStatus: string;
  errorMessage?: string | null;
  createdAt: string;
}

export interface AnalysisRunResult {
  analyzedCount: number;
  skippedCount: number;
  skippedAlreadyAnalyzedCount?: number;
  skippedNoTextContentCount?: number;
  failedCount: number;
  errorCount?: number;
  message: string;
  analyzedPostIds: number[];
  failedPostIds: number[];
}

export interface CollectorRunResult {
  status: string;
  startedAt: string;
  finishedAt: string;
  durationMs: number;
  fetchedCount: number;
  insertedCount: number;
  duplicateCount: number;
  errorCount: number;
  message: string;
  success?: boolean;
  fetchedPosts?: number | null;
  savedPosts?: number | null;
  skippedPosts?: number | null;
  failedPosts?: number | null;
}

export interface FetcherRun {
  id: number;
  startedAt: string;
  finishedAt: string;
  durationMs: number;
  status: string;
  triggerType: string;
  fetchedCount: number;
  insertedCount: number;
  duplicateCount: number;
  errorCount: number;
  message: string;
}
